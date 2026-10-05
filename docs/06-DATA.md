# 06 — Persistence and SQLite

## Database boundary

Persistence is intentionally lightweight.

The two core files are:

- src/SteamSync.Core/Data/SteamSyncDbContext.cs
- src/SteamSync.Core/Data/GameRepository.cs

This is direct Microsoft.Data.Sqlite usage, not Entity Framework Core.

## Database location

AppSettings.GetDatabaseFilePath resolves:

%AppData%\SteamSync\steamsync.db

SteamSyncDbContext opens that file and ensures the schema exists.

## Games table

The important fields are:

- Id
- Title
- Platform
- IsOwned
- IsInstalled
- ExePath
- StartDir
- LaunchArguments
- SteamAppId
- SteamGridDbId
- ArtworkCached
- IconPath
- LastSynced
- CreatedAt
- UpdatedAt
- IsVR
- OfficialSteamAppId

Indexes exist for title, platform and SteamAppId.

## Repository responsibilities

GameRepository contains:

- GetAll
- GetById
- GetByTitleAndPlatform
- GetUninstalledOwnedGames
- Upsert
- UpsertMany
- Insert
- Update
- Delete
- MarkAllUninstalled
- ClearAll

The rest of the application therefore does not need to issue SQL for ordinary game operations.

## Upsert identity

Upsert uses Title + Platform to decide whether a row already exists.

That identity is different from the Steam AppID identity.

When an existing game is found, the repository can carry forward useful state such as:

- SteamAppId
- ArtworkCached
- SteamGridDbId
- OfficialSteamAppId
- LastSynced

## Transaction boundary

UpsertMany wraps the batch in a SQLite transaction.

If one write fails, the transaction is rolled back.

That is an important consistency guarantee.

## Lightweight schema evolution

SteamSyncDbContext uses a simple compatibility trick:

- create the full table;
- attempt ALTER TABLE statements for newer columns;
- ignore duplicate-column errors.

This is a home-grown migration strategy rather than a formal migration system.

## Data lifecycle

<pre>
detectors
  -> GameDetectionService
  -> GameListViewModel
  -> GameRepository.UpsertMany
  -> SQLite

application start
  -> GameRepository.GetAll
  -> ObservableCollection&lt;DetectedGame&gt;
  -> Avalonia UI
</pre>

## Important mental model

SQLite is local application state and cache.

The external launchers and Windows installation state are the sources from which new detection facts are derived.

## Interview question to practice

Why not use the database as the source of truth?

Strong answer:

> The database is useful for state, history and cached metadata, but launcher-specific installation/ownership facts are discovered from the local launcher sources. Treating the database as authoritative would make stale ownership/install data difficult to reconcile.

## Checkpoint

Open GameRepository.Upsert and explain the insert/update decision and every field intentionally preserved from the previous row.
