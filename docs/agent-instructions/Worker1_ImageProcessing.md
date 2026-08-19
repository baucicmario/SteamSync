# Worker 1: Uninstalled Games Image Processing

## Objective
Implement an image processing pipeline in SteamSync for games that are detected as owned but **not installed**. You must grab their artwork, modify it to clearly indicate it is uninstalled (e.g., grayscale it and overlay a small launcher logo), and save it to the image cache folder.

## Context & Requirements
- **Database:** SteamSync stores games in an SQLite database (`steamsync.db`), table `Games`. You are targeting rows where `IsOwned = 1` and `IsInstalled = 0`.
- **Image Processing:** Use a library like `ImageSharp` or `System.Drawing.Common`.
- **Modifications Required:**
  1. Convert the game cover art to grayscale.
  2. Overlay a small icon/logo of the game's respective platform (e.g., Epic, GOG, Steam) in the corner.
  3. (Optional) Add a "Not Installed" or "Download" badge.
- **Output:** Because these games are not yet added to Steam, their Steam AppIDs are unknown. Do NOT save these to the official Steam image cache folder. Instead, save the modified images into a dedicated internal directory within the SteamSync data folder using a clean structure (e.g., `SteamSync\Cache\UninstalledImages\{Platform}\{GameID}_cover.jpg`). Workers 2 through 8 will retrieve the images from this internal library when they generate their shortcuts.
