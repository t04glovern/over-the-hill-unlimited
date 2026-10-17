using System;

namespace OverTheHillUnlimited;

/// <summary>A setting in the mod loader's own config file (BepInEx's .cfg, or MelonLoader's MelonPreferences.cfg).</summary>
internal abstract class Setting<T>
{
    /// <summary>The current value; setting it saves the config file.</summary>
    public abstract T Value { get; set; }

    /// <summary>Raised whenever the value changes, whether from the mod or from editing the config.</summary>
    public event Action Changed;

    protected void RaiseChanged() => Changed?.Invoke();
}
