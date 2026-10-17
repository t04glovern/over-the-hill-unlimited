using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using MelonLoader.Preferences;
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

    // Work for the main thread, from MelonLoader's preferences file watcher
    static readonly ConcurrentQueue<Action> _mainThreadWork = new();

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

    public override void OnUpdate()
    {
        while (_mainThreadWork.TryDequeue(out var work))
            work();
    }

    public override void OnDeinitializeMelon() => Stop();

    /// <summary>Where the self-test writes its results: MelonLoader's UserData folder.</summary>
    internal static string DataDirectory => MelonEnvironment.UserDataDirectory;

    internal static IModLog CreateLog(string name) => new MelonLog(new MelonLogger.Instance(name), name);

    static void AddSelfTestRunner() => _selfTestRequested = true;

    /// <summary>Calls back with the source and message of every error logged through MelonLoader, until disposed.</summary>
    internal static IDisposable ListenForErrors(Action<string, string> onError) => new ErrorListener(onError);

    /// <summary>
    /// For the self-test: changes a setting the way something outside the mod would. That's MelonLoader reloading an
    /// edited MelonPreferences.cfg, which its file watcher does on a thread-pool thread.
    /// </summary>
    internal static void ChangeSettingFromOutside<T>(Setting<T> setting, T value) =>
        Task.Run(() => ((MelonSetting<T>)setting).Entry.Value = value).Wait();

    // Every setting goes in one [OverTheHillUnlimited] category of UserData\MelonPreferences.cfg
    static MelonPreferences_Category Settings => _settings ??= MelonPreferences.CreateCategory(Id, Name);

    static Setting<T> Bind<T>(string section, string key, T defaultValue, string description) =>
        new MelonSetting<T>(Settings.CreateEntry(key, defaultValue, key, description));

    // MelonLoader applies the range whenever the value is set, including when it reads the file
    static Setting<int> BindRange(string section, string key, int defaultValue, int min, int max, string description) =>
        new MelonSetting<int>(Settings.CreateEntry(key, defaultValue, key, $"{description} From {min} to {max}.",
            validator: new ValueRange<int>(min, max)));

    sealed class MelonLog : IModLog
    {
        readonly MelonLogger.Instance _logger;

        public MelonLog(MelonLogger.Instance logger, string name = null)
        {
            _logger = logger;
            // MelonLoader shows a logger's name with its spaces as underscores
            Name = (name ?? Plugin.Name).Replace(' ', '_');
        }

        public string Name { get; }

        // MelonLoader's logger calls ToString() on what it's given, so a null message would throw
        public void LogInfo(object data) => _logger.Msg(Text(data));
        public void LogMessage(object data) => _logger.Msg(Text(data));
        public void LogWarning(object data) => _logger.Warning(Text(data));
        public void LogError(object data) => _logger.Error(Text(data));

        static string Text(object data) => data?.ToString() ?? "null";
    }

    // MelonLoader raises an entry's change events inside its setter, and on the file watcher's thread when the file is
    // edited, so Changed is raised from OnUpdate instead: on the main thread, once the setter is done
    sealed class MelonSetting<T> : Setting<T>
    {
        public MelonSetting(MelonPreferences_Entry<T> entry)
        {
            Entry = entry;
            Entry.OnEntryValueChanged.Subscribe(OnEntryValueChanged);
        }

        public MelonPreferences_Entry<T> Entry { get; }

        void OnEntryValueChanged(T oldValue, T newValue) => _mainThreadWork.Enqueue(RaiseChanged);

        public override T Value
        {
            get => Entry.Value;
            set
            {
                Entry.Value = value;
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
