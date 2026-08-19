# SteamSync

SteamSync detects games from third-party launchers and custom folders, downloads artwork from SteamGridDB, and syncs non-Steam shortcuts into Steam.

## Repository Layout

- `src/` - Production projects and application code.
  - `SteamSync.Core` - Detection, persistence, artwork, and Steam integration.
  - `SteamSync.PlayniteAdapter` - Playnite plugin integration.
  - `SteamSync.PlayniteWorker` - Isolated Playnite worker process.
  - `SteamSync.UI` - Avalonia desktop application.
- `tests/` - Automated tests for production code.
- `tools/` - Standalone build or integration tools.
- `samples/Manual/` - Manual test applications and local integration probes.
- `samples/Prototypes/` - Uncompiled experiments and temporary investigation programs.
- `docs/` - Project notes, launcher behavior documentation, and worker instructions.
- `extern/` - Git submodules and other external source dependencies.

## Building and Testing

Restore and build the solution:

```bash
dotnet restore SteamSync.sln
dotnet build SteamSync.sln
```

Run the automated tests:

```bash
dotnet test tests/SteamSync.Core.Tests/SteamSync.Core.Tests.csproj
```

The projects under `samples/Manual/` are intentionally not part of the main solution. Build or run them individually when investigating a specific launcher or algorithm.
