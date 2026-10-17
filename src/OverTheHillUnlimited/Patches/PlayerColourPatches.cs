using System.Runtime.InteropServices;
using HarmonyLib;
using OverTheHillUnlimited.Networking;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Colours for players 5 and up. UIManager.GetPlayerColor switches on the player's colour slot with cases for slots 0-3
/// (UIManager.Player1Color..Player4Color) and a fixed default for anything else, so without this every player from the
/// 5th on shares that default on the map, compass, name labels and notifications.
///
/// Windows only: GetPlayerColor returns a 16-byte Color, which the System V ABI returns in registers, and Il2CppInterop's
/// trampolines get register-returned structs wrong (the same family of problem as by-value struct parameters). On native
/// Linux, players 5 and up keep the default colour.
/// </summary>
public class PlayerColourPatches
{
    public static bool Supported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    // For colour slots 4..15, distinct from each other and readable on the map and HUD
    static readonly Color[] ExtraColours =
    {
        new(0.95f, 0.56f, 0.17f), // orange
        new(0.30f, 0.75f, 0.71f), // teal
        new(0.94f, 0.47f, 0.70f), // pink
        new(0.62f, 0.82f, 0.25f), // lime
        new(0.56f, 0.42f, 0.80f), // violet
        new(0.98f, 0.85f, 0.25f), // yellow
        new(0.36f, 0.62f, 0.95f), // sky blue
        new(0.80f, 0.33f, 0.33f), // brick
        new(0.55f, 0.40f, 0.26f), // brown
        new(0.20f, 0.55f, 0.35f), // forest
        new(0.75f, 0.75f, 0.75f), // silver
        new(0.12f, 0.30f, 0.60f), // navy
    };

    [HarmonyPatch(typeof(UIManager), nameof(UIManager.GetPlayerColor))]
    [HarmonyPostfix]
    static void ColourExtraSlots(PlayerRef playerRef, ref Color __result)
    {
        if (!UnlimitedMode.Enabled)
            return;
        int slot = ColourSlot(playerRef);
        if (slot >= Plugin.VanillaMaxPlayers && slot - Plugin.VanillaMaxPlayers < ExtraColours.Length)
            __result = ExtraColours[slot - Plugin.VanillaMaxPlayers];
    }

    // The player's index in OverlandSessionPlayers.PlayerColors, as GetPlayerColor works it out; -1 if they have none
    static int ColourSlot(PlayerRef player)
    {
        var players = OverlandSessionPlayers.Instance;
        if (players == null || !SessionPlayersCapacityPatches.State.IsExpanded(players))
            return -1;
        var slots = players.PlayerColors;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].RawEncoded == player.RawEncoded)
                return i;
        }
        return -1;
    }
}
