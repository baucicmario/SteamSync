# 02 — Repository map

## Top level

```
SteamSync/
├── README.md
├── project description.txt
├── SteamSync.sln
├── Directory.Build.targets
├── .gitmodules
├── references/
├── src/
└── tests/
```

### Top-level files

| Path | Role | Priority |
|---|---|---|
| `README.md` | user-facing overview, build/run/test instructions | Tier 1 |
| `project description.txt` | design intent / older project specification | Tier 2, verify against code |
| `SteamSync.sln` | solution/project wiring | Tier 1 |
| `Directory.Build.targets` | solution-wide MSBuild target | Tier 3 |
| `.gitmodules` | declares BoilR submodule | Tier 2 |

## `src/SteamSync.Core`

This is the non-UI engine.

### Artwork

- `ArtworkManager.cs` — resolves titles, chooses images, writes Steam grid assets.
- `SteamGridDbClient.cs` — HTTP client for SteamGridDB.
- `UninstalledImageProcessor.cs` — creates grayscale / branded artwork for owned-but-uninstalled games.

### Data

- `SteamSyncDbContext.cs` — owns the SQLite connection/schema.
- `GameRepository.cs` — CRUD/upsert mapping between SQLite rows and `DetectedGame`.

### Detection

- `IGameDetector.cs` — detector contract.
- `GameDetectionService.cs` — orchestrates detectors and enriches results.
- `EpicGamesDetector.cs`
- `GogDetector.cs`
- `UbisoftDetector.cs`
- `EaAppDetector.cs`
- `BattleNetDetector.cs`
- `RockstarDetector.cs`
- `XboxDetector.cs`
- `CustomFolderScanner.cs`
- `EaContentIdResolver.cs` — EA-specific ID translation / Playnite cache lookup.

### Models

- `DetectedGame.cs` — central domain-ish model.
- `SteamShortcut.cs` — in-memory representation of a `shortcuts.vdf` record.
- `AppSettings.cs` — settings + filesystem paths.
- `SteamGridDbModels.cs` — DTOs for SteamGridDB.
- `SteamStoreModels.cs` — DTOs for Steam Store API lookup.

### Steam

- `AppIdGenerator.cs`
- `BattleNetExecutableGenerator.cs`
- `DummyExecutableGenerator.cs`
- `EaExecutableGenerator.cs`
- `EpicExecutableGenerator.cs`
- `GogExecutableGenerator.cs`
- `ShortcutsVdfParser.cs`
- `ShortcutsVdfWriter.cs`
- `SteamInjectorService.cs`
- `SteamPathResolver.cs`
- `SteamProcessManager.cs`
- `UbisoftExecutableGenerator.cs`

### Utilities

- `Crc32.cs`
- `ExecutableMetadataReader.cs`
- `TitleSanitizer.cs`
- `VrDetectionUtility.cs`

### Logging

- `SyncLogger.cs`

## `src/SteamSync.UI`

Avalonia desktop shell.

- `Program.cs` — process entry point.
- `App.axaml.cs` — creates `MainWindow` and its main ViewModel.
- `ViewLocator.cs` — ViewModel → View convention.
- `ViewModels/MainViewModel.cs` — manual service composition/navigation.
- `ViewModels/GameListViewModel.cs` — detection + sync orchestration and UI state.
- `ViewModels/SettingsViewModel.cs` — settings persistence and directory picker.
- `Views/MainWindow.axaml` — application shell/sidebar.
- `Views/GameListView.axaml` — game table/actions/logs.
- `Views/SettingsView.axaml` — settings form.
- matching `.axaml.cs` code-behind files.

## `tests`

`SteamSync.Core.Tests` is the only test project currently present.

The tests focus on:

- detectors
- detection orchestration
- AppID generation
- VDF round-tripping
- title sanitization
- executable-generator behavior
- artwork logic
- Steam injection tags
- application settings

## References

`references/BoilR` is a Git submodule.

It is not part of the C# compilation of SteamSync; it is an external reference source used to inform the Steam integration behavior.

## Important absence

The repository tree analyzed at commit `500444a2751f38d34ea402fabea157a1b8a925d8` contains no `.github/workflows`, Dockerfile, or `publish/` directory.

Treat README references to `publish/` as stale unless another branch/release artifact proves otherwise.

## Checkpoint

Open the tree and classify each file into:

- UI
- orchestration
- domain/data
- integration
- utility
- test
- documentation

Then identify the Tier 1 files from each category.
