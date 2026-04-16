using HarmonyLib;

namespace BoplMorePlayersLocal8.Patches;

[HarmonyPatch(typeof(GameSessionHandler), nameof(GameSessionHandler.LoadAbilitySelectScene))]
internal static class LoadAbilitySelectDiagnosticsPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RuntimeSnapshot.Log("GameSessionHandler.LoadAbilitySelectScene invoked.", verbose: false);
        RuntimeSnapshot.LogGlobalState("LoadAbilitySelectScene", verbose: true);
    }
}

[HarmonyPatch(typeof(GameSessionHandler), nameof(GameSessionHandler.LoadNextLevelScene))]
internal static class LoadNextLevelDiagnosticsPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RuntimeSnapshot.Log("GameSessionHandler.LoadNextLevelScene invoked.", verbose: false);
        RuntimeSnapshot.LogGlobalState("LoadNextLevelScene", verbose: true);
    }
}

[HarmonyPatch(typeof(CharacterSelectHandler), "TryStartGame_inner")]
internal static class CharacterSelectStartDiagnosticsPatch
{
    [HarmonyPrefix]
    private static void Prefix(CharacterSelectHandler __instance)
    {
        var boxes = Traverse.Create(__instance).Field<CharacterSelectBox[]>("characterSelectBoxes").Value;
        var readyState = (CharSelectMenu)2;
        var readyCount = boxes?.Count(box => box != null && Traverse.Create(box).Field<CharSelectMenu>("menuState").Value == readyState) ?? -1;
        RuntimeSnapshot.Log($"TryStartGame_inner: boxes={boxes?.Length ?? -1}, ready={readyCount}, startButton={Traverse.Create(typeof(CharacterSelectHandler)).Field<bool>("startButtonAvailable").Value}.", verbose: false);
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        RuntimeSnapshot.LogGlobalState("TryStartGame_inner.Postfix", verbose: true);
    }
}
