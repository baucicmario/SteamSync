# 18 — Glossary

| Term | Meaning in SteamSync |
|---|---|
| AppID | Steam shortcut identity generated for a Non-Steam game. |
| DetectedGame | Central model representing a game found by a detector. |
| Detector | Component implementing IGameDetector for one source. |
| IGameDetector | Common detector interface. |
| SteamShortcut | In-memory representation of a shortcuts.vdf entry. |
| VDF | Valve KeyValues format; Steam's shortcuts file is binary VDF. |
| SteamGridDB | External artwork/search API used for game artwork. |
| Grid | Steam's portrait/landscape library artwork stored under config/grid. |
| Hero | Wide Steam library banner artwork. |
| VR | Virtual Reality metadata represented by DetectedGame.IsVR and SteamShortcut.OpenVr. |
| Owned | The launcher source says the user has the game entitlement. |
| Installed | A usable local installation/executable was found. |
| Uninstalled game | Owned but not currently installed. |
| SteamSync tag | Tag marking a shortcut as managed by SteamSync. |
| Upsert | Insert a row if it doesn't exist, otherwise update it. |
| MVVM | Model-View-ViewModel UI architecture. |
| ViewModel | UI-facing state and commands. |
| Composition root | Place where application dependencies are constructed. In this repository it is effectively MainViewModel's constructor. |
| CRC32 | 32-bit checksum used as part of deterministic Steam shortcut AppID generation. |
| Playnite | External launcher-management ecosystem referenced by project documentation and some local-data compatibility logic; not currently embedded as an SDK/shim in the master source tree. |
| BoilR | External project used as a reference for Steam shortcut behavior and AppID/VDF/process lifecycle concepts. |
| Steam CDN | Steam-hosted image endpoints used as artwork fallbacks. |
| Steam user ID | Numeric directory under Steam userdata representing a local Steam profile. |
| Force Sync | Sync mode that force-kills Steam before writing and launches it again afterward. |
