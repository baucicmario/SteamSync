# 10 — Important runtime flows

## Flow A — Application startup

<pre>
Program.Main
  -> Avalonia
  -> App.OnFrameworkInitializationCompleted
  -> MainWindow
  -> MainViewModel
  -> core service construction
  -> GameListViewModel
  -> load SQLite games
  -> Games page
</pre>

## Flow B — Detect games

<pre>
Detect button
  -> GameListViewModel.DetectGamesAsync
  -> RunDetectionAsync
  -> ReadSettings
  -> GameDetectionService.ConfigureDefaults
  -> DetectAllGamesAsync
  -> detector 1
  -> detector 2
  -> ...
  -> deduplicate
  -> existing Steam shortcut lookup
  -> AppID
  -> VR detection
  -> artwork-cache check
  -> optional SteamGridDB title resolution
  -> second deduplication
  -> GameRepository.UpsertMany
  -> LoadGamesFromDb
</pre>

### Where to debug a detection problem

Start at:

1. GameListViewModel.RunDetectionAsync
2. GameDetectionService.ConfigureDefaults
3. GameDetectionService.DetectAllGamesAsync
4. the specific detector
5. its local source path / registry / database parser
6. GameRepository.UpsertMany

## Flow C — Title resolution

<pre>
DetectedGame.Title
  -> TitleSanitizer.SanitizeForSearch
  -> SteamGridDbClient.SearchGamesAsync
  -> choose exact/verified/fuzzy result
  -> replace game.Title
  -> regenerate SteamAppId when ExePath exists
</pre>

This matters because changing the title can change the deterministic AppID.

## Flow D — Normal sync

<pre>
selected installed games
  -> persist manual changes
  -> resolve Steam users
  -> download missing artwork
  -> process uninstalled owned games
  -> SteamInjectorService.SyncAsync
  -> parse existing VDF
  -> separate managed/unmanaged
  -> create new SteamShortcut objects
  -> merge
  -> write binary VDF
</pre>

## Flow E — Force sync

<pre>
ForceSync button
  -> PerformSync(true)
  -> artwork processing
  -> SteamInjectorService.ForceSyncAsync
  -> SteamProcessManager.ForceKillAndWaitAsync
  -> SyncAsync
  -> SteamProcessManager.LaunchSteam
</pre>

## Flow F — Load persisted library

<pre>
MainViewModel creates GameListViewModel
  -> constructor
  -> LoadGamesFromDb
  -> GameRepository.GetAll
  -> ObservableCollection&lt;DetectedGame&gt;
  -> UI binding
</pre>

## Flow G — Owned but uninstalled

<pre>
launcher owned catalog/cache
  -> DetectedGame(IsOwned=true, IsInstalled=false)
  -> SQLite
  -> UninstalledImageProcessor
  -> grayscale/branding artwork
  -> launcher/dummy launch target
  -> SteamSyncTag + Uninstalled tag
  -> Steam library
</pre>

## Debugging strategy

When something goes wrong, identify the last successful boundary.

Example:

- game detected but not in DB → inspect GameRepository;
- in DB but no artwork → inspect ArtworkManager / API key / cache;
- artwork exists but no Steam shortcut → inspect SteamInjectorService;
- shortcut written but Steam UI does not show it → inspect Steam process state / Steam cache / VDF path;
- wrong game title → inspect ExecutableMetadataReader / TitleSanitizer / SteamGridDB resolution;
- duplicate game → inspect detector identity and GameDetectionService title grouping.

## Checkpoint

Choose one feature and draw the entire chain from button click to filesystem side effect.
