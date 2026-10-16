using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Il2CppInterop.Runtime;
using UnityEngine;
using BindingFlags = Il2CppSystem.Reflection.BindingFlags;
using Object = UnityEngine.Object;

namespace OverTheHillUnlimited.Testing;

/// <summary>
/// Looks for per-player limits that live in the game's data rather than its code: every list or array field holding
/// exactly 4 entries, on every loaded component and ScriptableObject (scene objects, and prefabs and assets loaded
/// into memory). Run with 16 players in the session, anything that should have grown with the players still shows 4.
/// Most hits are innocent (a car's 4 wheels), so the report is for reading, not a pass/fail check. With the
/// -otu-selftest-probe launch option, the self-test scans at each stage and writes
/// BepInEx/OverTheHillUnlimited-probe.txt.
/// </summary>
internal sealed class FourSlotProbe
{
    public const string Flag = "-otu-selftest-probe";
    const int Suspicious = 4;

    sealed class Hit
    {
        public string Field;
        public string ElementType;
        public readonly SortedSet<string> Stages = new();
        public readonly SortedSet<string> Examples = new();
        public int Instances;
    }

    readonly Dictionary<string, Hit> _hits = new();
    readonly Dictionary<IntPtr, List<Il2CppSystem.Reflection.FieldInfo>> _fieldsByType = new();
    static readonly Il2CppSystem.Type CollectionType = Il2CppType.Of<Il2CppSystem.Collections.ICollection>();

    public void Scan(string stage)
    {
        int scanned = 0;
        foreach (var type in new[] { Il2CppType.Of<MonoBehaviour>(), Il2CppType.Of<ScriptableObject>() })
        {
            foreach (var found in Resources.FindObjectsOfTypeAll(type))
            {
                scanned++;
                try
                {
                    ScanObject(found, stage);
                }
                catch (Exception)
                {
                    // Some objects can't be read mid-teardown; they're not what this is looking for
                }
            }
        }
        Plugin.Logger.LogInfo($"Probe '{stage}': scanned {scanned} objects, {_hits.Count} fields with {Suspicious} entries so far");
    }

    void ScanObject(Object target, string stage)
    {
        var type = target.GetIl2CppType();
        foreach (var field in CandidateFields(type))
        {
            var value = field.GetValue(target);
            var collection = value?.TryCast<Il2CppSystem.Collections.ICollection>();
            if (collection == null || collection.Count != Suspicious)
                continue;

            string key = $"{field.DeclaringType.FullName}.{field.Name}";
            if (!_hits.TryGetValue(key, out var hit))
                _hits[key] = hit = new Hit { Field = key, ElementType = ElementName(field.FieldType) };
            hit.Instances++;
            hit.Stages.Add(stage);
            if (hit.Examples.Count < 3)
                hit.Examples.Add(Describe(target));
        }
    }

    // Instance list and array fields declared on game types (not Unity's or the libraries' own base classes)
    List<Il2CppSystem.Reflection.FieldInfo> CandidateFields(Il2CppSystem.Type type)
    {
        if (_fieldsByType.TryGetValue(type.Pointer, out var cached))
            return cached;

        var fields = new List<Il2CppSystem.Reflection.FieldInfo>();
        for (var t = type; t != null && !IsEngineType(t); t = t.BaseType)
        {
            foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var fieldType = field.FieldType;
                if ((fieldType.IsArray || fieldType.IsGenericType) && CollectionType.IsAssignableFrom(fieldType))
                    fields.Add(field);
            }
        }
        _fieldsByType[type.Pointer] = fields;
        return fields;
    }

    static bool IsEngineType(Il2CppSystem.Type type)
    {
        string ns = type.Namespace ?? "";
        return ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro") || ns.StartsWith("Cinemachine") ||
               ns.StartsWith("DG.Tweening") || ns.StartsWith("Fusion") || type.FullName == "System.Object";
    }

    static string ElementName(Il2CppSystem.Type type)
    {
        if (type.IsArray)
            return type.GetElementType().Name + "[]";
        var arguments = type.GetGenericArguments();
        return $"{type.Name.Split('`')[0]}<{string.Join(", ", arguments.Select(a => a.Name))}>";
    }

    static string Describe(Object target)
    {
        var component = target.TryCast<Component>();
        if (component == null)
            return $"asset '{target.name}'";
        var path = new List<string>();
        for (var t = component.transform; t != null && path.Count < 6; t = t.parent)
            path.Add(t.name);
        path.Reverse();
        return string.Join("/", path);
    }

    public void Write()
    {
        var lines = new List<string>
        {
            $"Fields holding exactly {Suspicious} entries, by stage. Most are innocent; look for anything per player.",
            ""
        };
        foreach (var hit in _hits.Values.OrderBy(h => h.Field))
        {
            lines.Add($"{hit.Field} : {hit.ElementType}  ({hit.Instances} hits; {string.Join(", ", hit.Stages)})");
            foreach (string example in hit.Examples)
                lines.Add($"    {example}");
        }
        File.WriteAllLines(Path.Combine(Paths.BepInExRootPath, "OverTheHillUnlimited-probe.txt"), lines);
    }
}
