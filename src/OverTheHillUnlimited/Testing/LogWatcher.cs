using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace OverTheHillUnlimited.Testing;

/// <summary>
/// Collects the errors raised while a self-test step runs: exceptions the game logs to Unity's Player.log, and errors
/// logged through the mod loader (Il2CppInterop reports exceptions thrown inside patched methods there).
/// </summary>
internal sealed class LogWatcher : IDisposable
{
    static readonly Regex UnityExceptionLine = new(@"^[A-Za-z_][\w.`]*Exception: ", RegexOptions.Compiled);

    // Errors the game logs that aren't the mod's doing: level data wherever a car arrives, and each car's track-reset RPC
    // arriving before the snow and mud tracks system has set itself up in a new session
    static readonly string[] GameContentErrors = { "is not on a NavMesh", "TraxManager not yet initialized" };

    // Exceptions from the game's own races, by their first stack frame, all while quitting to the menu shuts the runner
    // down: HUDManager's ping icon coroutine reads the network runner without a null check, and since build 25992246
    // WheelTransformUpdater.Update indexes its car's per-wheel data by corner, which is already gone as the cars are
    // torn down
    static readonly string[] GameRaceFrames = { "HUDManager.UpdatePingIcon", "WheelTransformUpdater.Update" };

    readonly string _ignoredSource;
    readonly string _playerLogPath;
    readonly List<string> _loaderErrors = new();
    readonly IDisposable _listener;
    long _playerLogOffset;

    public LogWatcher(string ignoredSource)
    {
        _ignoredSource = ignoredSource;
        _playerLogPath = Application.consoleLogPath;
        _listener = Plugin.ListenForErrors(OnLoaderError);
    }

    /// <summary>Starts a new collection window.</summary>
    public void Mark()
    {
        _playerLogOffset = PlayerLogLength();
        lock (_loaderErrors)
            _loaderErrors.Clear();
    }

    /// <summary>Errors logged since the last Mark, each with its first stack frame where there is one.</summary>
    public List<string> ErrorsSinceMark()
    {
        var errors = new List<string>();
        lock (_loaderErrors)
            errors.AddRange(_loaderErrors);

        var lines = ReadPlayerLogSince(_playerLogOffset);
        for (int i = 0; i < lines.Length; i++)
        {
            if (!UnityExceptionLine.IsMatch(lines[i]))
                continue;
            string frame = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
            if (frame.StartsWith("Parameter name") && i + 2 < lines.Length)
                frame = lines[i + 2].Trim();
            if (Array.Exists(GameRaceFrames, known => frame.Contains(known)))
                continue;
            errors.Add($"Player.log: {lines[i].Trim()} {frame}");
        }
        return errors;
    }

    void OnLoaderError(string source, string message)
    {
        if (source == _ignoredSource)
            return;
        foreach (string known in GameContentErrors)
        {
            if (message.Contains(known))
                return;
        }
        // Unity's exceptions reach Player.log too, where ErrorsSinceMark reads them with their stack frame
        if (source == "Unity" && UnityExceptionLine.IsMatch(message))
            return;
        int newline = message.IndexOf('\n');
        lock (_loaderErrors)
            _loaderErrors.Add($"{Plugin.Loader} [{source}]: {(newline < 0 ? message : message[..newline])}");
    }

    public void Dispose() => _listener.Dispose();

    long PlayerLogLength()
    {
        try
        {
            return string.IsNullOrEmpty(_playerLogPath) ? 0 : new FileInfo(_playerLogPath).Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    string[] ReadPlayerLogSince(long offset)
    {
        if (string.IsNullOrEmpty(_playerLogPath))
            return Array.Empty<string>();
        try
        {
            using var stream = new FileStream(_playerLogPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (offset > stream.Length)
                offset = 0;
            stream.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd().Split('\n');
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }
}
