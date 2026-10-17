namespace OverTheHillUnlimited;

/// <summary>A log source on whichever mod loader runs the mod (BepInEx or MelonLoader).</summary>
internal interface IModLog
{
    void LogInfo(object data);
    void LogMessage(object data);
    void LogWarning(object data);
    void LogError(object data);
}
