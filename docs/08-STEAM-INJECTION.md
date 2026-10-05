# 08 — Steam injection and binary VDF

## Main class

src/SteamSync.Core/Steam/SteamInjectorService.cs is the bridge from the normalized game model to Steam's own representation.

It receives DetectedGame objects and creates SteamShortcut objects.

## Normal sync flow

<pre>
GameListViewModel.PerformSync
  -> selected installed games
  -> save UI edits to SQLite
  -> find Steam users
  -> fetch artwork
  -> process uninstalled games if enabled
  -> SteamInjectorService.SyncAsync
  -> read shortcuts.vdf
  -> split user vs managed shortcuts
  -> build new managed shortcuts
  -> merge
  -> write shortcuts.vdf
</pre>

## Managed shortcut identity

SteamSync adds:

- SteamSync
- Installed or Uninstalled
- VR when relevant

SteamShortcut.IsManagedBySteamSync is true when the SteamSync tag exists.

## Non-destructive merge

The current merge policy is:

<pre>
existing shortcuts
  -> userShortcuts = NOT managed by SteamSync
  -> managedShortcuts = managed by SteamSync

new shortcuts =
  userShortcuts + regenerated managed shortcuts
</pre>

This is the mechanism intended to keep unrelated shortcuts intact.

## AppID generation

AppIdGenerator uses:

CRC32(exe path + title)

then sets the top bit to identify a non-Steam shortcut.

The full 64-bit Steam-style value adds the non-Steam type marker.

See:

- src/SteamSync.Core/Utilities/Crc32.cs
- src/SteamSync.Core/Steam/AppIdGenerator.cs

## VDF parser

ShortcutsVdfParser understands the binary format used by shortcuts.vdf.

Markers:

- 0x00 = object
- 0x01 = string
- 0x02 = 32-bit integer
- 0x08 = object terminator

The parser reads the root shortcuts object, each numbered shortcut object, fields, and nested tags.

## VDF writer

ShortcutsVdfWriter reverses the parser model.

It:

- writes the root shortcuts object;
- writes each shortcut object using its new order;
- writes fields;
- writes tags;
- writes terminators.

The parser/writer pair is tested with round-trip tests.

## Steam path resolution

SteamPathResolver:

1. checks Windows registry;
2. checks common fallback install paths;
3. finds userdata;
4. enumerates numeric Steam user IDs;
5. derives shortcuts.vdf and grid paths.

Important paths:

%SteamInstall%\userdata\<userId>\config\shortcuts.vdf

%SteamInstall%\userdata\<userId>\config\grid\

## Force Sync

SteamProcessManager has both graceful and aggressive shutdown support.

ForceSyncAsync currently uses the aggressive path:

1. detect Steam;
2. force-kill Steam processes;
3. write shortcut changes;
4. launch Steam again.

This reduces the chance that Steam has the VDF file locked while the writer changes it.

## Interview explanation

A concise explanation:

> SteamSync projects its normalized game model into Steam's Non-Steam shortcut format. It uses a deterministic AppID, parses and writes the binary shortcuts.vdf structure itself, tags its managed entries, preserves unrelated shortcuts, and can restart Steam when it needs to eliminate file-locking problems.

## Checkpoint

Whiteboard:

DetectedGame → SteamShortcut → binary VDF → Steam library entry.

Then explain where artwork fits into the same AppID identity.
