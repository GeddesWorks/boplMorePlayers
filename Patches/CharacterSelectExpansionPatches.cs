using HarmonyLib;
using UnityEngine;

namespace BoplMorePlayersLocal8.Patches;

[HarmonyPatch(typeof(CharacterSelectHandler), "Awake")]
internal static class CharacterSelectHandlerAwakePatch
{
    [HarmonyPostfix]
    private static void Postfix(CharacterSelectHandler __instance)
    {
        CharacterSelectExpander.Expand(__instance);
        RuntimeSnapshot.LogGlobalState("CharacterSelectHandler.Awake.Postfix", verbose: false);
    }
}

[HarmonyPatch(typeof(CharSelectClickToJoin), "CurrentlyActiveIndex")]
internal static class CharSelectClickToJoinCurrentIndexPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref int __result)
    {
        var occupied = Traverse.Create(typeof(CharacterSelectBox)).Field<bool[]>("occupiedRectangles").Value;
        if (occupied == null || occupied.Length == 0)
        {
            return true;
        }

        var playableSlots = Mathf.Min(Plugin.TargetPlayerCount, Mathf.Max(0, occupied.Length - 1));
        for (var slot = 0; slot < playableSlots; slot++)
        {
            if (!occupied[slot])
            {
                __result = slot;
                return false;
            }
        }

        __result = occupied.Length - 1;
        return false;
    }
}

[HarmonyPatch(typeof(CharacterSelectHandler), nameof(CharacterSelectHandler.IsColorTaken))]
internal static class CharacterSelectColorLimitPatch
{
    [HarmonyPrefix]
    private static bool Prefix(int __0, CharacterSelectBox __1, ref bool __result)
    {
        if (!Plugin.RelaxColorUniquenessWhenFull.Value || GameLobby.isOnlineGame)
        {
            return true;
        }

        try
        {
            var handler = Traverse.Create(typeof(CharacterSelectHandler)).Field<CharacterSelectHandler>("selfRef").Value;
            if (handler == null)
            {
                return true;
            }

            var boxes = Traverse.Create(handler).Field<CharacterSelectBox[]>("characterSelectBoxes").Value;
            var playerColors = Traverse.Create(handler).Field<PlayerColors>("playerColors").Value;
            var colorCount = playerColors?.Length ?? 0;
            if (boxes == null || boxes.Length == 0 || colorCount <= 0)
            {
                return true;
            }

            var readyState = (CharSelectMenu)2;
            var readyPlayers = boxes.Count(box =>
                box != null &&
                box != __1 &&
                Traverse.Create(box).Field<CharSelectMenu>("menuState").Value == readyState);

            if (readyPlayers < colorCount)
            {
                return true;
            }

            __result = false;
            RuntimeSnapshot.Log(
                $"Color uniqueness relaxed (readyPlayers={readyPlayers}, availableColors={colorCount}, requestedColor={__0}).",
                verbose: false);
            return false;
        }
        catch (Exception ex)
        {
            RuntimeSnapshot.Log($"Color uniqueness relaxation failed: {ex.Message}", verbose: false);
            return true;
        }
    }
}

[HarmonyPatch(typeof(CharacterSelectBox), nameof(CharacterSelectBox.OnEnterSelect))]
internal static class CharacterSelectJoinDiagnosticsPatch
{
    [HarmonyPostfix]
    private static void Postfix(CharacterSelectBox __instance)
    {
        RuntimeSnapshot.Log(
            $"Player joined slot={__instance.RectangleIndex}, usesKeyboard={__instance.UseskeyboardMouse}, menuState={Traverse.Create(__instance).Field<CharSelectMenu>("menuState").Value}.",
            verbose: false);
        RuntimeSnapshot.LogGlobalState("CharacterSelectBox.OnEnterSelect", verbose: true);
    }
}

internal static class CharacterSelectExpander
{

    internal static void Expand(CharacterSelectHandler handler)
    {

        var boxes = Traverse.Create(handler).Field<CharacterSelectBox[]>("characterSelectBoxes").Value;
        if (boxes == null || boxes.Length == 0)
        {
            RuntimeSnapshot.Log("CharacterSelectExpander: characterSelectBoxes missing.", verbose: false);
            return;
        }

        EnsureSlotArrays();

        var targetCount = Plugin.TargetPlayerCount;
        var beforeCount = boxes.Length;
        if (boxes.Length < targetCount)
        {
            boxes = ExpandBoxes(handler, boxes, targetCount);
            Traverse.Create(handler).Field<CharacterSelectBox[]>("characterSelectBoxes").Value = boxes;
            RuntimeSnapshot.Log($"Expanded characterSelectBoxes from {beforeCount} to {boxes.Length}.", verbose: false);
        }

        EnsureAnimateOutDelays(handler, boxes.Length);

        if (Plugin.RepositionCharacterSelectBoxes.Value)
        {
            RepositionBoxes(handler, boxes);
        }


    }

    private static void EnsureSlotArrays()
    {
        var desired = Plugin.SlotArrayLength;

        var occupiedField = Traverse.Create(typeof(CharacterSelectBox)).Field<bool[]>("occupiedRectangles");
        var occupied = occupiedField.Value ?? Array.Empty<bool>();
        if (occupied.Length < desired)
        {
            var expanded = new bool[desired];
            Array.Copy(occupied, expanded, occupied.Length);
            occupiedField.Value = expanded;
            RuntimeSnapshot.Log($"Expanded occupiedRectangles from {occupied.Length} to {desired}.", verbose: false);
        }

        var deviceField = Traverse.Create(typeof(CharacterSelectBox)).Field<int[]>("deviceIds");
        var devices = deviceField.Value ?? Array.Empty<int>();
        if (devices.Length < desired)
        {
            var expanded = new int[desired];
            Array.Copy(devices, expanded, devices.Length);
            deviceField.Value = expanded;
            RuntimeSnapshot.Log($"Expanded deviceIds from {devices.Length} to {desired}.", verbose: false);
        }
    }

