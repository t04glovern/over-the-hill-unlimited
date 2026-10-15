using Fusion;
using Il2CppInterop.Runtime.InteropTypes;

namespace OverTheHillUnlimited.Networking;

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

    readonly string _name;
    readonly int _vanillaWordCount;
    bool _loggedExpansion;
    bool _loggedMismatch;

    public int ExtraWordCount { get; private set; }

    /// <param name="vanillaWordCount">From the type's [NetworkBehaviourWeaved(n)]; a mismatch at runtime means a game update changed the layout.</param>
    public ExpandedState(string name, int vanillaWordCount)
    {
        _name = name;
        _vanillaWordCount = vanillaWordCount;
    }

    public int AddArray(int elementWords)
    {
        int offset = ExtraWordCount;
        ExtraWordCount += Capacity * elementWords;
        return offset;
    }

    // NetworkDictionary layout: 3 meta words, then per slot one bucket word and an entry of next + key + value
    public int AddDictionary(int keyWords, int valueWords)
    {
        int offset = ExtraWordCount;
        ExtraWordCount += 3 + DictionaryCapacity * (2 + keyWords + valueWords);
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
        behaviour.WordCount == _vanillaWordCount + ExtraWordCount;

    public unsafe NetworkArray<T> Array<T>(NetworkBehaviour owner, int offset, int length, NetworkArray<T> vanilla) =>
        new((byte*)(owner.Ptr + _vanillaWordCount + offset), length, vanilla._readerWriter);

    public unsafe NetworkDictionary<K, V> Dictionary<K, V>(NetworkBehaviour owner, int offset, NetworkDictionary<K, V> vanilla) =>
        new(owner.Ptr + _vanillaWordCount + offset, DictionaryCapacity, vanilla._keyReaderWriter, vanilla._valReaderWriter);
}
