# 13 — Design decisions and tradeoffs

This document deliberately separates evidence from speculation.

## 1. Common detector interface

**Confirmed:** All detectors implement IGameDetector.

**Practical benefit:** The orchestrator can execute heterogeneous data sources uniformly.

**Tradeoff:** Detection-specific complexity remains inside individual classes, some of which are very large.

## 2. Direct SQLite instead of EF Core

**Confirmed:** Microsoft.Data.Sqlite is used directly.

**Practical benefit:** small dependency surface and direct control over queries/schema.

**Tradeoff:** mapping, schema evolution and migrations are handwritten.

## 3. Title + Platform as database identity

**Confirmed:** GameRepository.Upsert uses Title + Platform.

**Benefit:** simple matching.

**Risk:** titles can change after SteamGridDB resolution and different sources can use different naming conventions.

This is a concrete interview discussion point.

## 4. Title-based cross-detector deduplication

**Confirmed:** GameDetectionService groups raw games by case-insensitive Title.

**Benefit:** easy cross-launcher merge.

**Risk:** two different games with identical titles can collide.

**Strong inference:** the project accepts some false-merge risk in exchange for simple normalization.

## 5. Manual dependency composition

**Confirmed:** MainViewModel constructs services directly.

**Benefit:** very little infrastructure for a small desktop app.

**Tradeoff:** construction logic and UI orchestration are tightly connected.

**Strong inference:** a larger version of the application would benefit from an explicit application/composition layer.

## 6. Custom binary VDF implementation

**Confirmed:** SteamSync has its own parser and writer.

**Benefit:** no dependency on Steam's internal API is required for shortcut persistence.

**Tradeoff:** binary-format compatibility becomes the application's responsibility.

## 7. Tags for ownership of managed records

**Confirmed:** SteamSync adds the SteamSync tag and uses it to identify records it controls.

**Benefit:** unrelated Steam shortcuts can be preserved.

**Tradeoff:** if tags are manually removed or corrupted, the application may no longer recognize its own previous entries.

## 8. Force-killing Steam

**Confirmed:** ForceSync uses ForceKillAndWaitAsync.

**Benefit:** eliminates the Steam process as a likely file-lock holder.

**Tradeoff:** aggressive process termination can be disruptive.

There is a graceful shutdown implementation too, but ForceSync intentionally chooses the aggressive route.

## 9. Offline-first launcher detection

**Confirmed:** detector implementations read local registry, manifests, caches, databases and files rather than requiring all launchers to be interactive.

**Benefit:** lower integration complexity and no login automation.

**Tradeoff:** local launcher formats can change, and owned-state reconstruction can depend on undocumented cache formats.

## 10. SteamGridDB title resolution

**Confirmed:** title resolution is used to improve artwork matching.

**Benefit:** detector-specific names can be normalized to official titles.

**Tradeoff:** the resolved title can change the generated deterministic AppID.

## 11. Uninstalled-game representation

**Confirmed:** uninstalled games use processed artwork plus launcher/dummy launch representations.

**Benefit:** ownership can remain visible in Steam.

**Tradeoff:** launch behavior is necessarily more indirect than for an installed executable.

## How to speak about rationale

Do not say:

> "The author chose X because..."

unless source comments or project documentation establish it.

Instead say:

> "The implementation chooses X. The practical advantage appears to be Y, while the tradeoff is Z."

That is technically safer in an interview.
