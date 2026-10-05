# 14 — Pitfalls, discrepancies, and unknowns

## 1. Project description says Playnite SDK shim; current source does not show one

The file project description.txt describes a Playnite API shim and official Playnite extension reuse.

The current repository tree does not contain:

- a Playnite SDK package reference;
- an IPlayniteAPI implementation;
- a PlayniteExtensions submodule;
- Playnite plugin classes instantiated by SteamSync.

There are still references to Playnite in EaContentIdResolver and comments/interface documentation.

### Interview-safe wording

> The project description describes a planned/desired Playnite-based architecture, but the current master source implements direct launcher-specific detectors. Playnite is only consulted indirectly in a few places such as legacy EA mapping when local Playnite data exists.

Do not claim the current code runs Playnite plugins unless the repository changes.

## 2. README references publish/

The README describes a publish/ directory.

The current master tree analyzed does not contain it.

Treat that as stale documentation.

## 3. No CI workflow visible

No .github/workflows files are present in the current tree.

Do not claim GitHub Actions CI exists for this repository version.

## 4. Title is used as identity in multiple places

Detection deduplication groups by title.

Database upsert uses Title + Platform.

That can create collisions or identity churn.

## 5. API-driven title changes can affect AppID

AppID is based on executable path + title.

If title resolution changes the title, the AppID changes too.

This is an important subtlety because a "better" title can result in a different Steam shortcut identity.

## 6. Manual DI makes MainViewModel a composition hotspot

MainViewModel knows about many core implementation classes.

That is easy to understand in a small application, but it is a coupling point.

## 7. GameListViewModel is a large orchestration class

Detection, title resolution, database persistence, artwork, uninstalled processing, Steam syncing, sorting and UI state are all coordinated there.

This is useful to know when explaining maintainability.

## 8. Detector implementations are intentionally platform-specific

Some detectors parse registry keys, some SQLite, some protobuf, some JSON/XML/cache files.

That means launcher upgrades can break individual integrations independently.

The interface limits blast radius, but not source-format maintenance.

## 9. VR detection has an online fallback

VrDetectionUtility can call the Steam Store API.

Therefore "offline detection" is not true for every stage of the whole pipeline.

The launcher detection itself is mostly local, but VR enrichment may use the network.

## 10. API key handling

The current code stores the SteamGridDB API key in settings.json.

That is convenient but weaker than a dedicated secret store.

## 11. Database migrations are minimal

The schema uses CREATE TABLE and ALTER TABLE attempts rather than a migration framework.

This is fine for a small local app but is worth discussing if asked about long-term schema evolution.

## 12. Unknowns

From the repository alone, do not claim:

- production user counts;
- performance under very large libraries;
- exact author intent for every design;
- real-world launcher compatibility across all versions;
- security guarantees beyond what the code demonstrates.

## Checkpoint

Practice explaining one weakness without insulting the project:

> "For the project's scale, this is a simple solution. If I had to scale it, I would revisit X because Y."
