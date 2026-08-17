# Done So Far

This checklist tracks the implementation progress of the Hybrid Detection System based on the original plan.

### UI Layer — Detection Mode Dialog
- [x] **[NEW] `DetectionModeDialog.axaml`**
- [x] **[NEW] `DetectionModeDialog.axaml.cs`**
  *(Created the modal Avalonia dialog for choosing between Local Offline and Cloud/Web modes.)*
- [x] **[NEW] `CloudFailureDialog.axaml`**
- [x] **[NEW] `CloudFailureDialog.axaml.cs`**
  *(Created the fallback/retry dialog for when Cloud detection fails.)*

### Core Layer — Detection Service
- [x] **[MODIFY] `GameListViewModel.cs`**
  *(Updated `DetectGamesAsync()` to use the new dialog, and handle Cloud failure with retry/fallback.)*
- [x] **[MODIFY] `GameDetectionService.cs`**
  *(Added `ConfigurePlaynite(AppSettings settings)` to register only the `PlayniteWorkerClient`.)*
- [x] **[NEW] `DetectionMode.cs`**
  *(Created the enum for `Local` and `Cloud`.)*

### PlayniteWorker — The .NET Framework 4.6.2 Out-of-Process Worker
- [x] **[NEW] `tools/SteamSync.PlayniteWorker/SteamSync.PlayniteWorker.csproj`**
  *(Created the .NET Fx 4.6.2 project and linked Playnite submodule source files.)*
- [ ] **[NEW] `tools/SteamSync.PlayniteWorker/Program.cs`**
  *(Main entry point for parsing arguments, creating the mock API, instantiating the plugin, and outputting JSON.)*
- [~] **[NEW] `tools/SteamSync.PlayniteWorker/MockPlayniteApi/`**
  *(Copied `SteamSyncPlayniteAPI.cs` and `AuthWebView.cs`, but `MockGameDatabaseAPI.cs` needs to be implemented since it's referenced but undefined.)*

### Existing PlayniteAdapter — Cleanup
- [ ] **[MODIFY] `SteamSync.PlayniteAdapter.csproj`**
  *(Pending any necessary cleanup or marking as fallback.)*

### Settings
- [ ] **[MODIFY] `AppSettings.cs`**
  *(Ensure `PlayniteWorkerPath` resolves correctly relative to the app directory.)*

## Implementation Order Progress

| Phase | Task | Status |
|-------|------|--------|
| **1** | Create `DetectionMode` enum + dialog AXAML/code-behind | ✅ Done |
| **2** | Wire dialog into `GameListViewModel.DetectGamesAsync()` with fallback/retry | ✅ Done |
| **3** | Add `ConfigurePlaynite()` to `GameDetectionService` | ✅ Done |
| **4** | Build the `SteamSync.PlayniteWorker` .NET Fx 4.6.2 console app | 🚧 In Progress |
| **5** | Integrate Playnite submodule source files into the worker | ✅ Done (Linked via csproj) |
| **6** | Test end-to-end: Local mode, Cloud mode, failure → fallback | ❌ Pending |

---

**Where we stopped:**
We successfully scaffolded the UI changes, view model logic, and the core detection service updates. We also created the `SteamSync.PlayniteWorker.csproj` and copied over the initial mock API files. We are currently stopped at building the worker's `Program.cs` and implementing the missing `MockGameDatabaseAPI.cs`.