    private static CharacterSelectBox[] ExpandBoxes(CharacterSelectHandler handler, CharacterSelectBox[] existing, int targetCount)
    {
        var expanded = existing.ToList();
        var template = existing[^1];
        var parent = template.transform.parent;

        for (var slot = existing.Length; slot < targetCount; slot++)
        {
            var cloneGo = UnityEngine.Object.Instantiate(template.gameObject, parent);
            cloneGo.name = $"{template.gameObject.name}_P{slot + 1}";

            var clone = cloneGo.GetComponent<CharacterSelectBox>();
            if (clone == null)
            {
                RuntimeSnapshot.Log($"CharacterSelectExpander: clone missing CharacterSelectBox for slot {slot}.", verbose: false);
                continue;
            }

            clone.RectangleIndex = slot;
            clone.UseskeyboardMouse = false;
            clone.currentGamePad = null;

            Traverse.Create(clone).Field<int>("selectedIndex").Value = 0;
            Traverse.Create(clone).Field<CharSelectMenu>("menuState").Value = (CharSelectMenu)0;

            RebindNestedCharacterSelectReferences(clone);
            clone.OnEnterJoin();

            expanded.Add(clone);
            RuntimeSnapshot.Log($"Created character select slot {slot + 1} from template '{template.name}'.", verbose: false);
        }

        return expanded.ToArray();
    }

    private static void RebindNestedCharacterSelectReferences(CharacterSelectBox owner)
    {
        var ownerType = typeof(CharacterSelectBox);
        foreach (var component in owner.GetComponentsInChildren<Component>(includeInactive: true))
        {
            if (component == null)
            {
                continue;
            }

            var field = AccessTools.Field(component.GetType(), "csb");
            if (field == null || field.FieldType != ownerType)
            {
                continue;
            }

            field.SetValue(component, owner);
        }
    }

    private static void EnsureAnimateOutDelays(CharacterSelectHandler handler, int targetLength)
    {
        var field = Traverse.Create(handler).Field<float[]>("animateOutDelays");
        var existing = field.Value ?? Array.Empty<float>();
        if (existing.Length >= targetLength)
        {
            return;
        }

        var expanded = new float[targetLength];
        Array.Copy(existing, expanded, existing.Length);

        var step = 0.05f;
        if (existing.Length >= 2)
        {
            step = Mathf.Abs(existing[^1] - existing[^2]);
            if (step < 0.005f)
            {
                step = 0.05f;
            }
        }

        var baseDelay = existing.Length > 0 ? existing[^1] : 0f;
        for (var i = existing.Length; i < expanded.Length; i++)
        {
            expanded[i] = baseDelay + ((i - existing.Length + 1) * step);
        }

        field.Value = expanded;
        RuntimeSnapshot.Log($"Expanded animateOutDelays from {existing.Length} to {expanded.Length}.", verbose: false);
    }

    private static void RepositionBoxes(CharacterSelectHandler handler, CharacterSelectBox[] boxes)
    {
        if (boxes.Length <= 4)
        {
            return;
        }

        var rects = boxes
            .Select(box => box.GetComponent<RectTransform>())
            .Where(rect => rect != null)
            .Cast<RectTransform>()
            .ToArray();

        if (rects.Length == 0)
        {
            return;
        }

        var centerX = rects.Average(rect => rect.anchoredPosition.x);
        var centerY = rects.Average(rect => rect.anchoredPosition.y);
        var xMin = rects.Min(rect => rect.anchoredPosition.x);
        var xMax = rects.Max(rect => rect.anchoredPosition.x);

        var columns = Mathf.Min(4, boxes.Length);
        var rows = Mathf.CeilToInt(boxes.Length / (float)columns);
        var xSpan = columns <= 1 ? 0f : Mathf.Max(800f, Mathf.Abs(xMax - xMin));
        var rowSpacing = Mathf.Max(170f, rects[0].rect.height * 1.05f);
        var scale = boxes.Length > 4 ? 0.9f : 1f;

        for (var i = 0; i < boxes.Length; i++)
        {
            var rect = boxes[i].GetComponent<RectTransform>();
            if (rect == null)
            {
                continue;
            }

            var row = i / columns;
            var col = i % columns;

            var xT = columns <= 1 ? 0.5f : col / (float)(columns - 1);
            var x = Mathf.Lerp(centerX - (xSpan * 0.5f), centerX + (xSpan * 0.5f), xT);

            var rowOffset = ((rows - 1) * 0.5f) - row;
            var y = centerY + (rowOffset * rowSpacing);

            rect.anchoredPosition = new Vector2(x, y);
            rect.localScale = Vector3.one * scale;
        }

        var startButton = Traverse.Create(handler).Field<AnimateInOutUI>("startButton").Value;
        if (startButton != null)
        {
            var startRect = startButton.GetComponent<RectTransform>();
            if (startRect != null)
            {
                var minY = rects.Min(rect => rect.anchoredPosition.y);
                startRect.anchoredPosition = new Vector2(startRect.anchoredPosition.x, minY - (rowSpacing * 0.85f));
            }
        }

        Traverse.Create(handler).Field<Vector2>("center").Value = new Vector2(centerX, centerY - (rowSpacing * 0.2f));
        RuntimeSnapshot.Log($"Repositioned character select layout for {boxes.Length} boxes ({columns}x{rows}).", verbose: false);
    }
}

