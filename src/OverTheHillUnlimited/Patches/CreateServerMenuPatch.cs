using System;
using System.Collections.Generic;
using HarmonyLib;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Offers player counts above 4 in the host's Create Server "Max Players" selector while Unlimited mode is on, and
/// only the game's own options while it's off. The game itself int.Parse()s the selected entry into
/// GameSetupContext.MaxPlayers (ContextUICreateServer.SetGameSetupContextValues), and SessionSizePatches carries that
/// value into Fusion.
/// </summary>
public class CreateServerMenuPatch
{
    // Kept so a mode switch reaches it even while its tab is hidden (FindObjectsOfType skips inactive objects)
    static ContextUICreateServer _menu;

    // The game's own options ("2", "3", "4"), from before this patch first replaced the list
    static readonly List<string> _vanillaOptions = new();

    [HarmonyPatch(typeof(ContextUICreateServer), nameof(ContextUICreateServer.Start))]
    [HarmonyPostfix]
    static void Postfix(ContextUICreateServer __instance)
    {
        _menu = __instance;
        _vanillaOptions.Clear();
        var options = __instance.maxPlayersOptionsRange;
        for (int i = 0; options != null && i < options.Count; i++)
            _vanillaOptions.Add(options[i]);
        Apply(__instance);
    }

    /// <summary>Re-applies the options to the Create Server menu, if one exists, after Unlimited mode changes.</summary>
    public static void ApplyToOpenMenu()
    {
        if (_menu != null)
            Apply(_menu);
    }

    static void Apply(ContextUICreateServer menu)
    {
        var current = menu.maxPlayersOptionsRange;
        var selector = menu.MaxPlayersOptionSelector;
        if (current == null || _vanillaOptions.Count == 0 || selector == null)
        {
            Plugin.Logger.LogWarning("Create Server menu has no Max Players selector, leaving it alone");
            return;
        }

        int currentIndex = menu.maxPlayersOptionsSelectedIndex;
        string selectedValue = currentIndex >= 0 && currentIndex < current.Count ? current[currentIndex] : null;

        // A new list each time: the carousel keeps a reference to the list and a cached count, so editing the old one
        // in place left it out of step. The game's options first, then the counts above them in Unlimited mode.
        var options = new Il2CppSystem.Collections.Generic.List<string>();
        int highest = 0;
        foreach (string option in _vanillaOptions)
        {
            if (!int.TryParse(option, out int value))
            {
                Plugin.Logger.LogWarning("Max Players options aren't plain numbers any more, leaving them alone");
                return;
            }
            options.Add(option);
            highest = Math.Max(highest, value);
        }
        if (UnlimitedMode.Enabled)
        {
            for (int count = highest + 1; count <= Plugin.ConfigMaxPlayers.Value; count++)
                options.Add(count.ToString());
        }

        // Keep the selected count if it's still offered, otherwise the highest one. SetGameSetupContextValues reads
        // maxPlayersOptionsRange[maxPlayersOptionsSelectedIndex], so both must agree with the carousel.
        int selected = selectedValue != null ? options.IndexOf(selectedValue) : -1;
        if (selected < 0)
            selected = options.Count - 1;

        menu.maxPlayersOptionsRange = options;
        menu.maxPlayersOptionsSelectedIndex = selected;
        selector.Initialize(options, selected);
    }
}
