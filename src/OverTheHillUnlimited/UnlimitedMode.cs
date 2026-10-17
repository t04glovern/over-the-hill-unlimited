using OverTheHillUnlimited.Networking;

namespace OverTheHillUnlimited;

/// <summary>
/// Whether this player is in Unlimited mode, saved as ExpandPlayerSlots in the config. All patches stay applied either
/// way and check Enabled, so switching it between sessions moves you between modded servers (up to 16 players) and
/// vanilla ones. It's only available while the game's networked layout is the one the mod was built against. After a
/// game update that changes it, the whole mod stays vanilla, so every player on that version falls back the same way.
/// </summary>
internal static class UnlimitedMode
{
    // The setting as of the last change made outside a session
    static bool _saved;
    static bool _reverting;

    /// <summary>Whether the game's networked layout matches the mod's, checked once at startup.</summary>
    public static bool Available { get; private set; }

    public static bool Enabled => Available && Plugin.ConfigExpandPlayerSlots.Value;

    /// <summary>Networked objects keep the layout they were created with, so the mode only changes outside a session.</summary>
    public static bool CanChange => Available && !InSession;

    // From when the runner joins the session's Photon room, which is before OverlandSessionPlayers spawns, until the
    // session's objects are gone. (The server browser's lobby runner already counts as starting or running, so those
    // states can't tell a lobby from a session; the game's own "in game" check uses IsInSession too.)
    static bool InSession
    {
        get
        {
            if (OverlandSessionPlayers.Instance != null)
                return true;
            var overland = OverlandNetworkRunner.Instance;
            var runner = overland != null ? overland.NetworkRunner : null;
            return runner != null && runner.IsInSession;
        }
    }

    /// <summary>Checks the game's networked layout and starts guarding the setting. Call once at startup.</summary>
    public static void Init(params ExpandedState[] layouts)
    {
        _saved = Plugin.ConfigExpandPlayerSlots.Value;
        Plugin.ConfigExpandPlayerSlots.Changed += OnSettingChanged;

        foreach (var layout in layouts)
        {
            if (!layout.MatchesGame(out string problem))
            {
                Plugin.Logger.LogError($"{problem}. The game has probably been updated, so Unlimited mode is off " +
                                       "until the mod is updated too, and you play vanilla (4 players).");
                return;
            }
        }
        Available = true;
    }

    public static bool TrySetEnabled(bool enabled)
    {
        if (enabled == Enabled)
            return true;
        if (!CanChange)
        {
            Plugin.Logger.LogWarning(Available
                ? "Can't switch Unlimited mode during a session"
                : "Unlimited mode is unavailable: this mod version doesn't match the game's networked layout");
            return false;
        }

        Plugin.ConfigExpandPlayerSlots.Value = enabled; // the mod loader saves the config file
        Plugin.Logger.LogInfo(enabled
            ? $"Unlimited mode on: modded servers, up to {ExpandedState.Capacity} players"
            : $"Unlimited mode off: vanilla servers, {Plugin.VanillaMaxPlayers} players");
        return true;
    }

    /// <summary>For the self-test, right before the game quits: saves the setting even in the middle of a session.</summary>
    public static void SaveForNextLaunch(bool enabled)
    {
        _saved = enabled;
        Plugin.ConfigExpandPlayerSlots.Value = enabled;
    }

    // Changes from outside the mod, such as a config manager, would give objects spawned later in the session a
    // different layout from the other players' copies. Undo them until the session ends.
    static void OnSettingChanged()
    {
        bool value = Plugin.ConfigExpandPlayerSlots.Value;
        if (_reverting || value == _saved)
            return;
        if (!InSession)
        {
            _saved = value;
            return;
        }

        Plugin.Logger.LogWarning("Unlimited mode can't change during a session, so the setting goes back");
        _reverting = true;
        try
        {
            Plugin.ConfigExpandPlayerSlots.Value = _saved;
        }
        finally
        {
            _reverting = false;
        }
    }
}
