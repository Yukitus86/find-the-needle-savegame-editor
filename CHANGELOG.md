# Changelog

## 2.1.0

### New

- **Depot** tab: the shared depot stock with every good, −10 / +10 / +100, fill all up to any amount, empty the depot, depots on the yard and their acceptance rules (limits, refused goods)
- **English and German** interface, English by default; switch with EN / DE in the title bar (the loaded save and unsaved edits stay)
- Tech catalog grown from about 100 to 160+ cards (smelter, fabricator, assembler, depot, gas plant, blueprint designer, jetpack, …) in 10 branches
- New building names (depot, smelter, digester, pulper, needle radar, water main, …) and the "Mountain" pile size

### Changed

- **Tech** tab redesigned: branch list with progress on the left, compact two-column rows, unlock switches for one-step cards, filter all / open / done
- `--dump <save> <out.txt>` developer aid

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
