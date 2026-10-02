using HarmonyLib;

namespace BoplMorePlayersLocal8.Patches;

/// <summary>
/// Vanilla only ships per-player sounds for players 1-4 ("beamCharge1".."beamCharge4",
/// "slimeWalk1".."slimeWalk4", ...), but builds the name from player.Id. For players 5-8 the
/// lookup misses: Play only logs a warning, but PlayInterruptable returns index -1 and
/// Beam.ExitAbility then throws in TimesSoundsPlayed (timesSoundPlayed[-1]) before it reaches
/// ability.ExitAbility. The player stays in beam form forever: kills fail (unkillable) and
/// DestroyIfOutsideSceneBounds retries the exit every tick, adding onExitUpSpeed each time
/// (floats upward). See issue #2.
/// </summary>
internal static class PerPlayerSounds
{
    /// <summary>Maps a missing "&lt;name&gt;N" (N &gt; 4) to the existing "&lt;name&gt;((N-1)%4+1)".</summary>
    internal static void Remap(AudioManager audioManager, ref string name)
    {
        if (name == null)
        {
            return;
        }

        var end = name.Length;
        var start = end;
        while (start > 0 && char.IsDigit(name[start - 1]))
        {
            start--;
        }

        if (start == end || start == 0 || !int.TryParse(name.Substring(start), out var slot) || slot <= 4)
        {
            return;
        }

        if (SoundExists(audioManager, name))
        {
            return;
        }

        var fallback = name.Substring(0, start) + (((slot - 1) % 4) + 1);
        if (SoundExists(audioManager, fallback))
        {
            name = fallback;
        }
    }

    // Same match AudioManager uses in Play/PlayInterruptable.
    private static bool SoundExists(AudioManager audioManager, string name)
    {
        var sounds = audioManager.sounds;
        if (sounds == null)
        {
            return false;
        }

        for (var i = 0; i < sounds.Length; i++)
        {
            if (sounds[i].name != null && sounds[i].name.Equals(name))
            {
                return true;
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.PlayInterruptable), new[] { typeof(string) })]
internal static class AudioManagerPlayInterruptablePatch
{
    [HarmonyPrefix]
    private static void Prefix(AudioManager __instance, ref string name) => PerPlayerSounds.Remap(__instance, ref name);
}

// Play(string) and PlayWithRandomPitchVariation forward to these two overloads.
[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.Play), new[] { typeof(string), typeof(float) })]
internal static class AudioManagerPlayDelayPatch
{
    [HarmonyPrefix]
    private static void Prefix(AudioManager __instance, ref string name) => PerPlayerSounds.Remap(__instance, ref name);
}

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.Play), new[] { typeof(string), typeof(float), typeof(float) })]
internal static class AudioManagerPlayPitchPatch
{
    [HarmonyPrefix]
    private static void Prefix(AudioManager __instance, ref string name) => PerPlayerSounds.Remap(__instance, ref name);
}

/// <summary>
/// Safety net: a failed PlayInterruptable lookup (index -1) must never throw, or Beam.ExitAbility
/// aborts before the player leaves beam form.
/// </summary>
[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.TimesSoundsPlayed))]
internal static class AudioManagerTimesSoundsPlayedPatch
{
    [HarmonyPrefix]
    private static bool Prefix(InterruptableAudio interruptableAudio, int[] ___timesSoundPlayed, ref int __result)
    {
        var index = interruptableAudio.index;
        if (___timesSoundPlayed != null && index >= 0 && index < ___timesSoundPlayed.Length)
        {
            return true;
        }

        // Never equal to a real timesPlayed (>= 1) or the failed-lookup value (-1).
        __result = -2;
        return false;
    }
}
