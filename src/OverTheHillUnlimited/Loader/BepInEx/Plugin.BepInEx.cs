using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using OverTheHillUnlimited.Testing;
using LogLevel = BepInEx.Logging.LogLevel;

namespace OverTheHillUnlimited;

/// <summary>The BepInEx 6 (IL2CPP) half of the mod: BepInEx\plugins\OverTheHillUnlimited\OverTheHillUnlimited.dll.</summary>
[BepInPlugin(Id, Name, Version)]
public partial class Plugin : BasePlugin
{
    internal const string Loader = "BepInEx";

    static Plugin _instance;

    public override void Load()
    {
        _instance = this;
        Start(new BepInExLog(Log));
    }

    public override bool Unload()
    {
        Stop();
        return true;
    }

    /// <summary>Where the self-test writes its results: the BepInEx folder.</summary>
    internal static string DataDirectory => Paths.BepInExRootPath;

    internal static IModLog CreateLog(string name) => new BepInExLog(BepInEx.Logging.Logger.CreateLogSource(name));

    static void AddSelfTestRunner() => _instance.AddComponent<SelfTestRunner>();

    /// <summary>Calls back with the source and message of every error logged through BepInEx, until disposed.</summary>
    internal static IDisposable ListenForErrors(Action<string, string> onError) => new ErrorListener(onError);

    /// <summary>
    /// For the self-test: changes a setting the way something outside the mod would. BepInEx doesn't watch its config
    /// file, so that's another plugin, such as a config manager, setting it on the main thread.
    /// </summary>
    internal static void ChangeSettingFromOutside<T>(Setting<T> setting, T value) => setting.Value = value;

    static Setting<T> Bind<T>(string section, string key, T defaultValue, string description) =>
        new BepInExSetting<T>(_instance.Config.Bind(section, key, defaultValue, description));

    static Setting<int> BindRange(string section, string key, int defaultValue, int min, int max, string description) =>
        new BepInExSetting<int>(_instance.Config.Bind(section, key, defaultValue,
            new ConfigDescription(description, new AcceptableValueRange<int>(min, max))));

    sealed class BepInExLog : IModLog
    {
        readonly ManualLogSource _source;

        public BepInExLog(ManualLogSource source) => _source = source;

        public string Name => _source.SourceName;

        public void LogInfo(object data) => _source.LogInfo(data);
        public void LogMessage(object data) => _source.LogMessage(data);
        public void LogWarning(object data) => _source.LogWarning(data);
        public void LogError(object data) => _source.LogError(data);
    }

    // BepInEx saves the config file itself whenever a value is set
    sealed class BepInExSetting<T> : Setting<T>
    {
        readonly ConfigEntry<T> _entry;

        public BepInExSetting(ConfigEntry<T> entry)
        {
            _entry = entry;
            _entry.SettingChanged += OnSettingChanged;
        }

        void OnSettingChanged(object sender, EventArgs e) => RaiseChanged();

        public override T Value
        {
            get => _entry.Value;
            set => _entry.Value = value;
        }
    }

    sealed class ErrorListener : ILogListener
    {
        readonly Action<string, string> _onError;

        public ErrorListener(Action<string, string> onError)
        {
            _onError = onError;
            BepInEx.Logging.Logger.Listeners.Add(this);
        }

        public LogLevel LogLevelFilter => LogLevel.Error | LogLevel.Fatal;

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            if ((eventArgs.Level & (LogLevel.Error | LogLevel.Fatal)) != 0)
                _onError(eventArgs.Source.SourceName, eventArgs.Data?.ToString() ?? "");
        }

        public void Dispose() => BepInEx.Logging.Logger.Listeners.Remove(this);
    }
}
