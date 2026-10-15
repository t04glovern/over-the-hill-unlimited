using HarmonyLib;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Offers player counts above 4 in the host's Create Server "Max Players" selector while Unlimited mode is on, and
/// only the vanilla 2-4 while it's off. The game itself int.Parse()s the selected entry into GameSetupContext.MaxPlayers
/// (ContextUICreateServer.SetGameSetupContextValues), and SessionSizePatches carries that value into Fusion.
/// </summary>
public class CreateServerMenuPatch
{
    [HarmonyPatch(typeof(ContextUICreateServer), nameof(ContextUICreateServer.Start))]
    [HarmonyPostfix]
    static void Postfix(ContextUICreateServer __instance)
    {
        Apply(__instance);
    }

    /// <summary>Re-applies the options to any open Create Server menu, after Unlimited mode changes.</summary>
    public static void ApplyToOpenMenus()
    {
        foreach (var menu in Object.FindObjectsOfType<ContextUICreateServer>())
            Apply(menu);
    }

    static void Apply(ContextUICreateServer menu)
    {
        var options = menu.maxPlayersOptionsRange;
        var selector = menu.MaxPlayersOptionSelector;
        if (options == null || selector == null)
        {
            Plugin.Logger.LogWarning("Create Server menu has no Max Players selector, leaving it alone");
            return;
        }

        // Back to the vanilla "2", "3", "4", then extend if Unlimited mode is on
        for (int i = options.Count - 1; i >= 0; i--)
        {
            if (!int.TryParse(options[i], out int value))
            {
                Plugin.Logger.LogWarning("Max Players options aren't plain numbers any more, leaving them alone");
                return;
            }
            if (value > Plugin.VanillaMaxPlayers)
                options.RemoveAt(i);
        }
        if (UnlimitedMode.Enabled)
        {
            for (int count = Plugin.VanillaMaxPlayers + 1; count <= Plugin.ConfigMaxPlayers.Value; count++)
                options.Add(count.ToString());
        }

        // SetGameSetupContextValues indexes the options with this, so keep it in range
        int selected = Mathf.Clamp(menu.maxPlayersOptionsSelectedIndex, 0, options.Count - 1);
        menu.maxPlayersOptionsSelectedIndex = selected;
        selector.Initialize(options, selected);
    }
}
