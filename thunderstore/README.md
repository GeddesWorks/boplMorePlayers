# FixedMoreBopl

Expand Bopl Battle local couch multiplayer from 4 to 8 players.

## Features

- 8 character select slots in a single row
- Automatic UI scaling to fit all players on screen
- Relaxed color uniqueness when all palette colors are taken
- Wider camera zoom for crowded matches
- Compressed ability-select spacing for 5+ players
- Draw-winner UI expanded to handle extra players
- Replay recording disabled to avoid 4-player packet crashes

## Configuration

All features can be toggled individually in the BepInEx config file (`BepInEx/config/com.geddesworks.fixedmorebopl.cfg`). Key settings:

- **TargetLocalPlayers** (4-8) — how many couch slots to enable
- **RepositionCharacterSelectBoxes** — compact multi-row layout
- **IncreaseCameraZoomForCrowdedMatches** — wider camera for 5+ players
- **RelaxColorUniquenessWhenFull** — allow duplicate colors once palette is exhausted

## Requirements

- BepInEx 5.4.21+
