using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BoplMorePlayersLocal8.Patches;

[HarmonyPatch(typeof(CharacterSelectHandler), "Awake")]
internal static class CharacterSelectHandlerAwakePatch
{
    [HarmonyPostfix]
    private static void Postfix(CharacterSelectHandler __instance)
    {
        CharacterSelectExpander.Expand(__instance);

        // Log all connected gamepads for debugging join cap
        var gamepads = Gamepad.all;
        var gpInfo = new System.Text.StringBuilder();
        gpInfo.Append($"Connected gamepads: {gamepads.Count}");
        for (var i = 0; i < gamepads.Count; i++)
        {
            gpInfo.Append($"\n  [{i}] id={gamepads[i].deviceId} name='{gamepads[i].displayName}'");
        }
        RuntimeSnapshot.Log(gpInfo.ToString(), verbose: false);

        // Log CharSelectClickToJoin instances
        var clickToJoins = UnityEngine.Object.FindObjectsOfType<CharSelectClickToJoin>(includeInactive: true);
        RuntimeSnapshot.Log($"CharSelectClickToJoin instances: {clickToJoins.Length} " +
            $"[{string.Join(", ", clickToJoins.Select(c => $"box{c.csb?.RectangleIndex}(active={c.gameObject.activeInHierarchy})"))}]",
            verbose: false);

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

/// <summary>
/// The vanilla LateUpdate only processes joins when CurrentlyActiveIndex() == csb.RectangleIndex.
/// Since cloned boxes (slots 4+) have their CharSelectClickToJoin destroyed (to avoid duplicate
/// input issues), no instance's RectangleIndex matches slots 4+. This postfix piggybacks on
/// box 0's LateUpdate to handle gamepad joins for the expanded slots.
/// </summary>
[HarmonyPatch(typeof(CharSelectClickToJoin), "LateUpdate")]
internal static class CharSelectClickToJoinExpandedSlotsPatch
{
    [HarmonyPostfix]
    private static void Postfix(CharSelectClickToJoin __instance)
    {
        // Only run from one instance to avoid processing multiple times per frame
        if (__instance.csb.RectangleIndex != 0)
        {
            return;
        }

        // Find the next free slot
        var occupied = CharacterSelectBox.occupiedRectangles;
        if (occupied == null)
        {
            return;
        }

        var nextFreeSlot = -1;
        var playableSlots = Mathf.Min(Plugin.TargetPlayerCount, Mathf.Max(0, occupied.Length - 1));
        for (var i = 0; i < playableSlots; i++)
        {
            if (!occupied[i])
            {
                nextFreeSlot = i;
                break;
            }
        }

        // Vanilla LateUpdate already handles slots 0-3 (original boxes have their own CharSelectClickToJoin)
        if (nextFreeSlot < 4)
        {
            return;
        }

        // Get the CharacterSelectBox for the target slot
        var handler = Traverse.Create(typeof(CharacterSelectHandler)).Field<CharacterSelectHandler>("selfRef").Value;
        if (handler == null)
        {
            return;
        }

        var boxes = Traverse.Create(handler).Field<CharacterSelectBox[]>("characterSelectBoxes").Value;
        if (boxes == null || nextFreeSlot >= boxes.Length)
        {
            return;
        }

        var targetBox = boxes[nextFreeSlot];
        if (targetBox == null)
        {
            return;
        }

        var targetMenuState = Traverse.Create(targetBox).Field<CharSelectMenu>("menuState").Value;
        if (targetMenuState != (CharSelectMenu)0) // CharSelectMenu.join == 0
        {
            return;
        }

        // Check for keyboard join (if keyboard not already taken)
        if (!CharacterSelectBox.keyboardMouseIsOccupied)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame && !keyboard.escapeKey.wasPressedThisFrame)
            {
                CharacterSelectBox.deviceIds[nextFreeSlot] = 0;
                targetBox.UseskeyboardMouse = true;
                CharacterSelectBox.keyboardMouseIsOccupied = true;
                targetBox.OnEnterSelect();
                AudioManager.Get().Play("accept");
                RuntimeSnapshot.Log($"Expanded slot join: keyboard → slot {nextFreeSlot}.", verbose: false);
                return;
            }
        }

        // Check for gamepad joins
        var deviceIds = CharacterSelectBox.deviceIds;
        foreach (var gamepad in Gamepad.all)
        {
            // Skip if this gamepad is already assigned to any slot
            var alreadyAssigned = false;
            for (var j = 0; j < deviceIds.Length; j++)
            {
                if (deviceIds[j] == gamepad.deviceId)
                {
                    alreadyAssigned = true;
                    break;
                }
            }

            if (alreadyAssigned)
            {
                continue;
            }

            if (gamepad.startButton.wasPressedThisFrame || gamepad.aButton.wasPressedThisFrame)
            {
                CharacterSelectBox.deviceIds[nextFreeSlot] = gamepad.deviceId;
                targetBox.UseskeyboardMouse = false;
                targetBox.currentGamePad = gamepad;
                targetBox.OnEnterSelect();
                AudioManager.Get().Play("accept");
                RuntimeSnapshot.Log($"Expanded slot join: gamepad {gamepad.deviceId} → slot {nextFreeSlot}.", verbose: false);
                return;
            }
        }
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
            RepositionBoxes(handler, boxes, beforeCount);
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

            // Destroy the cloned CharSelectClickToJoin listener so this box doesn't independently
            // claim controller input. The original vanilla instances on boxes 1-4 already route
            // joins to slots 5-8 via our CurrentlyActiveIndex patch.
            var clickToJoin = cloneGo.GetComponentInChildren<CharSelectClickToJoin>(includeInactive: true);
            if (clickToJoin != null)
            {
                UnityEngine.Object.Destroy(clickToJoin);
                RuntimeSnapshot.Log($"Destroyed duplicate CharSelectClickToJoin on clone slot {slot}.", verbose: false);
            }

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

            var field = component.GetType().GetField(
                "csb",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

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

    private static void RepositionBoxes(CharacterSelectHandler handler, CharacterSelectBox[] boxes, int originalCount)
    {
        if (boxes.Length <= originalCount || originalCount <= 0)
        {
            return;
        }

        var originalRects = boxes
            .Take(originalCount)
            .Select(box => box.GetComponent<RectTransform>())
            .Where(rect => rect != null)
            .Cast<RectTransform>()
            .ToArray();

        if (originalRects.Length == 0)
        {
            return;
        }

        var columns = originalRects.Length;
        var rows = Mathf.CeilToInt(boxes.Length / (float)columns);

        var minX = originalRects.Min(rect => rect.anchoredPosition.x);
        var maxX = originalRects.Max(rect => rect.anchoredPosition.x);
        var centerX = (minX + maxX) * 0.5f;
        var centerY = originalRects.Average(rect => rect.anchoredPosition.y);

        var templateWidth = boxes
            .Take(originalCount)
            .Select(MeasureJoinCardWidth)
            .DefaultIfEmpty(260f)
            .Max();

        var baselineSpan = Mathf.Max(maxX - minX, templateWidth * 3.2f);
        var sidePadding = templateWidth * 0.15f;
        var availableWidth = baselineSpan + (sidePadding * 2f);

        var count = boxes.Length;
        var spacing = count <= 1 ? 0f : availableWidth / (count - 1);
        var desiredVisualWidth = spacing * 0.93f;
        var scale = Mathf.Clamp(desiredVisualWidth / templateWidth, 0.40f, 1f);
        var firstX = centerX - (spacing * (count - 1) * 0.5f);
        var rowY = centerY;

        var placementLog = new List<string>(count);

        for (var i = 0; i < count; i++)
        {
            var rect = boxes[i].GetComponent<RectTransform>();
            if (rect == null)
            {
                continue;
            }

            var x = firstX + (i * spacing);
            rect.anchoredPosition = new Vector2(x, rowY);
            // Keep root at scale 1 so parked panels (y=3000) land at world y=3000 (off-screen).
            // Shrink only the visible children so the row fits without fighting the game's Y-management.
            rect.localScale = Vector3.one;
            ScaleVisibleChildren(boxes[i], scale);
            placementLog.Add($"slot={i + 1} x={x:0.0} y={rowY:0.0} scale={scale:0.000}");
        }

        RuntimeSnapshot.Log(
            $"Repositioned character select to single row: slots={count}, baseColumns={columns}, rowsBeforeCompression={rows}, centerX={centerX:0.0}, centerY={centerY:0.0}, rowY={rowY:0.0}, availableWidth={availableWidth:0.0}, spacing={spacing:0.0}, templateVisualWidth={templateWidth:0.0}, desiredVisualWidth={desiredVisualWidth:0.0}, scale={scale:0.000}.",
            verbose: false);
        RuntimeSnapshot.Log($"Row placement details: {string.Join(" | ", placementLog)}", verbose: false);
    }

    private static float MeasureJoinCardWidth(CharacterSelectBox box)
    {
        var joinBorder = Traverse.Create(box).Field<RectTransform>("joinBorder").Value;
        if (joinBorder != null)
        {
            var parent = joinBorder.parent;
            if (parent == null)
            {
                return Mathf.Max(220f, joinBorder.rect.width);
            }

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, joinBorder);
            return Mathf.Max(joinBorder.rect.width, bounds.size.x);
        }

        var rootRect = box.GetComponent<RectTransform>();
        if (rootRect == null)
        {
            return 260f;
        }

        return Mathf.Max(220f, rootRect.rect.width * 2f);
    }

    // Scale every direct child of the box. Parked panels (SELECT_COLOR, SELECT_BORDER,
    // AbilityGrid_leaveActive) keep their anchoredPosition — localScale only shrinks their own
    // contents, not their Y offset. So y=3000 parking still places them off-screen, and the game's
    // own slide-down animation still works, just at the reduced size that matches the row.
    private static void ScaleVisibleChildren(CharacterSelectBox box, float scale)
    {
        if (scale <= 0f || Mathf.Approximately(scale, 1f))
        {
            return;
        }

        var scaleVec = Vector3.one * scale;
        var scaled = new List<string>();
        for (var i = 0; i < box.transform.childCount; i++)
        {
            var child = box.transform.GetChild(i);
            child.localScale = scaleVec;
            scaled.Add(child.name);
        }

        RuntimeSnapshot.Log($"Box '{box.name}' scaled all {scaled.Count} children [{string.Join(",", scaled)}] to {scale:0.000}.", verbose: false);
    }
}

