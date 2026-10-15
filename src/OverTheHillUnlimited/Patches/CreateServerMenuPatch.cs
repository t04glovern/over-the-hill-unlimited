using HarmonyLib;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Adds player counts above 4 to the host's Create Server "Max Players" selector. The game itself int.Parse()s
/// the selected entry into GameSetupContext.MaxPlayers (ContextUICreateServer.SetGameSetupContextValues), and
/// SessionSizePatches carries that value into Fusion.
/// </summary>
public class CreateServerMenuPatch
{
    [HarmonyPatch(typeof(ContextUICreateServer), nameof(ContextUICreateServer.Start))]
    [HarmonyPostfix]
    static void Postfix(ContextUICreateServer __instance)
    {
        var options = __instance.maxPlayersOptionsRange;
        var selector = __instance.MaxPlayersOptionSelector;
        if (options == null || selector == null)
        {
            Plugin.Logger.LogWarning("Create Server menu has no Max Players selector, leaving it alone");
            return;
        }

        // Vanilla offers "2", "3", "4"
        int highest = 0;
        for (int i = 0; i < options.Count; i++)
        {
            if (int.TryParse(options[i], out int value) && value > highest)
                highest = value;
        }
        if (highest == 0)
        {
            Plugin.Logger.LogWarning("Max Players options aren't plain numbers any more, leaving them alone");
            return;
        }

        int target = Plugin.ConfigMaxPlayers.Value;
        if (target <= highest)
            return;

        for (int count = highest + 1; count <= target; count++)
            options.Add(count.ToString());
        selector.Initialize(options, __instance.maxPlayersOptionsSelectedIndex);
    }
}
