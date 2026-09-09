# Bopl More Players Local 8

BepInEx + Harmony mod for local couch expansion to 8 players.

## Input Mirroring

If you want to test an 8-player local session with only one physical controller, enable `Input/MirrorPrimaryInputToExtraLocalPlayers` in the mod config. That mirrors the first local player's live input state onto the other local players during local/offline play.

## Diagnosing the Beam spin/anti-gravity bug (players 5-8)

[Issue #2](https://github.com/geddesworks/boplmoreplayers/issues/2) tracks a bug where a
player in slot 5+ using Beam ends up stuck spinning with no gravity (and other players
progressively lose platform collision). The Beam ability itself lives entirely in vanilla
`Assembly-CSharp.dll`, which this mod doesn't patch, so the likely cause is a vanilla
per-player array/state hard-sized for 4 players. To find the exact spot, this mod now logs
everything needed to `LogOutput.log` without anyone having to watch the console live:

- Any uncaught Unity exception/error (`DiagnosticsHooks`) — this should capture the exact
  stack trace from whatever vanilla code throws when a player index >= 4 hits it.
- A periodic Rigidbody2D snapshot per player slot (`PlayerPhysicsWatchdog`, every 2s, config
  `Diagnostics/EnablePhysicsWatchdog`) that flags any player with abnormal angular velocity or
  zero gravity scale — this should show exactly when/which slot enters the stuck state.

To collect a repro on Steam Deck: play until the bug happens, then close the game and grab
`LogOutput.log` from the BepInEx profile your mod manager is using (Thunderstore Mod
Manager/r2modman/Gale all expose a "browse profile folder" or "view logs" option) — no need to
read the console mid-match. Send that log back for analysis.

## Build

```powershell
dotnet build .\BoplMorePlayersLocal8.csproj -p:BoplBattleRootDir="C:\Program Files (x86)\Steam\steamapps\common\Bopl Battle" -p:BoplProfileDir="C:\Users\colli\AppData\Roaming\Thunderstore Mod Manager\DataFolder\BoplBattle\profiles\test my mods"
```

The build copies `BoplMorePlayersLocal8.dll` to:

```text
<Profile>\BepInEx\plugins\BoplMorePlayersLocal8\BoplMorePlayersLocal8.dll
```
