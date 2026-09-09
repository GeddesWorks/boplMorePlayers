using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

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

[HarmonyPatch(typeof(CharSelectClickToJoin), "LateUpdate")]
internal static class CharSelectClickToJoinDiagnosticsPatch
{
    private static float _lastLogTime;

    [HarmonyPostfix]
    private static void Postfix(CharSelectClickToJoin __instance)
    {
        // Log once every 5 seconds to avoid spam
        if (Time.time - _lastLogTime < 5f)
        {
            return;
        }

        _lastLogTime = Time.time;

        var csb = __instance.csb;
        var menuState = Traverse.Create(csb).Field<CharSelectMenu>("menuState").Value;
        var rectIndex = csb.RectangleIndex;
        var activeIndex = -1;

        // Replicate CurrentlyActiveIndex logic
        var occupied = Traverse.Create(typeof(CharacterSelectBox)).Field<bool[]>("occupiedRectangles").Value;
        if (occupied != null)
        {
            var playableSlots = Mathf.Min(Plugin.TargetPlayerCount, Mathf.Max(0, occupied.Length - 1));
            for (var i = 0; i < playableSlots; i++)
            {
                if (!occupied[i])
                {
                    activeIndex = i;
                    break;
                }
            }
            if (activeIndex == -1)
            {
                activeIndex = occupied.Length - 1;
            }
        }

        var respondsToInput = activeIndex == rectIndex;
        var gamepadCount = Gamepad.all.Count;
        var assignedCount = 0;
        var deviceIds = Traverse.Create(typeof(CharacterSelectBox)).Field<int[]>("deviceIds").Value;
        if (deviceIds != null)
        {
            foreach (var id in deviceIds)
            {
                if (id != 0) assignedCount++;
            }
        }

        RuntimeSnapshot.Log(
            $"[ClickToJoin] box={rectIndex} menuState={menuState} activeIndex={activeIndex} responds={respondsToInput} " +
            $"gamepads={gamepadCount} assigned={assignedCount} occupied=[{string.Join(",", occupied?.Select(o => o ? "1" : "0") ?? Array.Empty<string>())}]",
            verbose: false);
    }
}
