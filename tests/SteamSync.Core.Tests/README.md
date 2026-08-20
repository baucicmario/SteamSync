# SteamSync.Core.Tests

**Purpose:** This project contains unit and integration tests for the `SteamSync.Core` class library. It ensures the accuracy of game detection across various storefronts, Steam shortcut generation, binary VDF parsing/serialization, and title sanitization routines.

**Key Components:**
- `*DetectorTests.cs`: Unit tests for storefront detection logic (Battle.net, EA App, Epic Games, GOG, Rockstar, Ubisoft, Xbox) and overall `GameDetectionService`.
- `ShortcutsVdfTests.cs`: Tests for binary `shortcuts.vdf` deserialization and serialization.
- `AppIdGeneratorTests.cs`: Tests validating 64-bit Non-Steam AppID generation against Steam and BoilR standards.
- `*ExecutableGeneratorTests.cs`: Tests verifying the generation of launcher URI executable wrappers.
- `ArtworkManagerTests.cs`: Tests for artwork fetching, caching, and SteamGridDB response handling.
- `TitleSanitizerTests.cs`: Tests for regex-based cleaning of scene group tags, version numbers, and folder names.
- `SteamInjectorServiceTests.cs` & `AppSettingsTests.cs`: Tests for Steam shortcut injection workflows and configuration management.

**Dependencies:** Directly references `src/SteamSync.Core` and utilizes `xunit`, `Microsoft.NET.Test.Sdk`, and `coverlet.collector`.
