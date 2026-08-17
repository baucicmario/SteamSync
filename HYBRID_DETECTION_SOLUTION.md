# Hybrid Detection Solution

This document outlines the hybrid game detection strategy implemented in SteamSync, combining the speed of local scanning with the comprehensive cloud coverage provided by Playnite's platform integrations.

## Overview

We designed a **Hybrid (User-Selected)** approach that offers the best of both worlds. To avoid locking the user into a slow or failing path, there is no default detection mode. The application explicitly prompts the user to choose the desired detection method before beginning a sync operation.

## The Two Paths

### 1. Local Offline Detection
- **Description:** A fast, login-free method that reads local registries, filesystems, and databases.
- **Under the Hood:** Uses our lightweight C# detectors (e.g., `EpicGamesDetector`, `UbisoftDetector`, and custom directory scanners).
- **GOG Specifics:** We use the `GogDetector` which reads the local `galaxy-2.0.db` SQLite database, successfully identifying the user's full owned library without needing cloud credentials, making it a powerful offline solution.

### 2. Cloud / Web Detection (Playnite Engine)
- **Description:** A comprehensive cloud scraper that fetches the complete library (installed and uninstalled) from various platforms.
- **Under the Hood:** Leverages the `PlayniteExtensions` submodule plugins.
- **Architecture:** Because Playnite plugins target `.NET Framework 4.6.2` and rely heavily on WPF/XAML types (which conflict with our `.NET 9 Avalonia` app), we run them via an **out-of-process worker pattern** (`SteamSync.PlayniteWorker`). The worker instantiates the plugins natively, handles browser-based WebAuth (via `WebView2`), and communicates back to the main app via JSON over stdout using the `PlayniteWorkerClient`.

## User Interface & Flow

1. **The Choice Dialog (`DetectionModeDialog.axaml`)**
   When the user clicks the **Detect Games** button, the application suspends the sync operation and displays a modal dialog asking the user to choose between the **Local Offline** or **Cloud / Web** options.

2. **Orchestration (`GameDetectionService.cs`)**
   Based on the selection, the system configures the appropriate detectors:
   - For Local: Runs `ConfigureDefaults(settings)`.
   - For Cloud: Runs `ConfigurePlaynite(settings)`.

3. **Fallback Mechanism (`CloudFailureDialog.axaml`)**
   Cloud connections or plugin logic can fail (e.g., due to API changes or network drops). If the Cloud/Playnite sync fails, the system catches the exception and presents a fallback dialog, offering the user three options:
   - **Retry Cloud**
   - **Use Local Instead**
   - **Cancel**

## Architecture Recap

- **UI:** Avalonia Dialogs (`DetectionModeDialog`, `CloudFailureDialog`) injected into `GameListViewModel` to intercept the standard sync command.
- **Adapter:** `PlayniteWorkerClient` which invokes the appropriate `.NET Framework 4.6.2` `PlayniteWorker` executables, reading their JSON stdout to build the list of `DetectedGame` models.
- **Workers:** Standalone `.NET Fx 4.6.2` CLI applications wrapping specific Playnite SDK integrations (e.g., Epic, GOG, Battle.net, etc.), mimicking the Playnite host environment.
