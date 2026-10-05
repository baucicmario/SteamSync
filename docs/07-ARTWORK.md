# 07 — Artwork pipeline

## Main classes

- src/SteamSync.Core/Artwork/SteamGridDbClient.cs
- src/SteamSync.Core/Artwork/ArtworkManager.cs
- src/SteamSync.Core/Artwork/UninstalledImageProcessor.cs

## SteamGridDbClient

SteamGridDbClient owns the HTTP mechanics:

- base URL: https://www.steamgriddb.com/api/v2
- Bearer authentication
- game search
- grids
- heroes
- logos
- icons
- image downloads

The client stays small so ArtworkManager can own the application-specific selection rules.

## Title resolution

ArtworkManager.ResolveGameTitleAsync takes a detector title and attempts to map it to an official SteamGridDB name.

It:

1. sanitizes the title for search;
2. searches SteamGridDB;
3. prefers exact matches;
4. retries some VR cases;
5. otherwise prefers verified results;
6. finally falls back to the first result.

This is important because artwork search quality depends heavily on title quality.

## Installed artwork flow

During sync:

<pre>
DetectedGame
  -> ArtworkManager.FetchAndSaveArtworkAsync
  -> SteamGridDbClient.SearchGamesAsync
  -> select image
  -> download bytes
  -> Steam user grid directory
</pre>

The expected Steam-style filenames include:

- {appid}p.png — portrait/grid
- {appid}.png — landscape
- {appid}_hero.png — hero
- {appid}_logo.png — logo
- {appid}_icon.png — icon

ArtworkManager can use Steam CDN fallback URLs for some image types.

## Cache detection

ArtworkManager.IsArtworkCached checks the user's Steam grid directory for files matching the expected AppID prefixes.

That state is copied into DetectedGame.ArtworkCached.

## Uninstalled artwork

UninstalledImageProcessor queries the local database for:

IsOwned = true and IsInstalled = false

It then:

1. finds artwork;
2. downloads it;
3. converts the image to grayscale;
4. overlays platform branding where appropriate;
5. stores processed images under %AppData%\SteamSync\Cache\UninstalledImages\;
6. prepares a launcher/dummy representation so the shortcut can still be useful.

## Why uninstalled games are harder

Installed games have a real executable path.

Uninstalled games do not.

SteamSync therefore separates:

- visual representation: processed artwork;
- launch representation: launcher-specific executable/arguments or generated dummy wrapper.

That is an important design point.

## Image processing

SixLabors.ImageSharp performs the transformation.

The processor deliberately filters out NSFW and humor-tagged source images when selecting candidates.

## Checkpoint

Pick a game and trace:

detector title → sanitized search title → SteamGridDB game → image URL → Steam grid file.

Then do the same for an uninstalled game and explain why the launch target differs.
