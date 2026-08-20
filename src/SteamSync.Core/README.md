# SteamSync.Core

**Purpose:** This project contains the core business logic, domain models, and platform integration engines for SteamSync. It is responsible for scanning third-party launchers and custom folders, managing the local SQLite cache, downloading SteamGridDB artwork, and injecting shortcuts into Steam.

**Key Components:**
- `Detection/`: Storefront detectors (Battle.net, EA App, Epic Games, GOG, Rockstar, Ubisoft, Xbox) and heuristic custom folder scanners.
- `Steam/`: Binary `shortcuts.vdf` parser and writer, 64-bit AppID generator, launcher executable wrappers, and Steam process lifecycle manager.
- `Artwork/`: SteamGridDB API client, artwork downloader/cache manager, and image processors for uninstalled title overlays.
- `Data/`: SQLite database context (`SteamSyncDbContext`) and repository layer (`GameRepository`) for state persistence.
- `Models/`: Data models for detected games, application settings, Steam shortcuts, and API payloads.
- `Utilities/`: Helper utilities for CRC32 hashing, executable metadata inspection, regex title sanitization, and VR detection.
- `Logging/`: Synchronization logger (`SyncLogger`) for operation auditing.
- `Assets/`: Embedded launcher logos and platform icons.

**Dependencies:** Relies on NuGet packages including `Microsoft.Data.Sqlite`, `SixLabors.ImageSharp`, `protobuf-net`, `YamlDotNet`, and `CommunityToolkit.Mvvm`; referenced by `src/SteamSync.UI` and `tests/SteamSync.Core.Tests`.
