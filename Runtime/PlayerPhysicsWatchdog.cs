using HarmonyLib;
using UnityEngine;

namespace BoplMorePlayersLocal8;

/// <summary>
/// Periodically dumps every player's Rigidbody2D state so a Beam-triggered stuck
/// spin/anti-gravity state (reported for player slot 5+) shows up in LogOutput.log with the
/// exact slot index, frame-time, and physics values, instead of only a silent visual glitch.
/// </summary>
internal static class PlayerPhysicsWatchdog
{
    private const float IntervalSeconds = 2f;
    private const float SpinAngularVelocityThreshold = 25f;

    private static float _lastTick;

    internal static void Tick()
    {
        if (!Plugin.EnableDiagnostics.Value || !Plugin.EnablePhysicsWatchdog.Value)
        {
            return;
        }

        if (Time.unscaledTime - _lastTick < IntervalSeconds)
        {
            return;
        }

        _lastTick = Time.unscaledTime;

        var session = UnityEngine.Object.FindObjectOfType<GameSessionHandler>();
        if (session == null)
        {
            return;
        }

        var slimes = Traverse.Create(session).Field<SlimeController[]>("slimeControllers").Value;
        if (slimes == null || slimes.Length == 0)
        {
            return;
        }

        for (var slot = 0; slot < slimes.Length; slot++)
        {
            var slime = slimes[slot];
            if (slime == null)
            {
                continue;
            }

            var rb = slime.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = slime.GetComponentInChildren<Rigidbody2D>();
            }

            if (rb == null)
            {
                continue;
            }

            var spinning = Mathf.Abs(rb.angularVelocity) > SpinAngularVelocityThreshold;
            var zeroGravity = Mathf.Approximately(rb.gravityScale, 0f);

            var state = $"slot={slot} pos={rb.position} vel={rb.velocity} angularVel={rb.angularVelocity:0.0} " +
                        $"gravityScale={rb.gravityScale:0.00} bodyType={rb.bodyType} simulated={rb.simulated} " +
                        $"freezeRotation={rb.freezeRotation}";

            if (spinning || zeroGravity)
            {
                Plugin.Log.LogWarning($"[Watchdog] possible stuck spin/anti-gravity state: {state}");
            }
            else
            {
                RuntimeSnapshot.Log($"[Watchdog] {state}", verbose: true);
            }
        }
    }
}
