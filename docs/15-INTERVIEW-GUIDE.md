# 15 — Interview guide

## 30-second answer

> SteamSync is a Windows .NET desktop application that discovers games across multiple PC launchers and custom folders, normalizes them into a common DetectedGame model, caches their state in SQLite, fetches artwork, and projects selected games into Steam as Non-Steam shortcuts. The interesting engineering parts are the heterogeneous detector layer, the custom binary shortcuts.vdf parser/writer, deterministic Steam AppID generation, artwork processing, and Steam process lifecycle management.

## 2-minute answer

Start with the architecture:

> The UI is Avalonia/MVVM. MainViewModel acts as the composition root and creates the core services. GameListViewModel handles user-facing detection and sync workflows. In the core library, GameDetectionService orchestrates a set of IGameDetector implementations for Epic, GOG, Ubisoft, EA, Battle.net, Rockstar, Xbox and custom directories. Each detector returns DetectedGame, so downstream code doesn't care where a game came from. Results are deduplicated, enriched with AppID/VR/artwork state and stored in SQLite. When the user syncs, ArtworkManager gets assets and SteamInjectorService converts the normalized model into SteamShortcut records and writes Steam's binary shortcuts.vdf while preserving non-SteamSync shortcuts.

Then mention Force Sync:

> Force Sync kills Steam, writes the configuration, and relaunches Steam to avoid file-locking issues.

## 5-minute walkthrough

### 1. Entry point

Program.cs → App.axaml.cs → MainViewModel.

### 2. Composition

MainViewModel constructs logger, detector service, repository, artwork services and injector.

### 3. Domain model

DetectedGame is the common model.

### 4. Detection

GameDetectionService configures detectors and executes them.

### 5. Normalization

Results are deduplicated and enriched.

### 6. Persistence

GameRepository maps DetectedGame to SQLite.

### 7. Artwork

ArtworkManager talks to SteamGridDB and writes Steam-compatible image names.

### 8. Steam

SteamInjectorService maps DetectedGame → SteamShortcut and writes binary VDF.

### 9. Process lifecycle

ForceSync uses SteamProcessManager.

### 10. Testing

xUnit tests the core behavior, especially detector contracts, VDF round trips, AppID calculation and title sanitization.

## Likely interviewer questions

### Why did you use an IGameDetector?

**Answer:** It isolates launcher-specific knowledge behind a common contract. The orchestrator can run different sources without knowing their internal formats.

### Why not have detectors write to Steam directly?

**Answer:** That would couple discovery to output. Keeping detection normalized lets the same game model feed SQLite, artwork, UI and Steam integration.

### How do you avoid overwriting user shortcuts?

**Answer:** SteamSync tags its managed entries. During sync it parses the existing VDF, separates managed and unmanaged entries, keeps unmanaged entries, and regenerates its managed set.

### How are Steam AppIDs generated?

**Answer:** CRC32 of executable path plus title, then the non-Steam bit is set. The full 64-bit form adds the non-Steam type marker.

### What happens if one launcher detector fails?

**Answer:** GameDetectionService catches the detector exception, logs it, reports zero for progress, and continues with the remaining detectors.

### Why SQLite?

**Answer:** It is a small local application with a need for durable cache/state, not a server database. SQLite gives persistence without requiring a separate service.

### Why not EF Core?

**Answer:** The current implementation uses Microsoft.Data.Sqlite directly, which keeps the local persistence layer small and explicit. The tradeoff is handwritten mapping/schema management.

### How do you support uninstalled games?

**Answer:** Ownership and installation are separate. Owned-but-uninstalled games can be detected from launcher caches/catalogs. The application creates processed grayscale artwork and a launcher/dummy launch representation so the game can still appear in Steam.

### Why restart Steam?

**Answer:** Steam can hold shortcuts.vdf and related state open. Force Sync removes that process-level contention before writing, then starts Steam again.

### What would you improve?

Good answers include:

- introduce a dedicated application/use-case layer;
- reduce GameListViewModel responsibilities;
- replace title-based identity with stronger source IDs where possible;
- formalize database migrations;
- move API secrets to a secure OS credential store;
- add integration tests around real file fixtures;
- add CI;
- make launcher parsers easier to update independently.

### What is the most interesting technical part?

A strong answer is the Steam integration:

> SteamSync has to interact with a binary format that Steam owns rather than an application-defined JSON API. The parser/writer, deterministic AppID generation, tags and merge strategy form a complete projection layer from the application's normalized model into Steam's expected representation.

## Interview trap: Playnite

If asked whether SteamSync uses Playnite:

> The repository's project description describes a Playnite SDK shim architecture, but the current master source does not contain that shim or Playnite SDK integration. The implemented detector architecture is direct launcher-specific detection. There are some indirect Playnite-data references, especially for EA legacy mappings.

That answer demonstrates that you inspected the actual code rather than memorizing the project description.

## Whiteboard exercise

Draw:

<pre>
Launchers / folders
       ↓
IGameDetector implementations
       ↓
GameDetectionService
       ↓
DetectedGame
   ↙       ↓       ↘
SQLite   Artwork   Steam
                    ↓
               SteamShortcut
                    ↓
              shortcuts.vdf
</pre>

## Final interview rule

Always anchor claims to a concrete file.

For example:

> "GameDetectionService does X — you can see that in GameDetectionService.cs."

That makes your explanation credible and helps you recover if the interviewer asks a follow-up.
