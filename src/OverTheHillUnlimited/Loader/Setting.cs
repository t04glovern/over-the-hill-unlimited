using System;

namespace OverTheHillUnlimited;

/// <summary>A setting in the mod loader's own config file (BepInEx's .cfg, or MelonLoader's MelonPreferences.cfg).</summary>
internal abstract class Setting<T>
{
    /// <summary>The current value; setting it saves the config file.</summary>
    public abstract T Value { get; set; }

    /// <summary>
    /// Raised on the main thread whenever the value changes, whether from the mod or from outside it (a config manager,
    /// or MelonLoader reloading an edited MelonPreferences.cfg). Handlers may set the value again.
    /// </summary>
    public event Action Changed;

    protected void RaiseChanged() => Changed?.Invoke();
}
