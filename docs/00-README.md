# 00 — How to learn SteamSync

## Goal

Build a mental model before memorizing files.

### The order that matters

Think of SteamSync as this pipeline:

```
User clicks Detect
    ↓
GameListViewModel
    ↓
GameDetectionService
    ↓
platform/custom detectors
    ↓
DetectedGame objects
    ↓
dedupe + AppID + VR + artwork-cache enrichment
    ↓
GameRepository / SQLite

User clicks Sync
    ↓
GameListViewModel
    ↓
artwork pipeline
    ↓
SteamInjectorService
    ↓
SteamPathResolver
    ↓
ShortcutsVdfParser
    ↓
merge managed + user shortcuts
    ↓
ShortcutsVdfWriter
    ↓
Steam config/grid files

Force Sync adds:
SteamProcessManager → kill Steam → write → launch Steam
```

### The five concepts to keep in your head

1. **Detector** — finds games in one source.
2. **DetectedGame** — the common representation shared across the system.
3. **Repository** — stores and reloads that representation.
4. **Artwork / Steam integration** — turns metadata into Steam files.
5. **UI ViewModel** — orchestrates user-driven actions and exposes state to Avalonia.

### What not to do

Do not start by reading every detector line-by-line. First understand:

- the interfaces,
- the central model,
- the orchestrator,
- one complete flow,
- then the specialized implementations.

### First source files to open

- `src/SteamSync.UI/Program.cs`
- `src/SteamSync.UI/App.axaml.cs`
- `src/SteamSync.UI/ViewModels/MainViewModel.cs`
- `src/SteamSync.UI/ViewModels/GameListViewModel.cs`
- `src/SteamSync.Core/Detection/GameDetectionService.cs`
- `src/SteamSync.Core/Models/DetectedGame.cs`
- `src/SteamSync.Core/Steam/SteamInjectorService.cs`

### Checkpoint

Without looking at the code, explain this sentence:

> SteamSync does not make Steam the source of truth; it first builds a normalized game library and only then projects selected games into Steam's shortcut/artwork format.
