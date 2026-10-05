# 11 — Testing

## Test project

tests/SteamSync.Core.Tests is an xUnit project targeting the same Windows/.NET environment.

It references SteamSync.Core directly.

There is currently no separate UI test project in the repository tree.

## Test groups

### Detection

- BattleNetDetectorTests
- EaAppDetectorTests
- EpicGamesDetectorTests
- GogDetectorTests
- RockstarDetectorTests
- UbisoftDetectorTests
- XboxDetectorTests
- GameDetectionServiceTests

These validate source-specific detection and orchestration behavior.

### Steam

- AppIdGeneratorTests
- ShortcutsVdfTests
- SteamInjectorServiceTests
- executable-generator tests

### Utilities

- TitleSanitizerTests

### Artwork / settings

- ArtworkManagerTests
- AppSettingsTests

## Detection orchestration test

GameDetectionServiceTests uses a MockDetector implementing IGameDetector.

That is important architecturally: the detector interface makes the orchestrator testable without requiring a real launcher installation.

The tests verify:

- IncludeUninstalledGames behavior;
- filtering of uninstalled records;
- multiple detectors returning safely.

## VDF round-trip test

ShortcutsVdfTests serializes a SteamShortcut with ShortcutsVdfWriter, then parses it again.

It asserts important fields survive the round trip.

That tests the custom binary-format implementation at the pair level.

## Tag test

SteamInjectorServiceTests verifies:

- SteamSync tag constants;
- managed/unmanaged recognition;
- Installed/Uninstalled/VR tags survive VDF serialization.

## Title sanitizer test

TitleSanitizerTests provides concrete examples such as:

- FitGirl tag removal;
- scene group suffix removal;
- version stripping;
- architecture suffix removal;
- whitespace normalization.

These are excellent examples for understanding intended behavior.

## What is not covered strongly

From the visible test tree, there is less evidence of:

- UI interaction tests;
- full end-to-end Steam installation tests;
- live launcher integration tests;
- real Steam process lifecycle tests;
- production SteamGridDB API tests.

That is normal for a unit-focused project, but it is worth knowing for an interview.

## Test command

The repository README gives:

dotnet test tests/SteamSync.Core.Tests/SteamSync.Core.Tests.csproj

## How to learn from tests

Tests are often the shortest specification of edge cases.

Recommended order:

1. GameDetectionServiceTests
2. AppIdGeneratorTests
3. ShortcutsVdfTests
4. SteamInjectorServiceTests
5. TitleSanitizerTests
6. individual detector tests

## Checkpoint

Pick a test and answer:

- what production behavior is being specified?
- what dependency is being replaced?
- what would break if the implementation changed?
