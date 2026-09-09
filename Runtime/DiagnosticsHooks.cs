using UnityEngine;

namespace BoplMorePlayersLocal8;

/// <summary>
/// Beam/respawn physics for players 5-8 live entirely in vanilla Assembly-CSharp code this mod
/// doesn't patch. Unity normally routes uncaught exceptions from MonoBehaviour callbacks through
/// Application.logMessageReceived, so subscribing here surfaces the exact stack trace into
/// LogOutput.log without needing to know which internal method/array is at fault ahead of time.
/// </summary>
internal static class DiagnosticsHooks
{
    internal static void Subscribe()
    {
        Application.logMessageReceived += OnUnityLog;
    }

    internal static void Unsubscribe()
    {
        Application.logMessageReceived -= OnUnityLog;
    }

    private static void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        if (!Plugin.EnableDiagnostics.Value)
        {
            return;
        }

        if (type != LogType.Exception && type != LogType.Error)
        {
            return;
        }

        Plugin.Log.LogError($"[UnityLog:{type}] {condition}\n{stackTrace}");
    }
}
