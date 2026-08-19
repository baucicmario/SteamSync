# Game Launcher Installation Automation Requirements

## Overview
This document outlines the technical requirements, execution mechanisms, and required identifiers for programmatically triggering the installation process (or opening the installation page) for specific PC game clients. 

### Core Architecture Principle
When implementing the ID resolution and path detection logic for the following clients, the system should strictly utilize the existing open-source solutions found in **Playnite** and **BoilR** as references. Rather than implementing standalone parsing logic for local client databases, integrate and adapt their established mechanisms as closely as possible.

---

## 1. Epic Games Store - Tested Works
*   **Execution Method:** URI Protocol
*   **Protocol Scheme:** `com.epicgames.launcher://`
*   **Installation Command:** `com.epicgames.launcher://` (Fallback to launcher/library) OR `com.epicgames.launcher://store/p/{StoreSlug}` (Fallback to store page)
*   **Behavior:** Due to bugs in the new Epic Games Launcher installation dialog (which fails to show progress properly or doesn't open at all), Playnite abandoned the `?action=install` parameter. It is now recommended to simply open the Epic library view or route the user to the specific game's store page where they can initiate the install manually.
*   **Required Identifiers:**
    *   `StoreSlug` (e.g., `hogwarts-legacy`) if deep-linking to the product page. None needed if falling back to the library view.

## 2. GOG Galaxy - Tested Works
*   **Execution Method:** Command-line executable argument + URI Protocol
*   **Protocol Scheme:** `goggalaxy://`
*   **Installation Command (CLI):** `"<PathToGalaxy>\GalaxyClient.exe" /command=installGame /gameId={GameID}` followed by `goggalaxy://openGameView/{GameID}`
*   **Behavior:** According to Playnite, the CLI `/command=installGame` fails if the GOG Galaxy core components are not already initialized (detectable via lock files in `ProgramData\GOG.com\Galaxy\lock-files`). Therefore, the robust method is to wait for the client to run, execute the CLI install command, and then immediately invoke the `openGameView` URI so the UI correctly focuses on the installation.
*   **Required Identifiers:**
    *   `GameID` (Numeric string).
    *   Requires locating the Galaxy installation directory (typically via Registry keys).

## 3. Ubisoft Connect - Tested Works
*   **Execution Method:** URI Protocol
*   **Protocol Scheme:** `uplay://`
*   **Installation Command:** `uplay://install/{GameID}`
*   **Behavior:** Directly opens the Ubisoft Connect installation prompt without further navigation.
*   **Required Identifiers:**
    *   `GameID` (A specific numerical ID assigned to the Ubisoft title, e.g., 4 for Assassin's Creed 2).

## 4. EA App (Formerly Origin) - Tested Works
*   **Execution Method:** URI Protocol / CLI Executable
*   **Protocol Scheme:** `origin2://` (or `link2ea://`)
*   **Installation / Launch Command:** `origin2://game/launch/?offerIds={OfferID}` OR `"<PathToEA>\EALauncher.exe" "origin2://game/launch/?offerIds={OfferID}"`
*   **Behavior:** The EA App registers the `origin2://` protocol handler to `EALauncher.exe`. Calling `origin2://game/launch/?offerIds={OfferID}` for an uninstalled game opens the EA App and triggers the native dialog: *"Game not installed: Go to your library to double-check [Game Title] is installed properly... [GET THE GAME] [CLOSE]"*. Clicking **[GET THE GAME]** starts the installation / download prompt directly in the EA App.
*   **Required Identifiers:**
    *   `OfferID` (e.g., `70619` for Battlefield 3, `Origin.OFR.50.0002694`, or numeric Content ID).

## 5. Battle.net - Tested Works
*   **Execution Method:** Command-line executable argument
*   **Protocol Scheme:** N/A for installations (Playnite bypasses the URI for installations)
*   **Installation Command (CLI):** `"<PathToBattleNet>\Battle.net.exe" --game={GameUID}`
*   **Behavior:** According to Playnite's implementation, bypassing the `--install` flag and just using `--game={GameUID}` will effectively focus the Battle.net client on the specific game page, where the user can then install it. Playnite enforces that Battle.net must be installed and running.
*   **Required Identifiers:**
    *   `GameUID` (e.g., `odin` for Call of Duty: Modern Warfare, `WoW` for World of Warcraft).

## 6. Rockstar Games Launcher - Tested Works
*   **Execution Method:** Command-line executable argument
*   **Protocol Scheme:** N/A (Relies on executable flags)
*   **Installation Command:** `"<PathToRockstarLauncher>\Launcher.exe" -installTitle {TitleID}`
*   **Behavior:** Launches the Rockstar client and automatically navigates to the installation confirmation screen.
*   **Required Identifiers:**
    *   `TitleID` (e.g., `GTAV`, `RDR2`).
    *   Requires programmatic path resolution to the Rockstar Games Launcher executable.

## 7. Xbox / Windows Store - Tested Works
*   **Execution Method:** URI Protocol
*   **Protocol Scheme:** `ms-windows-store://` or `xbox-app://`
*   **Installation Command:** `ms-windows-store://pdp/?productid={ProductID}`
*   **Behavior:** Opens the Windows Store product page where the user can click install, or redirects into the Xbox App depending on system configuration. (Fully silent auto-installs are restricted by Windows UWP security policies without specialized app package management APIs).
*   **Required Identifiers:**
    *   `ProductID` (A 12-character alphanumeric code, e.g., `9NP1P1WFSV2S`).

---

## Appendix: Example Commands

### Epic Games Store
*   **Open Launcher / Library:** `com.epicgames.launcher://`
*   **Open Specific Game Page (Hogwarts Legacy):** `com.epicgames.launcher://store/p/hogwarts-legacy`

### GOG Galaxy
*   **Install & Focus Game (The Witcher 2) - Dynamic PowerShell Example:** 
    ```powershell
    $gogPath = Join-Path (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths").client "GalaxyClient.exe"; Start-Process $gogPath -ArgumentList "/command=installGame /gameId=1207658930"
    Start-Process "goggalaxy://openGameView/1207658930"
    ```

### Ubisoft Connect
*   **Install Game (Anno 1602):** `uplay://install/2990`

### EA App (Origin) - Tested Works
*   **Launch / Install Game (Apex Legends) - URI Example:** `origin2://game/launch?offerIds=Origin.OFR.50.0002694&autoDownload=1`
*   **Launch / Install Game (Apex Legends) - Dynamic PowerShell Example:**
    ```powershell
    $eaPath = (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop").LauncherAppPath; Start-Process $eaPath -ArgumentList "`"origin2://game/launch?offerIds=Origin.OFR.50.0002694&autoDownload=1`""
    ```

### Battle.net
*   **Focus Game Page (Call of Duty: Modern Warfare) - Dynamic PowerShell Example:**
    ```powershell
    $bnetPath = Join-Path (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net").InstallLocation "Battle.net.exe"; Start-Process $bnetPath -ArgumentList "--game=odin"
    ```

### Rockstar Games Launcher
*   **Install Game (GTA: San Andreas) - Dynamic PowerShell Example:**
    ```powershell
    $rsPath = Join-Path (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Rockstar Games\Launcher").InstallFolder "Launcher.exe"; Start-Process $rsPath -ArgumentList "-installTitle gta-sa"
    ```

### Xbox / Windows Store
*   **Open Store Page (Halo Infinite):** `ms-windows-store://pdp/?productid=9NP1P1WFSV2S`