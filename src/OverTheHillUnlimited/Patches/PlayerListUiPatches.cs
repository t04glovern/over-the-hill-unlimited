using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// The pause menu and map hold one element per vanilla slot and loop over the whole player list, so with the
/// expanded list they index past their UI lists and throw. While these methods run, OrderedPlayerRefs reports only
/// the first 4 slots and the UI behaves exactly as vanilla, so players in slots 5+ don't show in them. (The map keeps
/// parallel lists of player markers and pin rects, so it isn't padded like the UIs in PerPlayerUiPatches yet.)
/// </summary>
[HarmonyPatch]
public class PlayerListUiPatches
{
    static int _depth;
    static int _frame = -1;

    // Scoped to the current frame so a UI method that throws before its postfix can't leave the clamp on
    public static bool ClampToVanilla => _depth > 0 && _frame == Time.frameCount;

    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(StatePauseMenu), nameof(StatePauseMenu.SetupMenu));
        yield return AccessTools.Method(typeof(StatePauseMenu), nameof(StatePauseMenu.OpenPlayerMenu));
        yield return AccessTools.Method(typeof(StatePauseMenu), nameof(StatePauseMenu.SelectPlayerInfo));
        yield return AccessTools.Method(typeof(StatePauseMenu), nameof(StatePauseMenu.RefreshPlayerOptionsIndicators));
        yield return AccessTools.Method(typeof(MapView), nameof(MapView.SetupPlayerNames));
        yield return AccessTools.Method(typeof(MapView), nameof(MapView.UpdatePlayersNameZoom));
        yield return AccessTools.Method(typeof(MapView), nameof(MapView.UpdatePlayerIndicators));
        yield return AccessTools.Method(typeof(MapView), nameof(MapView.UpdatePlayersPinpoints));
        yield return AccessTools.Method(typeof(MapView), nameof(MapView.IsPlayerPinpoint));
    }

    static void Prefix()
    {
        int frame = Time.frameCount;
        if (_frame != frame)
        {
            _frame = frame;
            _depth = 0;
        }
        _depth++;
    }

    static void Postfix()
    {
        if (_depth > 0)
            _depth--;
    }
}
