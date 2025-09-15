# Data Model

## Tables

```sql
CREATE TABLE Sources (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Name TEXT NOT NULL,
  RootUrl TEXT NOT NULL,
  ParserType TEXT NOT NULL, -- e.g., "BlackAndWhite", "Generic"
  PollIntervalSeconds INTEGER NOT NULL DEFAULT 300,
  Enabled INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE Items (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  SourceId INTEGER NOT NULL,
  ItemKey TEXT NOT NULL,     -- stable key (SKU or normalized hash)
  Title TEXT NOT NULL,
  Url TEXT NOT NULL,
  PriceCents INTEGER,
  InStock INTEGER NOT NULL,
  FirstSeenUtc TEXT NOT NULL,
  LastSeenUtc TEXT NOT NULL,
  AttributesJson TEXT,       -- origin, producer, process, notes
  UNIQUE(SourceId, ItemKey),
  FOREIGN KEY(SourceId) REFERENCES Sources(Id) ON DELETE CASCADE
);

CREATE TABLE Events (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  SourceId INTEGER NOT NULL,
  ItemId INTEGER NOT NULL,
  EventType TEXT NOT NULL,   -- NewItem, BackInStock, PriceChanged, OutOfStock
  EventDataJson TEXT,
  CreatedUtc TEXT NOT NULL,
  FOREIGN KEY(SourceId) REFERENCES Sources(Id) ON DELETE CASCADE,
  FOREIGN KEY(ItemId) REFERENCES Items(Id) ON DELETE CASCADE
);

CREATE TABLE Settings (
  Id INTEGER PRIMARY KEY CHECK (Id = 1),
  DataJson TEXT NOT NULL
);

CREATE INDEX IX_Items_Source_InStock ON Items(SourceId, InStock);
CREATE INDEX IX_Items_Url ON Items(Url);
CREATE INDEX IX_Events_Source_Created ON Events(SourceId, CreatedUtc);
```

## Retention Policy
- Items: keep the latest N per source (e.g., 500) and anything seen within the last M days (e.g., 30).
- Events: keep last K per source (e.g., 1000) and last M days.
- Pragmatic pruning: run at startup and then hourly.

## Settings Example (JSON)
```json
{
  "sources": [
    { "name": "Black & White Roasters", "rootUrl": "https://www.blackwhiteroasters.com/collections/coffee", "parser": "BlackAndWhite", "intervalSeconds": 300, "enabled": true }
  ],
  "retention": { "itemsPerSource": 500, "eventsPerSource": 1000, "days": 30 },
  "network": { "minDelayPerHostMs": 1500, "timeoutSeconds": 15 },
  "notification": { "enabled": true, "quietHours": { "start": "22:00", "end": "07:00" } }
}
```
