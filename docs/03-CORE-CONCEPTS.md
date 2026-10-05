# 03 — Core concepts and data model

## 1. `IGameDetector`

File: [IGameDetector.cs](../src/SteamSync.Core/Detection/IGameDetector.cs)

Contract:

```csharp
string Name { get; }
string PlatformId { get; }
Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default);
```

The important idea is inversion of control at the detection layer:

- the orchestrator owns the collection of detectors;
- each detector owns source-specific knowledge;
- downstream code only sees `DetectedGame`.

## 2. `DetectedGame`

File: [DetectedGame.cs](../src/SteamSync.Core/Models/DetectedGame.cs)

This is the system's cross-module carrier.

### Persistence-related fields

- `Id`
- `Title`
- `Platform`
- `IsOwned`
- `IsInstalled`
- `ExePath`
- `StartDir`
- `LaunchArguments`
- `SteamAppId`
- `SteamGridDbId`
- `OfficialSteamAppId`
- `LastSynced`
- `ArtworkCached`
- `IsVR`

### UI-only / transient field

- `IsSelected`

The class inherits from CommunityToolkit MVVM's `ObservableObject`, which lets Avalonia bindings react to changes.

## 3. Two-state ownership/install model

The core distinction:

```
owned = true, installed = true
owned = true, installed = false
```

The second state is what powers the optional "Include Uninstalled" behavior.

A detector can therefore return an object with:

- ownership,
- platform,
- title,
- launch information if available.

The rest of the pipeline can later decide how to make that usable in Steam.

## 4. `SteamShortcut`

File: [SteamShortcut.cs](../src/SteamSync.Core/Models/SteamShortcut.cs)

This is the in-memory model of Steam's binary shortcut format.

Important fields:

- AppID
- AppName
- Exe
- StartDir
- Icon
- LaunchOptions
- OpenVR
- LastPlayTime
- Tags

The key special property is:

```csharp
IsManagedBySteamSync => Tags.Contains("SteamSync")
```

That property is what allows SteamSync to distinguish its entries from user-created shortcuts.

## 5. AppID identity

Files:
- [Crc32.cs](../src/SteamSync.Core/Utilities/Crc32.cs)
- [AppIdGenerator.cs](../src/SteamSync.Core/Steam/AppIdGenerator.cs)

The short AppID is deterministic:

```
crc = CRC32(exePath + appName)
shortAppId = crc | 0x80000000
```

The full 64-bit form adds Steam's non-Steam type marker:

```
fullAppId = (ulong)shortAppId << 32 | 0x02000000
```

Why this matters:

- the same path + title can reproduce the same identity;
- Steam's grid filenames can be derived consistently;
- existing entries can be matched.

## 6. Title normalization

Files:
- [ExecutableMetadataReader.cs](../src/SteamSync.Core/Utilities/ExecutableMetadataReader.cs)
- [TitleSanitizer.cs](../src/SteamSync.Core/Utilities/TitleSanitizer.cs)

Custom folder detection tries:

1. executable metadata `ProductName`
2. executable metadata `FileDescription`
3. sanitized folder name

Sanitization removes common repacker/scene/version noise.

This matters because the title is later used as a search term against SteamGridDB.

## 7. VR detection

File: [VrDetectionUtility.cs](../src/SteamSync.Core/Utilities/VrDetectionUtility.cs)

Order of evidence:

1. cache
2. launch arguments
3. title
4. executable/folder name
5. Steam Store API lookup

The result is written into `DetectedGame.IsVR`.

When later injected into Steam, VR state becomes `SteamShortcut.OpenVr = 1`.

## Checkpoint

Draw this dependency chain from memory:

```
source-specific evidence
    ↓
DetectedGame
    ↓
AppID / VR / Artwork enrichment
    ↓
SQLite
    ↓
SteamShortcut
    ↓
shortcuts.vdf
```
