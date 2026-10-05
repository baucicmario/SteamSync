# SteamSync — Learning Handbook

> Generated from the repository source on 2026-10-05.
> Source branch analyzed: `master`
> Source commit analyzed: `500444a2751f38d34ea402fabea157a1b8a925d8`

## How to use this handbook

This is a **study course**, not just a reference manual. Read it in order the first time. Then use the Search Guide when you need to locate something while working in the code.

### Learning path

1. [00 — How to use the handbook](./00-README.md)
2. [01 — Big picture](./01-BIG-PICTURE.md)
3. [02 — Repository map](./02-REPOSITORY-MAP.md)
4. [03 — Core concepts and data model](./03-CORE-CONCEPTS.md)
5. [04 — Application startup](./04-APPLICATION-STARTUP.md)
6. [05 — Game detection](./05-DETECTION.md)
7. [06 — Persistence and SQLite](./06-DATA.md)
8. [07 — Artwork pipeline](./07-ARTWORK.md)
9. [08 — Steam injection and VDF](./08-STEAM-INJECTION.md)
10. [09 — UI / MVVM](./09-UI.md)
11. [10 — Important runtime flows](./10-IMPORTANT-FLOWS.md)
12. [11 — Testing](./11-TESTING.md)
13. [12 — Configuration, build, and operations](./12-CONFIGURATION.md)
14. [13 — Design decisions](./13-DESIGN-DECISIONS.md)
15. [14 — Pitfalls, discrepancies, and unknowns](./14-PITFALLS-AND-UNKNOWNs.md)
16. [15 — Interview guide](./15-INTERVIEW-GUIDE.md)
17. [16 — Search / debugging guide](./16-SEARCH-GUIDE.md)
18. [17 — File-by-file catalog](./17-FILE-BY-FILE.md)
19. [18 — Glossary](./18-GLOSSARY.md)

### What you should be able to explain after finishing

- What SteamSync does and why its two-project structure exists.
- Where the application starts.
- How `MainViewModel` manually wires the core services.
- How `GameDetectionService` turns many platform-specific scanners into one normalized list.
- How `DetectedGame` acts as the central cross-module data model.
- How detection results are deduplicated, enriched with VR/AppID/artwork state, and persisted.
- How artwork moves from SteamGridDB / Steam CDN into Steam's `config/grid` directory.
- How SteamSync writes binary `shortcuts.vdf` while preserving shortcuts that are not tagged `SteamSync`.
- How Force Sync changes the Steam process lifecycle.
- How Avalonia MVVM exposes all of this to the user.
- What the repository actually implements versus what older project-description text claims.

### Evidence convention

Throughout these documents:

- **Confirmed** = directly demonstrated by current source/config/test files.
- **Strong inference** = not explicitly stated, but supported by the implementation.
- **Unknown** = the repository does not provide enough evidence.

### Important scope note

The repository contains older prose that describes a planned or intended Playnite-extension architecture and a `publish/` directory. The current tree analyzed here does **not** contain a Playnite SDK shim or a `publish/` directory. Those discrepancies are documented in [14 — Pitfalls, discrepancies, and unknowns](./14-PITFALLS-AND-UNKNOWNs.md) and should be treated as interview talking points rather than facts about the current implementation.
