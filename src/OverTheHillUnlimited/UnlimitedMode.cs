namespace OverTheHillUnlimited;

/// <summary>
/// Whether this player is in Unlimited mode, saved as ExpandPlayerSlots in the config. All patches stay applied either
/// way. The ones that change the network layout, matchmaking or session size check this, so switching it between
/// sessions moves you between modded servers (up to 16 players) and vanilla ones.
/// </summary>
internal static class UnlimitedMode
{
    public static bool Enabled => Plugin.ConfigExpandPlayerSlots.Value;

    // Networked objects keep the layout they were created with, so only switch outside a session
    public static bool CanChange => OverlandSessionPlayers.Instance == null;

    public static bool TrySetEnabled(bool enabled)
    {
        if (enabled == Enabled)
            return true;
        if (!CanChange)
        {
            Plugin.Logger.LogWarning("Can't switch Unlimited mode during a session");
            return false;
        }

        Plugin.ConfigExpandPlayerSlots.Value = enabled; // BepInEx saves the config file
        Plugin.Logger.LogInfo(enabled
            ? "Unlimited mode on: modded servers, up to 16 players"
            : "Unlimited mode off: vanilla servers, 4 players");
        return true;
    }
}
