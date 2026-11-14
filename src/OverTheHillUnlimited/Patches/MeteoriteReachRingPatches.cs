using System;
using HarmonyLib;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// The ring a meteorite draws on the ground for its reward reach shows the reach MeteoriteBalancePatches counts with.
/// MeteoriteVisuals copies the meteorite's 10 m (prizeAwardMaxDistance) when it's enabled and draws the ring from it in
/// SwitchMeteoriteState, as it lands. With ScaleRewardReach and more than 4 players, the mod counts cars further out,
/// so the ring is drawn at that reach, and redrawn when players join or leave. Only the host's setting decides the
/// reward; each player's ring follows their own setting, the same by default.
/// </summary>
public class MeteoriteReachRingPatches
{
    static int _players = -1;
    static bool _scale;
    static bool _warned;

    [HarmonyPatch(typeof(MeteoriteVisuals), nameof(MeteoriteVisuals.SwitchMeteoriteState))]
    [HarmonyPrefix]
    static void DrawWidenedReach(MeteoriteVisuals __instance)
    {
        try
        {
            ShowReach(__instance, redrawRing: false);
        }
        catch (Exception e)
        {
            WarnOnce(e);
        }
    }

    // Runs every frame on every player's game; rings already drawn follow players joining and leaving
    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.Render))]
    [HarmonyPostfix]
    static void FollowPlayerCount(OverlandSessionPlayers __instance)
    {
        try
        {
            int players = __instance.GetPlayerCount();
            bool scale = Plugin.ConfigScaleMeteoriteReach.Value;
            if (players == _players && scale == _scale)
                return;
            foreach (var visuals in UnityEngine.Object.FindObjectsOfType<MeteoriteVisuals>())
                ShowReach(visuals, redrawRing: true);
            _players = players;
            _scale = scale;
        }
        catch (Exception e)
        {
            WarnOnce(e);
        }
    }

    /// <summary>The reach the meteorite's ring shows now (for the self-test).</summary>
    internal static float ShownReach(Meteorite meteorite)
    {
        var visuals = meteorite.MeteoriteVisuals;
        return visuals != null ? visuals.prizeAwardMaxDistance : 0;
    }

    static void ShowReach(MeteoriteVisuals visuals, bool redrawRing)
    {
        var meteorite = visuals.GetComponentInParent<Meteorite>();
        if (meteorite == null)
            return;
        float reach = MeteoriteBalancePatches.Reach(MeteoriteBalancePatches.GameReach(meteorite), SessionPlayers);
        visuals.prizeAwardMaxDistance = reach;
        var ring = visuals.areaOfEffectDecalProjector;
        if (redrawRing && ring != null)
        {
            // The game sizes the ring's x and y (across the ground) to the reach's diameter
            ring.m_Size = new UnityEngine.Vector3(2 * reach, 2 * reach, ring.m_Size.z);
            ring.geometryDirty = true;
        }
    }

    static void WarnOnce(Exception e)
    {
        if (_warned)
            return;
        _warned = true;
        Plugin.Logger.LogWarning($"Couldn't show a meteorite's reach ring: {e}");
    }

    static int SessionPlayers => OverlandSessionPlayers.Instance != null ? OverlandSessionPlayers.Instance.GetPlayerCount() : 0;
}
