using System.Collections.Generic;
using Fusion;
using Il2CppInterop.Runtime.InteropTypes;

namespace OverTheHillUnlimited.Networking;

/// <summary>Where a [Networked] property lives in the vanilla state block, from its [NetworkedWeaved(offset, words)].</summary>
internal readonly record struct WovenRange(int Offset, int Words);

/// <summary>
/// Words appended after a NetworkBehaviour's woven state block, holding larger copies of its [Capacity(4)]
/// collections. Fusion sizes the block through NetworkBehaviourUtils.GetWordCount and replicates all of it,
/// so the appended words sync like any other networked state, as long as every peer appends the same layout.
/// </summary>
internal sealed class ExpandedState
{
    // Fixed rather than configurable: all peers must agree on the state layout
    public const int Capacity = 16;

    // NetworkDictionary capacities must be prime (vanilla [Capacity(4)] is woven as 7)
    public const int DictionaryCapacity = 17;

    readonly record struct Collection(WovenRange Vanilla, int Offset, int Words);

    readonly string _name;
    readonly int _vanillaWordCount;
    readonly List<Collection> _collections = new();
    bool _loggedExpansion;
    bool _loggedMismatch;

    public int ExtraWordCount { get; private set; }

    /// <summary>The behaviour's expanded state size: vanilla words plus appended words.</summary>
    public int WordCount => _vanillaWordCount + ExtraWordCount;

    /// <param name="vanillaWordCount">From the type's [NetworkBehaviourWeaved(n)]; a mismatch at runtime means a game update changed the layout.</param>
    public ExpandedState(string name, int vanillaWordCount)
    {
        _name = name;
        _vanillaWordCount = vanillaWordCount;
    }

    /// <summary>Reserves a Capacity-slot copy of the array property at <paramref name="vanilla"/>; returns its offset.</summary>
    public int AddArray(WovenRange vanilla, int elementWords) =>
        Add(vanilla, Capacity * elementWords);

    /// <summary>Reserves a DictionaryCapacity-slot copy of the dictionary property at <paramref name="vanilla"/>; returns its offset.</summary>
    // NetworkDictionary layout: 3 meta words, then per slot one bucket word and an entry of next + key + value
    public int AddDictionary(WovenRange vanilla, int keyWords, int valueWords) =>
        Add(vanilla, 3 + DictionaryCapacity * (2 + keyWords + valueWords));

    int Add(WovenRange vanilla, int words)
    {
        int offset = ExtraWordCount;
        _collections.Add(new Collection(vanilla, offset, words));
        ExtraWordCount += words;
        return offset;
    }

    /// <summary>Call from a NetworkBehaviourUtils.GetWordCount postfix.</summary>
    public void ExpandIf<T>(NetworkBehaviour behaviour, ref int wordCount) where T : Il2CppObjectBase
    {
        if (behaviour == null || behaviour.TryCast<T>() == null)
            return;

        if (wordCount != _vanillaWordCount)
        {
            if (!_loggedMismatch)
                Plugin.Logger.LogError($"{_name} has {wordCount} state words, expected {_vanillaWordCount}. " +
                                       "The game has probably been updated, so it stays at 4 player slots.");
            _loggedMismatch = true;
            return;
        }

        wordCount += ExtraWordCount;
        if (!_loggedExpansion)
            Plugin.Logger.LogInfo($"{_name} state expanded from {_vanillaWordCount} to {wordCount} words");
        _loggedExpansion = true;
    }

    public bool IsExpanded(NetworkBehaviour behaviour) =>
        behaviour.WordCount == WordCount;

    public unsafe NetworkArray<T> Array<T>(NetworkBehaviour owner, int offset, int length, NetworkArray<T> vanilla) =>
        new((byte*)(owner.Ptr + _vanillaWordCount + offset), length, vanilla._readerWriter);

    public unsafe NetworkDictionary<K, V> Dictionary<K, V>(NetworkBehaviour owner, int offset, NetworkDictionary<K, V> vanilla) =>
        new(owner.Ptr + _vanillaWordCount + offset, DictionaryCapacity, vanilla._keyReaderWriter, vanilla._valReaderWriter);

    /// <summary>
    /// Fusion's ChangeDetector only compares each property's vanilla words, so on its own it never sees writes to
    /// the appended copies. On the state authority, this writes a hash of each appended collection into the last
    /// (now unused) vanilla word of the property it replaces, so the property reads as changed whenever its real
    /// data does. The hash replicates with the rest of the block, so clients' change detectors fire too.
    /// Call before the behaviour's Render runs its ChangeDetector.
    /// </summary>
    public unsafe void SignalChanges(NetworkBehaviour behaviour)
    {
        if (!behaviour.HasStateAuthority || !IsExpanded(behaviour))
            return;

        int* state = behaviour.Ptr;
        if (state == null)
            return;

        foreach (var collection in _collections)
        {
            int hash = Hash(state + _vanillaWordCount + collection.Offset, collection.Words);
            int* signal = state + collection.Vanilla.Offset + collection.Vanilla.Words - 1;
            if (*signal != hash)
                *signal = hash;
        }
    }

    // FNV-1a over 32-bit words
    static unsafe int Hash(int* words, int count)
    {
        uint hash = 2166136261;
        for (int i = 0; i < count; i++)
        {
            hash ^= (uint)words[i];
            hash *= 16777619;
        }
        return (int)hash;
    }
}
