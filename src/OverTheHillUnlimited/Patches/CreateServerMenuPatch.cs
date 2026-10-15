using HarmonyLib;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Offers player counts above 4 in the host's Create Server "Max Players" selector while Unlimited mode is on, and
/// only the vanilla 2-4 while it's off. The game itself int.Parse()s the selected entry into GameSetupContext.MaxPlayers
/// (ContextUICreateServer.SetGameSetupContextValues), and SessionSizePatches carries that value into Fusion.
/// </summary>
public class CreateServerMenuPatch
{
    // Kept so a mode switch reaches it even while its tab is hidden (FindObjectsOfType skips inactive objects)
    static ContextUICreateServer _menu;

    [HarmonyPatch(typeof(ContextUICreateServer), nameof(ContextUICreateServer.Start))]
    [HarmonyPostfix]
    static void Postfix(ContextUICreateServer __instance)
    {
        _menu = __instance;
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
        if (current == null || current.Count == 0 || selector == null)
        {
            Plugin.Logger.LogWarning("Create Server menu has no Max Players selector, leaving it alone");
            return;
        }

        int currentIndex = menu.maxPlayersOptionsSelectedIndex;
        string selectedValue = currentIndex >= 0 && currentIndex < current.Count ? current[currentIndex] : null;

        // A new list each time: the carousel keeps a reference to the list and a cached count, so editing the old one
        // in place left it out of step. Vanilla "2", "3", "4" first, then 5 and up in Unlimited mode.
        var options = new Il2CppSystem.Collections.Generic.List<string>();
        for (int i = 0; i < current.Count; i++)
        {
            if (!int.TryParse(current[i], out int value))
            {
                Plugin.Logger.LogWarning("Max Players options aren't plain numbers any more, leaving them alone");
                return;
            }
            if (value <= Plugin.VanillaMaxPlayers)
                options.Add(current[i]);
        }
        if (UnlimitedMode.Enabled)
        {
            for (int count = Plugin.VanillaMaxPlayers + 1; count <= Plugin.ConfigMaxPlayers.Value; count++)
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
