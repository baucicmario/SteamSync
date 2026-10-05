# 12 — Configuration, build, and operations

## Application settings

File: src/SteamSync.Core/Models/AppSettings.cs

Persistent settings live at:

%AppData%\SteamSync\settings.json

Settings include:

- SteamGridDB API key
- IncludeUninstalledGames
- detector enable/disable flags
- custom scan directories
- optional SteamInstallPath property

## Runtime state files

SteamSync also uses:

- %AppData%\SteamSync\steamsync.db
- %AppData%\SteamSync\steamsync.log
- %AppData%\SteamSync\Cache\UninstalledImages\
- %AppData%\SteamSync\vr_cache.json

The settings and database paths are created through AppSettings helpers.

## Secrets

The SteamGridDB API key is stored in settings.json by the current implementation.

Do not commit a real API key.

For an interview, accurately say:

> The application currently persists the SteamGridDB bearer token in its local settings JSON.

Do not claim that it uses a dedicated OS secret store unless the code changes.

## Build

The solution contains:

- SteamSync.Core
- SteamSync.UI
- SteamSync.Core.Tests

The README documents:

dotnet build SteamSync.sln -c Release

and:

dotnet run --project src/SteamSync.UI/SteamSync.UI.csproj -c Release

## Package dependencies

Core includes:

- Microsoft.Data.Sqlite
- protobuf-net
- System.Text.Json
- CommunityToolkit.Mvvm
- YamlDotNet
- SixLabors.ImageSharp
- SixLabors.ImageSharp.Drawing
- Microsoft.CodeAnalysis.CSharp

UI includes:

- Avalonia
- Avalonia DataGrid
- Avalonia Desktop
- Avalonia Fluent theme
- Avalonia Inter fonts
- CommunityToolkit.Mvvm
- diagnostics support

## Solution-wide build target

Directory.Build.targets defines a SixLabors_ValidateLicense target with an empty body.

It is not an application runtime component.

## CI/CD

The current repository tree contains no GitHub Actions workflow files.

Do not tell an interviewer that this repository has a CI pipeline unless you can point to another branch/repository configuration.

## Deployment

The current master tree does not contain the publish/ directory referenced by README prose.

The source does contain an executable Windows desktop project configured as WinExe.

## Platform

Both core and UI target net9.0-windows.

The repository is therefore specifically Windows-oriented despite Avalonia itself being cross-platform.

## Checkpoint

Explain:

- where configuration is stored;
- where the database is stored;
- where logs are stored;
- where uninstalled artwork cache is stored;
- how to build;
- how to run tests;
- what is missing from CI/CD.
