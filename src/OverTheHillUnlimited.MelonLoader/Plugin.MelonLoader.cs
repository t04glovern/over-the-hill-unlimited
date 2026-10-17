using System;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using MelonLoader.Utils;
using OverTheHillUnlimited;
using OverTheHillUnlimited.Testing;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(Plugin), Plugin.Name, Plugin.Version, Plugin.Author,
    "https://github.com/t04glovern/over-the-hill-unlimited")]
[assembly: MelonGame("Funselektor", "over the hill")]
// The mod applies its own patches, some only on request (the self-test's, the diagnostics), so not every one in here
[assembly: HarmonyDontPatchAll]

namespace OverTheHillUnlimited;

/// <summary>The MelonLoader 0.7 half of the mod: Mods\OverTheHillUnlimited.MelonLoader.dll.</summary>
public partial class Plugin : MelonMod
{
    internal const string Loader = "MelonLoader";

    static MelonPreferences_Category _settings;
    static bool _selfTestRequested;

    public override void OnInitializeMelon() => Start(new MelonLog(LoggerInstance));

    // The self-test's runner is a Unity component, so it's added once Unity is up
    public override void OnLateInitializeMelon()
    {
        if (!_selfTestRequested)
            return;
        ClassInjector.RegisterTypeInIl2Cpp<SelfTestRunner>();
        var host = new GameObject($"{Id} self-test");
        Object.DontDestroyOnLoad(host);
        host.hideFlags = HideFlags.HideAndDontSave;
        host.AddComponent<SelfTestRunner>();
    }

    public override void OnDeinitializeMelon() => Stop();

    /// <summary>Where the self-test writes its results: MelonLoader's UserData folder.</summary>
    internal static string DataDirectory => MelonEnvironment.UserDataDirectory;

    internal static IModLog CreateLog(string name) => new MelonLog(new MelonLogger.Instance(name));

    static void AddSelfTestRunner() => _selfTestRequested = true;

    /// <summary>Calls back with the source and message of every error logged through MelonLoader, until disposed.</summary>
    internal static IDisposable ListenForErrors(Action<string, string> onError) => new ErrorListener(onError);

    // Every setting goes in one [OverTheHillUnlimited] category of UserData\MelonPreferences.cfg
    static MelonPreferences_Category Settings => _settings ??= MelonPreferences.CreateCategory(Id, Name);

    static Setting<T> Bind<T>(string section, string key, T defaultValue, string description) =>
        new MelonSetting<T>(Settings.CreateEntry(key, defaultValue, key, description), null);

    static Setting<int> BindRange(string section, string key, int defaultValue, int min, int max, string description) =>
        new MelonSetting<int>(Settings.CreateEntry(key, defaultValue, key, $"{description} From {min} to {max}."),
            value => Math.Clamp(value, min, max));

    sealed class MelonLog : IModLog
    {
        readonly MelonLogger.Instance _logger;

        public MelonLog(MelonLogger.Instance logger) => _logger = logger;

        public void LogInfo(object data) => _logger.Msg(data);
        public void LogMessage(object data) => _logger.Msg(data);
        public void LogWarning(object data) => _logger.Warning(data);
        public void LogError(object data) => _logger.Error(data);
    }

    // MelonLoader doesn't range-check values read from the file, so out-of-range ones are clamped on reading
    sealed class MelonSetting<T> : Setting<T>
    {
        readonly MelonPreferences_Entry<T> _entry;
        readonly Func<T, T> _clamp;

        public MelonSetting(MelonPreferences_Entry<T> entry, Func<T, T> clamp)
        {
            _entry = entry;
            _clamp = clamp;
            _entry.OnEntryValueChanged.Subscribe(OnValueChanged);
        }

        void OnValueChanged(T oldValue, T newValue) => RaiseChanged();

        public override T Value
        {
            get => _clamp != null ? _clamp(_entry.Value) : _entry.Value;
            set
            {
                _entry.Value = value;
                Settings.SaveToFile(false);
            }
        }
    }

    sealed class ErrorListener : IDisposable
    {
        readonly Action<string, string> _onError;

        public ErrorListener(Action<string, string> onError)
        {
            _onError = onError;
            MelonLogger.ErrorCallbackHandler += OnError;
        }

        void OnError(string source, string message) => _onError(source, message ?? "");

        public void Dispose() => MelonLogger.ErrorCallbackHandler -= OnError;
    }
}
