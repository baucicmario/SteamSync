

1. PROJECT OVERVIEW
SteamSync detects both installed and owned games across multiple third-party launchers (Epic Games, GOG Galaxy, Ubisoft Connect, EA App, Battle.net) as well as custom user directories (standalone, DRM-free, or pirated games). It fetches official artwork from SteamGridDB and injects them into the local Steam client as Non-Steam shortcuts.

2. CORE REUSE PHILOSOPHY
PLAYNITE (Submodule + SDK Shim): Do not rewrite official storefront launcher detection. Use a Git Submodule pointing to the official PlayniteExtensions repository. Install the PlayniteSDK via NuGet and implement an IPlayniteAPI "Shim." SteamSync will instantiate official Playnite library plugins unmodified, passing the mock SDK API. This allows instant updates to broken launcher integrations via git submodule update --remote without running the heavy Playnite UI.

BOILR (Rust Reference translated to C#): Adapt the Steam injection workflow from BoilR (https://github.com/PhilipK/BoilR). Implement BoilR's exact CRC32 AppID generation, binary shortcuts.vdf manipulation, Steam process kill/restart lifecycle, and SteamGridDB artwork folder structure in native C#.

3. CORE MODULES
Module A: Game Detector (Hybrid Architecture)

Playnite API Shim: A mock IPlayniteAPI container that satisfies the constructor requirements of Playnite's LibraryPlugin classes.

Storefront Integrations: Directly execute the GetGames() methods from the submoduled Playnite plugins to fetch Epic, GOG, Ubisoft, EA, and Battle.net data.

Smart Custom Folder Scanner (Native C#): A highly accurate heuristic scanner for standalone games bypassing Playnite's native folder logic.

Expanded Blacklist: Filter out known garbage (unins000.exe, dxsetup.exe), engine handlers (unitycrashhandler.exe), and scene/crack tools (steamclient_loader.exe).

Metadata Extraction: Read embedded Windows executable metadata (FileVersionInfo.ProductName and FileDescription) to identify the true game name (e.g., extracting "Cyberpunk 2077" directly from the .exe).

Fallback Sanitization: If metadata is missing, use the parent folder name but apply strict Regex sanitization to strip out repacker tags (e.g., [FitGirl]), scene groups (e.g., -RUNE, -CODEX), version numbers, and replace periods/underscores with spaces to guarantee accurate SteamGridDB fuzzy matching.

State Management: Distinctly track two independent Boolean flags for every title: IsOwned and IsInstalled.

Module B: Data Storage (SQLite)

Maintain a local SQLite database tracking: Id, Title, Platform, IsOwned, IsInstalled, ExePath, LaunchArguments, SteamAppId, and LastSynced.

Module C: Steam Injector & Lifecycle Manager

AppID Calculation: Implement 64-bit Non-Steam AppID generation matching Steam/BoilR specifications (CRC32 of target path + app name with specific bitwise flags).

VDF Manipulation: Read and write Steam's binary shortcuts.vdf format located in userdata/<user_id>/config/.

Process Safety & Force Sync: When Force Sync is triggered and Steam is running, gracefully terminate the Steam process, apply VDF/artwork changes, and relaunch Steam.

Uninstallation Sync: Automatically remove shortcuts and grid images from Steam for games no longer marked as installed in the database.

Module D: Artwork Manager (SteamGridDB)

Fuzzy Matching: Pass the cleaned titles from Module A to the SteamGridDB API to resolve the correct official game entity.

Asset Download: Download cover grids, heroes, logos, and icons.

Injection: Save assets directly to userdata/<user_id>/config/grid/ using official Steam naming schemes (<appid>p.png, <appid>_hero.png, <appid>_logo.png, <appid>_icon.png).

4. USER INTERFACE (Avalonia UI or WPF)
Clean, modern dark UI architecture.

Main View: Quick "Sync" and "Force Sync (Restart Steam)" actions with progress indicators and a datagrid/list of detected games.

Settings View: SteamGridDB API key configuration, launcher authentication triggers (passing auth states to the Playnite plugins), and a directory picker for defining custom scan paths.