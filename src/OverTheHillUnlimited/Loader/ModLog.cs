namespace OverTheHillUnlimited;

/// <summary>A log source on whichever mod loader runs the mod (BepInEx or MelonLoader).</summary>
internal interface IModLog
{
    /// <summary>The source name the loader reports this log's messages under.</summary>
    string Name { get; }

    void LogInfo(object data);
    void LogMessage(object data);
    void LogWarning(object data);
    void LogError(object data);
}
