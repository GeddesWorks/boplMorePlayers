using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BoplMorePlayersLocal8;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.collin.boplmoreplayerslocal8";
    public const string PluginName = "Bopl More Players Local 8";
    public const string PluginVersion = "0.1.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<int> TargetLocalPlayers = null!;
    internal static ConfigEntry<bool> EnableDiagnostics = null!;
    internal static ConfigEntry<bool> VerboseDiagnostics = null!;
    internal static ConfigEntry<bool> DisableReplayRecording = null!;
    internal static ConfigEntry<bool> RepositionCharacterSelectBoxes = null!;
    internal static ConfigEntry<bool> CompressMidRoundSelectorSpacing = null!;
    internal static ConfigEntry<bool> IncreaseCameraZoomForCrowdedMatches = null!;
    internal static ConfigEntry<bool> RelaxColorUniquenessWhenFull = null!;
    internal static ConfigEntry<bool> ExpandDrawWinnerUiSlots = null!;

    internal static int TargetPlayerCount => Mathf.Clamp(TargetLocalPlayers.Value, 4, 8);
    internal static int SlotArrayLength => TargetPlayerCount + 1;

    private Harmony? _harmony;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        BindConfig();
        ApplyEarlyRuntimeOverrides();

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(Assembly.GetExecutingAssembly());

        var patchedMethods = _harmony.GetPatchedMethods()
            .Where(method => Harmony.GetPatchInfo(method)?.Owners.Contains(PluginGuid) == true)
            .Select(method => $"{method.DeclaringType?.FullName}.{method.Name}")
            .OrderBy(name => name)
            .ToArray();

        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
        Log.LogInfo($"Target local players: {TargetPlayerCount}. Slot array length: {SlotArrayLength}.");
        Log.LogInfo($"Patched {patchedMethods.Length} methods:\n  - {string.Join("\n  - ", patchedMethods)}");

        RuntimeSnapshot.LogGlobalState("Plugin.Awake", verbose: false);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RuntimeSnapshot.Log($"Scene loaded: '{scene.name}' ({mode}).", verbose: false);
        RuntimeSnapshot.LogGlobalState($"SceneLoaded:{scene.name}", verbose: true);
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        RuntimeSnapshot.Log($"Scene unloaded: '{scene.name}'.", verbose: false);
    }

    private void BindConfig()
    {
        TargetLocalPlayers = Config.Bind(
            "General",
            "TargetLocalPlayers",
            8,
            new ConfigDescription("Target number of local couch players.", new AcceptableValueRange<int>(4, 8)));

        EnableDiagnostics = Config.Bind(
            "Diagnostics",
            "EnableDiagnostics",
            true,
            "Enable runtime diagnostics for joins, arrays, player counts, and scene transitions.");

        VerboseDiagnostics = Config.Bind(
            "Diagnostics",
            "VerboseDiagnostics",
            false,
            "Log additional verbose state snapshots.");

        DisableReplayRecording = Config.Bind(
            "Stability",
            "DisableReplayRecording",
            true,
            "Disable replay recording to avoid 4-player replay packet assumptions during local 8-player testing.");

        RepositionCharacterSelectBoxes = Config.Bind(
            "Layout",
            "RepositionCharacterSelectBoxes",
            true,
            "Re-layout character select boxes into a compact multi-row arrangement when expanding beyond vanilla slots.");

        CompressMidRoundSelectorSpacing = Config.Bind(
            "Layout",
            "CompressMidRoundSelectorSpacing",
            true,
            "Reduce between-round selector spacing for 5+ players so circles stay on-screen.");

        IncreaseCameraZoomForCrowdedMatches = Config.Bind(
            "Match",
            "IncreaseCameraZoomForCrowdedMatches",
            true,
            "Increase max camera zoom and breathing room when player count exceeds 4.");

        RelaxColorUniquenessWhenFull = Config.Bind(
            "CharacterSelect",
            "RelaxColorUniquenessWhenFull",
            true,
            "Allow duplicate colors once all unique colors are consumed so 5+ players can still ready up.");

        ExpandDrawWinnerUiSlots = Config.Bind(
            "Match",
            "ExpandDrawWinnerUiSlots",
            true,
            "Clone draw-winner UI slots when needed to avoid out-of-range errors with more than 4 players.");
    }

    private void ApplyEarlyRuntimeOverrides()
    {
        if (!DisableReplayRecording.Value)
        {
            return;
        }

        try
        {
            var replayField = AccessTools.Field(typeof(Host), "recordReplay");
            if (replayField != null && replayField.IsStatic && replayField.FieldType == typeof(bool))
            {
                replayField.SetValue(null, false);
                Log.LogInfo("Disabled Host.recordReplay for local 8-player stability.");
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to disable replay recording: {ex.Message}");
        }
    }
}
