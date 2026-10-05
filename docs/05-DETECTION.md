# 05 — Game detection

## The orchestrator

src/SteamSync.Core/Detection/GameDetectionService.cs owns the detector collection and the post-processing of raw results.

Its responsibilities are:

1. configure the detector list from AppSettings;
2. execute detectors;
3. merge and deduplicate raw results;
4. recover information from existing Steam shortcuts;
5. assign AppIDs;
6. detect VR;
7. determine artwork-cache state.

## Runtime sequence

<pre>
GameListViewModel
  -> ReadSettings
  -> ConfigureDefaults
  -> DetectAllGamesAsync
  -> each IGameDetector.DetectGamesAsync
  -> merge results
  -> deduplicate
  -> AppID / VR / artwork enrichment
  -> return List&lt;DetectedGame&gt;
</pre>

## Detector contract

src/SteamSync.Core/Detection/IGameDetector.cs exposes:

- Name
- PlatformId
- DetectGamesAsync

Every detector therefore returns the same central model: DetectedGame.

## Detector set

ConfigureDefaults can add:

- EpicGamesDetector
- GogDetector
- UbisoftDetector
- EaAppDetector
- BattleNetDetector
- RockstarDetector
- XboxDetector
- CustomFolderScanner

The settings object controls which are enabled.

## Error isolation

Each detector runs inside a try/catch in the orchestrator. A failure in one launcher source is logged and scanning continues with the remaining detectors.

That is an important resilience boundary.

## Deduplication

The current implementation groups by Title using a case-insensitive comparison.

For each title group, it prefers:

1. installed record;
2. record with a non-null executable path.

It then merges IsOwned and IsInstalled across the group.

This is a practical solution, but it means title is being used as an identity key at this stage.

## Existing Steam shortcuts as enrichment

After raw detection, GameDetectionService reads existing shortcuts.vdf files through ShortcutsVdfParser.

Two lookup maps are built:

- title → SteamShortcut
- executable path → SteamShortcut

Those lookups can recover an existing AppID and previous VR configuration.

## AppID enrichment

Installed games with an executable get a deterministic AppID from:

src/SteamSync.Core/Steam/AppIdGenerator.cs

The input is the executable path plus the title.

## VR enrichment

If an existing shortcut already has OpenVR enabled, that is preserved.

Otherwise VrDetectionUtility runs its heuristic and Steam Store API checks.

## Artwork-cache enrichment

The service checks the Steam grid directory via ArtworkManager.IsArtworkCached.

This means the detection result already knows whether artwork is likely present before sync begins.

# Platform detector guide

## EpicGamesDetector

Installed games come from Epic .item manifests under ProgramData.

Owned-but-uninstalled games can come from Epic's catalog cache when IncludeUninstalledGames is true.

The detector filters out non-game categories such as engines, plugins, assets, DLC, and promotional items.

## GogDetector

Installed games come from GOG registry keys.

Owned games can also come from the local GOG Galaxy SQLite database.

This is an example of merging an installed source with an owned source.

## UbisoftDetector

Installed games come from Ubisoft registry keys.

Owned/uninstalled games come from Ubisoft's local configuration cache, which is protobuf-serialized.

## EaAppDetector

EA detection is implemented as an offline local-data integration using EA registry/manifests/configuration.

EaContentIdResolver translates EA offer/content IDs and can also consume Playnite legacy-offers.json data if that file exists on the user's machine.

That does not mean the current repository embeds the Playnite SDK.

## BattleNetDetector

Battle.net combines several local sources: registry, product data, protobuf data, local cache and license fragments.

The implementation can reconstruct installed and owned-but-uninstalled games.

## RockstarDetector

Rockstar detection combines:

- Windows uninstall and launcher registry data
- build collection / manifest XML files
- launcher logs
- profile/cache files

It also includes a table of known Rockstar title IDs and preferred executables.

## XboxDetector

Xbox / Windows Store detection is focused on installed games. The repository README explicitly describes the integration as installed-only.

## CustomFolderScanner

CustomFolderScanner is the detector to study when learning the project's heuristic logic.

It:

1. scans configured roots;
2. looks one level down for candidate game directories;
3. recursively looks for executables to a bounded depth;
4. filters installers, crash handlers, engine tools and launchers;
5. prefers executable metadata;
6. falls back to a sanitized folder name.

The shared IsBlacklistedExecutable helper is reused by other detectors when choosing likely game executables.

## Checkpoints

Be able to explain:

- why IGameDetector exists;
- why detector errors are isolated;
- why all detectors return DetectedGame;
- why title deduplication happens centrally;
- which sources provide installed state and which can provide owned state.
