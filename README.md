# Bopl More Players Local 8

BepInEx + Harmony mod for local couch expansion to 8 players.

## Input Mirroring

If you want to test an 8-player local session with only one physical controller, enable `Input/MirrorPrimaryInputToExtraLocalPlayers` in the mod config. That mirrors the first local player's live input state onto the other local players during local/offline play.

## Build

```powershell
dotnet build .\BoplMorePlayersLocal8.csproj -p:BoplBattleRootDir="C:\Program Files (x86)\Steam\steamapps\common\Bopl Battle" -p:BoplProfileDir="C:\Users\colli\AppData\Roaming\Thunderstore Mod Manager\DataFolder\BoplBattle\profiles\test my mods"
```

The build copies `BoplMorePlayersLocal8.dll` to:

```text
<Profile>\BepInEx\plugins\BoplMorePlayersLocal8\BoplMorePlayersLocal8.dll
```
