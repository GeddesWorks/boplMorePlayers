using HarmonyLib;
using UnityEngine;

namespace BoplMorePlayersLocal8;

internal static class RuntimeSnapshot
{
    internal static void Log(string message, bool verbose)
    {
        if (!Plugin.EnableDiagnostics.Value)
        {
            return;
        }

        if (verbose && !Plugin.VerboseDiagnostics.Value)
        {
            return;
        }

        Plugin.Log.LogInfo($"[Diag] {message}");
    }

    internal static void LogGlobalState(string context, bool verbose)
    {
        if (!Plugin.EnableDiagnostics.Value)
        {
            return;
        }

        var players = PlayerHandler.Get()?.PlayerList();
        var playerCount = players?.Count ?? -1;
        var localPlayers = players?.Count(p => p.IsLocalPlayer) ?? -1;
        var alivePlayers = players?.Count(p => p.IsAlive) ?? -1;

        var occupied = Traverse.Create(typeof(CharacterSelectBox)).Field<bool[]>("occupiedRectangles").Value;
        var devices = Traverse.Create(typeof(CharacterSelectBox)).Field<int[]>("deviceIds").Value;

        var charSelectHandler = UnityEngine.Object.FindObjectOfType<CharacterSelectHandler>();
        var charBoxes = charSelectHandler == null
            ? null
            : Traverse.Create(charSelectHandler).Field<CharacterSelectBox[]>("characterSelectBoxes").Value;

        var gameSessionHandler = UnityEngine.Object.FindObjectOfType<GameSessionHandler>();
        var slimeControllers = gameSessionHandler == null
            ? null
            : Traverse.Create(gameSessionHandler).Field<SlimeController[]>("slimeControllers").Value;

        Log(
            $"{context}: players={playerCount}, localPlayers={localPlayers}, alivePlayers={alivePlayers}, " +
            $"occupiedSlots={FormatBoolArray(occupied)}, deviceSlots={FormatIntArray(devices)}, " +
            $"charBoxes={charBoxes?.Length ?? -1}, slimeControllers={slimeControllers?.Length ?? -1}, " +
            $"targetPlayers={Plugin.TargetPlayerCount}, slotArrayLength={Plugin.SlotArrayLength}",
            verbose);
    }

    private static string FormatBoolArray(bool[]? values)
    {
        if (values == null)
        {
            return "null";
        }

        return $"len={values.Length} [{string.Join(",", values.Select(v => v ? 1 : 0))}]";
    }

    private static string FormatIntArray(int[]? values)
    {
        if (values == null)
        {
            return "null";
        }

        return $"len={values.Length} [{string.Join(",", values)}]";
    }
}
