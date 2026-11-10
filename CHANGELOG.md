# Changelog

## 2.0.0

Rewritten from scratch for Early Access.

### New

- New interface: borderless dark window, tiles and cards, one tab per area
- **Haystack radar** with top view, 3D view and 3D x-ray; the 3D camera fits itself to tall piles
- Dig a needle out, put the player on top of it, scale or restore the pile
- **Needles**, **Tech**, **Gear**, **Buildings**, **Missions**, **Career**, **Backups** and **Raw data** tabs
- Finds the Early Access save folder (`Godot\app_userdata\FindTheNeedle`) on its own, the Demo folder as fallback
- New app icon

### Fair play

- Saving sets `debug_used = true`, so edited runs stay off the leaderboards
- Career records are read-only; `board_*`, `debug_used` and `run_timed` are locked in the raw data view
