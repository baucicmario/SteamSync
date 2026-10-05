# 17 — File-by-file catalog

## Tier 1 — Must understand

### UI composition

- Program.cs — process entry.
- App.axaml.cs — application initialization and MainWindow creation.
- MainViewModel.cs — composition root and page navigation.
- GameListViewModel.cs — main use-case orchestration.
- MainWindow.axaml — UI shell.
- GameListView.axaml — detection/sync UI.

### Core orchestration

- GameDetectionService.cs — detector execution and normalization.
- IGameDetector.cs — detector contract.
- DetectedGame.cs — central model.
- GameRepository.cs — persistence operations.
- SteamInjectorService.cs — Steam projection.
- SteamPathResolver.cs — Steam filesystem discovery.
- ShortcutsVdfParser.cs — binary VDF input.
- ShortcutsVdfWriter.cs — binary VDF output.
- AppIdGenerator.cs — Steam AppID identity.
- Crc32.cs — checksum primitive.
- ArtworkManager.cs — artwork application logic.

## Tier 2 — Should understand

### Detection

- EpicGamesDetector.cs — Epic manifests/catalog.
- GogDetector.cs — registry + GOG SQLite.
- UbisoftDetector.cs — registry + protobuf cache.
- EaAppDetector.cs — EA local data.
- EaContentIdResolver.cs — EA ID translation.
- BattleNetDetector.cs — Battle.net local state.
- RockstarDetector.cs — Rockstar local state.
- XboxDetector.cs — installed Windows Store/MSIX detection.
- CustomFolderScanner.cs — heuristic standalone game scan.

### Artwork

- SteamGridDbClient.cs — HTTP API client.
- UninstalledImageProcessor.cs — grayscale/uninstalled pipeline.

### Utilities

- TitleSanitizer.cs — search/display normalization.
- ExecutableMetadataReader.cs — PE metadata.
- VrDetectionUtility.cs — VR heuristic/API lookup.
- SyncLogger.cs — logging.

### UI

- SettingsViewModel.cs — settings persistence.
- SettingsView.axaml — settings UI.
- ViewLocator.cs — ViewModel-to-View convention.

## Tier 3 — Useful reference

### Models

- SteamShortcut.cs
- SteamGridDbModels.cs
- SteamStoreModels.cs
- SteamStoreModels.cs

### Launcher executable helpers

- BattleNetExecutableGenerator.cs
- DummyExecutableGenerator.cs
- EaExecutableGenerator.cs
- EpicExecutableGenerator.cs
- GogExecutableGenerator.cs
- UbisoftExecutableGenerator.cs

These are most useful when investigating uninstalled-game launch behavior.

### Supporting UI files

- App.axaml
- MainWindow.axaml.cs
- GameListView.axaml.cs
- SettingsView.axaml.cs
- ViewModelBase.cs

### Tests

All files under tests/SteamSync.Core.Tests/ are reference material after the core model is understood.

## File relationship map

<pre>
Program
  -> App
  -> MainViewModel
       -> GameListViewModel
            -> GameDetectionService
                 -> IGameDetector implementations
            -> GameRepository
            -> ArtworkManager
            -> UninstalledImageProcessor
            -> SteamInjectorService
       -> SettingsViewModel
</pre>

## How to read a Tier 2 detector

Use this sequence:

1. constructor/settings;
2. Name/PlatformId;
3. DetectGamesAsync;
4. source paths;
5. parsing method;
6. executable selection;
7. DetectedGame construction;
8. exception handling;
9. tests.

## How to read a helper

Ask:

- who calls it?
- what invariant does it provide?
- is it pure?
- does it access the filesystem/network?
- is it persisted?
- is it tested?

## Checkpoint

Pick one Tier 2 file and write a 5-sentence explanation without looking at its XML comments.
