# Bopl More Players Local 8

BepInEx + Harmony mod for local couch expansion to 8 players.

## Build

```powershell
dotnet build .\BoplMorePlayersLocal8.csproj -p:BoplBattleRootDir="C:\Program Files (x86)\Steam\steamapps\common\Bopl Battle" -p:BoplProfileDir="C:\Users\colli\AppData\Roaming\Thunderstore Mod Manager\DataFolder\BoplBattle\profiles\test my mods"
```

The build copies `BoplMorePlayersLocal8.dll` to:

```text
<Profile>\BepInEx\plugins\BoplMorePlayersLocal8\BoplMorePlayersLocal8.dll
```
