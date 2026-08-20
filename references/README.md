# references

**Purpose:** This directory holds external reference codebases, submodules, and third-party tools that inform SteamSync's architecture and feature implementation. It acts as an internal reference library for reverse-engineered protocols, binary file formats, and platform integrations.

**Key Components:**
- `BoilR/`: An external Rust-based reference project utilized for its implementations of binary Steam `shortcuts.vdf` manipulation, CRC32 AppID generation algorithms, and SteamGridDB artwork workflows.

**Dependencies:** Provides reference algorithms and data structures adapted into native C# within `src/SteamSync.Core`.
