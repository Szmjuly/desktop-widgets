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

        EnsureSchemaUpgrades(conn);
    }

    private static void EnsureSchemaUpgrades(SqliteConnection conn)
    {
        // Add SeenUtc column to Items if missing
        using var infoCmd = conn.CreateCommand();
        infoCmd.CommandText = "PRAGMA table_info(Items);";
        using var r = infoCmd.ExecuteReader();
        var hasSeen = false;
        while (r.Read())
        {
            var name = r.GetString(1);
            if (string.Equals(name, "SeenUtc", StringComparison.OrdinalIgnoreCase)) { hasSeen = true; break; }
        }
        if (!hasSeen)
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Items ADD COLUMN SeenUtc TEXT";
            alter.ExecuteNonQuery();
        }
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

        // Per-source caps: keep only the most recent N items/events by timestamp
        if (policy.ItemsPerSource > 0)
        {
            // Delete older items beyond the most recent N by LastSeenUtc for each source
            using var getSources = conn.CreateCommand();
            getSources.CommandText = "SELECT Id FROM Sources";
            using var rdr = await getSources.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var sids = new List<int>();
            while (await rdr.ReadAsync(ct).ConfigureAwait(false)) sids.Add(rdr.GetInt32(0));

            foreach (var sid in sids)
            {
                using var capCmd = conn.CreateCommand();
                capCmd.CommandText = @"
DELETE FROM Items
WHERE Id IN (
  SELECT Id FROM Items WHERE SourceId = $sid
  ORDER BY LastSeenUtc DESC
  LIMIT -1 OFFSET $keep
);";
                capCmd.Parameters.AddWithValue("$sid", sid);
                capCmd.Parameters.AddWithValue("$keep", policy.ItemsPerSource);
                await capCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        if (policy.EventsPerSource > 0)
        {
            using var getSources2 = conn.CreateCommand();
            getSources2.CommandText = "SELECT Id FROM Sources";
            using var rdr2 = await getSources2.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var sids2 = new List<int>();
            while (await rdr2.ReadAsync(ct).ConfigureAwait(false)) sids2.Add(rdr2.GetInt32(0));

            foreach (var sid in sids2)
            {
                using var capCmd = conn.CreateCommand();
                capCmd.CommandText = @"
DELETE FROM Events
WHERE Id IN (
  SELECT Id FROM Events WHERE SourceId = $sid
  ORDER BY CreatedUtc DESC
  LIMIT -1 OFFSET $keep
);";
                capCmd.Parameters.AddWithValue("$sid", sid);
                capCmd.Parameters.AddWithValue("$keep", policy.EventsPerSource);
                await capCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        tx.Commit();
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Events";
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Items";
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        tx.Commit();
    }

    public async Task SetItemsUnseenAsync(int sourceId, IEnumerable<string> itemKeys, CancellationToken ct = default)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        foreach (var key in itemKeys)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Items SET SeenUtc = NULL WHERE SourceId=$sid AND ItemKey=$key";
            cmd.Parameters.AddWithValue("$sid", sourceId);
            cmd.Parameters.AddWithValue("$key", key);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        tx.Commit();
    }

    public async Task SetItemSeenAsync(int sourceId, string itemKey, DateTimeOffset seenUtc, CancellationToken ct = default)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Items SET SeenUtc=$seen WHERE SourceId=$sid AND ItemKey=$key";
        cmd.Parameters.AddWithValue("$sid", sourceId);
        cmd.Parameters.AddWithValue("$key", itemKey);
        cmd.Parameters.AddWithValue("$seen", seenUtc.UtcDateTime.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> GetUnseenCountAsync(CancellationToken ct = default)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Items WHERE SeenUtc IS NULL";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is long l ? (int)l : 0;
    }

    public async Task<int> GetTotalItemsCountAsync(CancellationToken ct = default)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Items";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is long l ? (int)l : 0;
    }

    public async Task<Dictionary<int, int>> GetUnseenCountsBySourceAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<int, int>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT SourceId, COUNT(*) FROM Items WHERE SeenUtc IS NULL GROUP BY SourceId";
        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rdr.ReadAsync(ct).ConfigureAwait(false))
        {
            var sid = rdr.GetInt32(0);
            var cnt = rdr.GetInt32(1);
            map[sid] = cnt;
        }
        return map;
    }

    public async Task<List<CoffeeItem>> GetUnseenItemsAsync(CancellationToken ct = default)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Id, SourceId, ItemKey, Title, Url, PriceCents, InStock, FirstSeenUtc, LastSeenUtc
                            FROM Items WHERE SeenUtc IS NULL ORDER BY LastSeenUtc DESC";
        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var list = new List<CoffeeItem>();
        while (await rdr.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(ReadItem(rdr));
        }
        return list;
    }

    public async Task<List<CoffeeItem>> GetUnseenItemsBySourceAsync(int sourceId, CancellationToken ct = default)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Id, SourceId, ItemKey, Title, Url, PriceCents, InStock, FirstSeenUtc, LastSeenUtc
                            FROM Items WHERE SeenUtc IS NULL AND SourceId=$sid ORDER BY LastSeenUtc DESC";
        cmd.Parameters.AddWithValue("$sid", sourceId);
        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var list = new List<CoffeeItem>();
        while (await rdr.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(ReadItem(rdr));
        }
        return list;
    }

    private static CoffeeItem ReadItem(SqliteDataReader rdr)
    {
        var urlStr = rdr.GetString(4);
        Uri url;
        try { url = new Uri(urlStr); } catch { url = new Uri("https://example.invalid"); }
        int? price = rdr.IsDBNull(5) ? null : rdr.GetInt32(5);
        var first = DateTimeOffset.TryParse(rdr.GetString(7), out var f) ? f : DateTimeOffset.UtcNow;
        var last = DateTimeOffset.TryParse(rdr.GetString(8), out var l) ? l : DateTimeOffset.UtcNow;
        return new CoffeeItem
        {
            Id = rdr.IsDBNull(0) ? null : rdr.GetInt32(0),
            SourceId = rdr.GetInt32(1),
            ItemKey = rdr.GetString(2),
            Title = rdr.GetString(3),
            Url = url,
            PriceCents = price,
            InStock = rdr.GetBoolean(6),
            FirstSeenUtc = first,
            LastSeenUtc = last
        };
    }

    public async Task MarkAllSeenAsync(DateTimeOffset seenUtc, CancellationToken ct = default)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Items SET SeenUtc=$seen WHERE SeenUtc IS NULL";
        cmd.Parameters.AddWithValue("$seen", seenUtc.UtcDateTime.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkSourceSeenAsync(int sourceId, DateTimeOffset seenUtc, CancellationToken ct = default)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Items SET SeenUtc=$seen WHERE SourceId=$sid AND SeenUtc IS NULL";
        cmd.Parameters.AddWithValue("$seen", seenUtc.UtcDateTime.ToString("o"));
        cmd.Parameters.AddWithValue("$sid", sourceId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
