using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BoplMorePlayersLocal8.Patches;

[HarmonyPatch(typeof(HandleAbilitySelectUI), "Init")]
internal static class HandleAbilitySelectUiSpacingPatch
{
    [HarmonyPrefix]
    private static void Prefix(HandleAbilitySelectUI __instance, ref float __state)
    {
        var separationField = Traverse.Create(__instance).Field<float>("Separation");
        __state = separationField.Value;

        if (!Plugin.CompressMidRoundSelectorSpacing.Value)
        {
            return;
        }

        var count = PlayerHandler.Get()?.NumberOfPlayers() ?? 0;
        if (count <= 4)
        {
            return;
        }

        var factor = 4f / count;
        var compressed = Mathf.Max(48f, __state * factor);
        separationField.Value = compressed;
        RuntimeSnapshot.Log($"Compressed mid-round selector separation for {count} players: {__state} -> {compressed}.", verbose: false);
    }

    [HarmonyPostfix]
    private static void Postfix(HandleAbilitySelectUI __instance, float __state)
    {
        Traverse.Create(__instance).Field<float>("Separation").Value = __state;
    }
}

[HarmonyPatch(typeof(PlayerAverageCamera), "Start")]
internal static class PlayerAverageCameraStartPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerAverageCamera __instance)
    {
        if (!Plugin.IncreaseCameraZoomForCrowdedMatches.Value)
        {
            return;
        }

        var count = PlayerHandler.Get()?.NumberOfPlayers() ?? 0;
        if (count <= 4)
        {
            return;
        }

        var traverse = Traverse.Create(__instance);
        var maxZoom = traverse.Field<float>("MAX_ZOOM").Value;
        var minZoom = traverse.Field<float>("MIN_ZOOM").Value;
        var extraZoom = traverse.Field<float>("extraZoomRoom").Value;

        var zoomMultiplier = Mathf.Lerp(1.2f, 1.6f, (count - 4) / 4f);
        var newMaxZoom = maxZoom * zoomMultiplier;
        var newMinZoom = minZoom * 1.05f;
        var newExtraZoom = extraZoom + (count - 4) * 0.25f;

        traverse.Field<float>("MAX_ZOOM").Value = newMaxZoom;
        traverse.Field<float>("MIN_ZOOM").Value = newMinZoom;
        traverse.Field<float>("extraZoomRoom").Value = newExtraZoom;

        RuntimeSnapshot.Log(
            $"Expanded camera zoom for {count} players (MIN_ZOOM {minZoom}->{newMinZoom}, MAX_ZOOM {maxZoom}->{newMaxZoom}, extraZoomRoom {extraZoom}->{newExtraZoom}).",
            verbose: false);
    }
}

[HarmonyPatch(typeof(CharacterStatsList), "OnEnable")]
internal static class CharacterStatsListDrawSlotsPatch
{
    [HarmonyPrefix]
    private static void Prefix(CharacterStatsList __instance)
    {
        if (!Plugin.ExpandDrawWinnerUiSlots.Value)
        {
            return;
        }

        var count = PlayerHandler.Get()?.NumberOfPlayers() ?? 0;
        if (count <= 4)
        {
            return;
        }

        EnsureImageArrayCapacity(__instance, "winnersOfDraw", count, Vector2.right, 75f);
    }

    private static void EnsureImageArrayCapacity(CharacterStatsList owner, string fieldName, int required, Vector2 direction, float spacing)
    {
        var field = Traverse.Create(owner).Field<Image[]>(fieldName);
        var current = field.Value;
        if (current == null || current.Length == 0 || current.Length >= required)
        {
            return;
        }

        var list = current.ToList();
        var template = current[^1];
        var templateRect = template.GetComponent<RectTransform>();
        for (var i = list.Count; i < required; i++)
        {
            var cloneGo = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            cloneGo.name = $"{template.gameObject.name}_P{i + 1}";

            var cloneImage = cloneGo.GetComponent<Image>();
            if (cloneImage == null)
            {
                continue;
            }

            var cloneRect = cloneGo.GetComponent<RectTransform>();
            if (cloneRect != null && templateRect != null)
            {
                cloneRect.anchoredPosition = templateRect.anchoredPosition + (direction * spacing * (i - (current.Length - 1)));
            }

            cloneGo.SetActive(false);
            list.Add(cloneImage);
        }

        field.Value = list.ToArray();
        RuntimeSnapshot.Log($"Expanded {fieldName} from {current.Length} to {list.Count}.", verbose: false);
    }
}

[HarmonyPatch(typeof(GameSessionHandler), "SpawnPlayers")]
internal static class GameSessionSpawnDiagnosticsPatch
{
    [HarmonyPostfix]
    private static void Postfix(GameSessionHandler __instance)
    {
        var players = PlayerHandler.Get()?.PlayerList() ?? new List<Player>();
        var slimes = Traverse.Create(__instance).Field<SlimeController[]>("slimeControllers").Value;
        RuntimeSnapshot.Log(
            $"SpawnPlayers complete: players={players.Count}, slimes={slimes?.Length ?? -1}, teamsLeft={PlayerHandler.Get()?.TeamsLeft() ?? -1}.",
            verbose: false);
        RuntimeSnapshot.LogGlobalState("GameSessionHandler.SpawnPlayers", verbose: true);
    }
}
