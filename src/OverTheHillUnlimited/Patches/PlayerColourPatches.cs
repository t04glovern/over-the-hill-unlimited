using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using OverTheHillUnlimited.Networking;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Colours for players 5 and up. UIManager.GetPlayerColor switches on the player's colour slot with cases for slots 0-3
/// (UIManager.Player1Color..Player4Color) and a fixed default for anything else, so without this every player from the
/// 5th on shares that default on the map, compass, name labels and notifications. The flare at each player's map pin
/// (WorldSpacePinpoint.GetPlayerFlare) switches on the same slot: Flare_Yellow, Flare_Purple and Flare_Green for slots
/// 0-2 and Flare_Red for anything else, so every player from the 5th on also had player 4's red flare.
///
/// Windows only: GetPlayerColor returns a 16-byte Color, which the System V ABI returns in registers, and Il2CppInterop's
/// trampolines get register-returned structs wrong (the same family of problem as by-value struct parameters). On native
/// Linux, players 5 and up keep the default colour, and the flares keep player 4's red to go with it.
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

    // Property of the flare stick's material (Shader Graphs/EnvironmentPropShader) holding its colour: a texture of one
    // flat colour per flare (Anchor_Red, Anchor_Green, ...)
    internal const string FlareStickTexture = "_Main_Texture";

    // Recoloured copies of the game's flare for colour slots 4..15, by slot. The game loads the flare prefabs once and
    // keeps them for the whole run (ResourcesManager has no way to release them), so each copy is made once and kept.
    static readonly Dictionary<int, GameObject> _extraFlares = new();

    // Holds the copies. It's inactive, so they never show or run, while the ones the game instantiates from them are
    // active, the way an active prefab's instances are.
    static GameObject _extraFlareHolder;

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

    // WorldSpacePinpoint.InstantiateFlare instantiates the returned flare under the pin owner's pin
    [HarmonyPatch(typeof(WorldSpacePinpoint), nameof(WorldSpacePinpoint.GetPlayerFlare))]
    [HarmonyPostfix]
    static void FlareForExtraSlots(PlayerRef playerRef, ref GameObject __result)
    {
        if (!UnlimitedMode.Enabled || __result == null)
            return;
        int slot = ColourSlot(playerRef);
        if (slot >= Plugin.VanillaMaxPlayers && slot - Plugin.VanillaMaxPlayers < ExtraColours.Length)
            __result = ExtraFlare(__result, slot - Plugin.VanillaMaxPlayers);
    }

    // A copy of the game's flare (Flare_Red, for any slot past the 4th) in an extra slot's colour
    static GameObject ExtraFlare(GameObject gameFlare, int extra)
    {
        if (_extraFlares.TryGetValue(extra, out var flare) && flare != null)
            return flare;

        if (_extraFlareHolder == null)
        {
            _extraFlareHolder = new GameObject($"{Plugin.Id} flares");
            _extraFlareHolder.SetActive(false);
            Object.DontDestroyOnLoad(_extraFlareHolder);
        }
        flare = Object.Instantiate(gameFlare, _extraFlareHolder.transform);
        flare.name = $"Flare_Player{Plugin.VanillaMaxPlayers + extra + 1}";
        Recolour(flare, ExtraColours[extra]);
        _extraFlares[extra] = flare;
        return flare;
    }

    // Where the game's flares differ from each other: the light (which also tints the lens flare), the start colour of
    // the smoke particles, and the flare stick's texture
    static void Recolour(GameObject flare, Color colour)
    {
        foreach (var light in flare.GetComponentsInChildren<Light>(true))
            light.color = colour;

        foreach (var particles in flare.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!IsFlareSmoke(particles))
                continue;
            var main = particles.main;
            var startColour = main.startColor;
            startColour.m_Mode = ParticleSystemGradientMode.Color;
            startColour.m_ColorMax = colour;
            main.startColor = startColour;
        }

        // The game's stick textures are one flat colour, so a single pixel stands in for them
        var texture = new Texture2D(1, 1) { name = flare.name };
        texture.SetPixel(0, 0, colour);
        texture.Apply();
        Material stick = null;
        foreach (var renderer in flare.GetComponentsInChildren<MeshRenderer>(true))
        {
            var material = renderer.sharedMaterial;
            if (material == null || !material.HasProperty(FlareStickTexture))
                continue;
            if (stick == null)
            {
                stick = new Material(material) { name = flare.name };
                stick.SetTexture(FlareStickTexture, texture);
            }
            renderer.sharedMaterial = stick;
        }
    }

    /// <summary>
    /// Whether these are one of a flare's three smoke particle systems (FlareParticle, FlareMidtParticle,
    /// FlareDistantParticle), which are in the flare's colour. Its fire and sparks are the same in every flare.
    /// </summary>
    internal static bool IsFlareSmoke(ParticleSystem particles) => particles.name.StartsWith("Flare");

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
