using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CoffeeStockWidget.Core.Abstractions;
using CoffeeStockWidget.Core.Models;
using Microsoft.Data.Sqlite;

namespace CoffeeStockWidget.Infrastructure.Storage;

public class SqliteDataStore : IDataStore
{
    private readonly string _dbPath;

    public SqliteDataStore(string? dbPath = null)
    {
        _dbPath = dbPath ?? GetDefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        EnsureCreated();
    }

    private static string GetDefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "CoffeeStockWidget");
        return Path.Combine(dir, "db.sqlite");
    }

    private SqliteConnection Open()
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath };
        var conn = new SqliteConnection(cs.ToString());
        conn.Open();
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys=ON;";
            pragma.ExecuteNonQuery();
        }
        return conn;
    }

    private void EnsureCreated()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Sources (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Name TEXT NOT NULL,
  RootUrl TEXT NOT NULL,
  ParserType TEXT NOT NULL,
  PollIntervalSeconds INTEGER NOT NULL DEFAULT 300,
  Enabled INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS Items (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  SourceId INTEGER NOT NULL,
  ItemKey TEXT NOT NULL,
  Title TEXT NOT NULL,
  Url TEXT NOT NULL,
  PriceCents INTEGER,
  InStock INTEGER NOT NULL,
  FirstSeenUtc TEXT NOT NULL,
  LastSeenUtc TEXT NOT NULL,
  AttributesJson TEXT,
  UNIQUE(SourceId, ItemKey),
  FOREIGN KEY(SourceId) REFERENCES Sources(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Events (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  SourceId INTEGER NOT NULL,
  ItemId INTEGER NOT NULL,
  EventType TEXT NOT NULL,
  EventDataJson TEXT,
  CreatedUtc TEXT NOT NULL,
  FOREIGN KEY(SourceId) REFERENCES Sources(Id) ON DELETE CASCADE,
  FOREIGN KEY(ItemId) REFERENCES Items(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Settings (
  Id INTEGER PRIMARY KEY CHECK (Id = 1),
  DataJson TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_Items_Source_InStock ON Items(SourceId, InStock);
CREATE INDEX IF NOT EXISTS IX_Items_Url ON Items(Url);
CREATE INDEX IF NOT EXISTS IX_Events_Source_Created ON Events(SourceId, CreatedUtc);
";
        cmd.ExecuteNonQuery();
    }

    public async Task UpsertItemsAsync(IEnumerable<CoffeeItem> items, CancellationToken ct = default)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();

        foreach (var i in items)
        {
            var attrs = i.Attributes != null ? JsonSerializer.Serialize(i.Attributes) : null;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Items (SourceId, ItemKey, Title, Url, PriceCents, InStock, FirstSeenUtc, LastSeenUtc, AttributesJson)
VALUES ($sid, $key, $title, $url, $price, $stock, $first, $last, $attrs)
ON CONFLICT(SourceId, ItemKey) DO UPDATE SET
  Title=excluded.Title,
  Url=excluded.Url,
  PriceCents=excluded.PriceCents,
  InStock=excluded.InStock,
  LastSeenUtc=excluded.LastSeenUtc,
  AttributesJson=excluded.AttributesJson;
";
            cmd.Parameters.AddWithValue("$sid", i.SourceId);
            cmd.Parameters.AddWithValue("$key", i.ItemKey);
            cmd.Parameters.AddWithValue("$title", i.Title);
            cmd.Parameters.AddWithValue("$url", i.Url.ToString());
            cmd.Parameters.AddWithValue("$price", (object?)i.PriceCents ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$stock", i.InStock ? 1 : 0);
            cmd.Parameters.AddWithValue("$first", i.FirstSeenUtc.UtcDateTime.ToString("o"));
            cmd.Parameters.AddWithValue("$last", i.LastSeenUtc.UtcDateTime.ToString("o"));
            cmd.Parameters.AddWithValue("$attrs", (object?)attrs ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        tx.Commit();
    }

    public async Task UpsertSourceAsync(Source source, CancellationToken ct = default)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Sources (Id, Name, RootUrl, ParserType, PollIntervalSeconds, Enabled)
VALUES ($id, $name, $root, $parser, $interval, $enabled)
ON CONFLICT(Id) DO UPDATE SET
  Name=excluded.Name,
  RootUrl=excluded.RootUrl,
  ParserType=excluded.ParserType,
  PollIntervalSeconds=excluded.PollIntervalSeconds,
  Enabled=excluded.Enabled;
";
        cmd.Parameters.AddWithValue("$id", (object?)source.Id ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$name", source.Name);
        cmd.Parameters.AddWithValue("$root", source.RootUrl.ToString());
        cmd.Parameters.AddWithValue("$parser", source.ParserType);
        cmd.Parameters.AddWithValue("$interval", source.PollIntervalSeconds);
        cmd.Parameters.AddWithValue("$enabled", source.Enabled ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        if (!source.Id.HasValue)
        {
            // Retrieve last inserted id
            using var idCmd = conn.CreateCommand();
            idCmd.CommandText = "SELECT Id FROM Sources WHERE RootUrl=$root LIMIT 1;";
            idCmd.Parameters.AddWithValue("$root", source.RootUrl.ToString());
            var result = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (result is long lid)
            {
                source.Id = (int)lid;
            }
        }
    }

    public async Task<IReadOnlyList<CoffeeItem>> GetItemsBySourceAsync(int sourceId, CancellationToken ct = default)
    {
        var list = new List<CoffeeItem>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Id, ItemKey, Title, Url, PriceCents, InStock, FirstSeenUtc, LastSeenUtc, AttributesJson FROM Items WHERE SourceId=$sid";
        cmd.Parameters.AddWithValue("$sid", sourceId);
        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rdr.ReadAsync(ct).ConfigureAwait(false))
        {
            var attrsJson = rdr.IsDBNull(8) ? null : rdr.GetString(8);
            var attrs = string.IsNullOrWhiteSpace(attrsJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, string>>(attrsJson!);

            list.Add(new CoffeeItem
            {
                Id = rdr.GetInt32(0),
                SourceId = sourceId,
                ItemKey = rdr.GetString(1),
                Title = rdr.GetString(2),
                Url = new Uri(rdr.GetString(3)),
                PriceCents = rdr.IsDBNull(4) ? null : rdr.GetInt32(4),
                InStock = rdr.GetInt32(5) != 0,
                FirstSeenUtc = DateTimeOffset.Parse(rdr.GetString(6)),
                LastSeenUtc = DateTimeOffset.Parse(rdr.GetString(7)),
                Attributes = attrs
            });
        }
        return list;
    }

    public async Task RecordEventsAsync(IEnumerable<StockChangeEvent> eventsToRecord, CancellationToken ct = default)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        foreach (var e in eventsToRecord)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Events (SourceId, ItemId, EventType, EventDataJson, CreatedUtc) VALUES ($sid, $iid, $type, $data, $created)";
            cmd.Parameters.AddWithValue("$sid", e.SourceId);
            cmd.Parameters.AddWithValue("$iid", e.ItemId);
            cmd.Parameters.AddWithValue("$type", e.EventType.ToString());
            cmd.Parameters.AddWithValue("$data", DBNull.Value);
            cmd.Parameters.AddWithValue("$created", e.CreatedUtc.UtcDateTime.ToString("o"));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        tx.Commit();
    }

    public async Task PruneAsync(RetentionPolicy policy, CancellationToken ct = default)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();

        // Date-based prune for simplicity (can add per-source caps later)
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Abs(policy.Days));
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Events WHERE CreatedUtc < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", cutoff.UtcDateTime.ToString("o"));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // Optional: prune items not seen since cutoff
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Items WHERE LastSeenUtc < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", cutoff.UtcDateTime.ToString("o"));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        tx.Commit();
    }
}
