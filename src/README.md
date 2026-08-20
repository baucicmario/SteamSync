# src

**Purpose:** This folder serves as the root source directory for the SteamSync solution, organizing all production code into distinct architectural layers. It contains the business logic library and the desktop user interface application.

**Key Components:**
- `SteamSync.Core`: Class library project containing the game detection engine, Steam integration services, database storage, artwork management, and utility classes.
- `SteamSync.UI`: Avalonia UI desktop application project implementing the MVVM presentation layer, view models, and user settings interface.

**Dependencies:** Depends on external NuGet packages defined in the respective `.csproj` files, and is tested by test suites in the `tests` directory.
