# SteamSync.UI

**Purpose:** This project provides the cross-platform desktop user interface for SteamSync, built with Avalonia UI following the MVVM pattern. It allows users to view detected games, configure SteamGridDB credentials, manage custom scan folders, and trigger synchronization or Steam restarts.

**Key Components:**
- `Views/`: Avalonia XAML views including `MainWindow.axaml`, `GameListView.axaml`, and `SettingsView.axaml`.
- `ViewModels/`: MVVM view models including `MainViewModel.cs`, `GameListViewModel.cs`, and `SettingsViewModel.cs` managing presentation state and user commands.
- `Program.cs` & `App.axaml`: Application entry point, styling configurations, Fluent theme setup, and resource bindings.
- `ViewLocator.cs`: View locator mapping view models to their corresponding XAML views.
- `Assets/`: Application icons and visual assets.

**Dependencies:** Heavily relies on `src/SteamSync.Core` for all backend operations, data access, and settings management; utilizes Avalonia UI framework packages and `CommunityToolkit.Mvvm`.
