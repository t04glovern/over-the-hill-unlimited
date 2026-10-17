using System.Collections.Generic;
using HarmonyLib;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Lets more than 4 cars winch an item recovery object (the broken-down vehicle players tow to an end point). The host
/// records the cars winched to it in its networked ItemRecoveryMissionStatus, which has 4 slots
/// (AttachedWinchSystem0..3): AddWinchedWinchSystem throws once they're full, RemoveWinchedWinchSystem throws for a car
/// it never recorded, completing the mission only detaches the 4 recorded winches, and once those 4 let go the object
/// counts as abandoned while others still tow it.
///
/// Rather than resize the networked status, the host keeps cars past the 4th in its own list per recovery point. When a
/// recorded car lets go, the first one waiting takes its slot through the game's own attach handler, so the networked
/// slots always hold 4 of the cars actually attached. On completion, the waiting cars are detached the same way the
/// game detaches the recorded ones. The status's slots are only read on the host, so clients need nothing.
/// </summary>
public class ItemRecoveryWinchPatches
{
    const int StatusSlots = 4;

    sealed class Winched
    {
        public readonly List<VehicleWinchSystem> Recorded = new();
        public readonly List<VehicleWinchSystem> Waiting = new();
    }

    static readonly Dictionary<int, Winched> _byPoint = new();

    /// <summary>How many cars are waiting for a slot at this recovery point (for the self-test).</summary>
    internal static int WaitingCount(ItemRecovery point) =>
        point != null && _byPoint.TryGetValue(point.GetInstanceID(), out var winched) ? winched.Waiting.Count : 0;

    [HarmonyPatch(typeof(ItemRecoveryNetworkManager), nameof(ItemRecoveryNetworkManager.MovableObjectAttached))]
    [HarmonyPrefix]
    static bool OnAttached(ItemRecoveryNetworkManager __instance, VehicleWinchSystem vehicleWinchSystem, NetworkObject attachedObject)
    {
        var winched = Lookup(__instance, attachedObject, create: true);
        if (winched == null || vehicleWinchSystem == null)
            return true;
        if (winched.Recorded.Count < StatusSlots)
        {
            winched.Recorded.Add(vehicleWinchSystem);
            return true;
        }
        winched.Waiting.Add(vehicleWinchSystem);
        return false;
    }

    [HarmonyPatch(typeof(ItemRecoveryNetworkManager), nameof(ItemRecoveryNetworkManager.MoveableObjectDetached))]
    [HarmonyPrefix]
    static bool OnDetached(ItemRecoveryNetworkManager __instance, VehicleWinchSystem vehicleWinchSystem, NetworkObject attachedObject)
    {
        var winched = Lookup(__instance, attachedObject, create: false);
        if (winched == null || vehicleWinchSystem == null)
            return true;
        if (Remove(winched.Waiting, vehicleWinchSystem))
            return false;
        Remove(winched.Recorded, vehicleWinchSystem);
        return true;
    }

    // Once a recorded car's slot is free, the first waiting car takes it the way it would have on attaching
    [HarmonyPatch(typeof(ItemRecoveryNetworkManager), nameof(ItemRecoveryNetworkManager.MoveableObjectDetached))]
    [HarmonyPostfix]
    static void FillFreedSlot(ItemRecoveryNetworkManager __instance, NetworkObject attachedObject)
    {
        var winched = Lookup(__instance, attachedObject, create: false);
        if (winched == null || winched.Waiting.Count == 0 || winched.Recorded.Count >= StatusSlots)
            return;
        var next = winched.Waiting[0];
        winched.Waiting.RemoveAt(0);
        __instance.MovableObjectAttached(next, attachedObject);
    }

    // Completing detaches the 4 recorded winches; detach the waiting ones too
    [HarmonyPatch(typeof(ItemRecoveryNetworkManager), nameof(ItemRecoveryNetworkManager.ItemRecoveryCompleted))]
    [HarmonyPrefix]
    static void DetachWaitingOnComplete(ItemRecoveryNetworkManager __instance, ItemRecovery itemRecoveryPoint)
    {
        DetachWaiting(__instance, itemRecoveryPoint);
    }

    // The recovery end point's own completion path goes through here on the host as well
    [HarmonyPatch(typeof(ItemRecoveryNetworkManager), nameof(ItemRecoveryNetworkManager.RPC_CompleteMissionOnPeer))]
    [HarmonyPrefix]
    static void DetachWaitingOnPeerComplete(ItemRecoveryNetworkManager __instance, NetworkObject objectToRecover)
    {
        if (__instance.HasStateAuthority && objectToRecover != null)
            DetachWaiting(__instance, __instance.GetItemRecoveryPoint(objectToRecover));
    }

    [HarmonyPatch(typeof(ItemRecoveryNetworkManager), nameof(ItemRecoveryNetworkManager.Respawn))]
    [HarmonyPrefix]
    static void ForgetOnRespawn(ItemRecovery itemRecoveryPoint)
    {
        if (itemRecoveryPoint != null)
            _byPoint.Remove(itemRecoveryPoint.GetInstanceID());
    }

    [HarmonyPatch(typeof(ItemRecoveryNetworkManager), nameof(ItemRecoveryNetworkManager.Despawned))]
    [HarmonyPrefix]
    static void ForgetAll()
    {
        _byPoint.Clear();
    }

    static void DetachWaiting(ItemRecoveryNetworkManager manager, ItemRecovery point)
    {
        if (point == null || !_byPoint.TryGetValue(point.GetInstanceID(), out var winched))
            return;
        foreach (var waiting in winched.Waiting)
        {
            if (waiting != null)
                manager.DetachWinchSystem(waiting.Id);
        }
        _byPoint.Remove(point.GetInstanceID());
    }

    // Only for recovery objects, on the host, in Unlimited mode; anything else is left to the game
    static Winched Lookup(ItemRecoveryNetworkManager manager, NetworkObject attachedObject, bool create)
    {
        if (!UnlimitedMode.Enabled || attachedObject == null || !manager.HasStateAuthority)
            return null;
        var point = manager.GetItemRecoveryPoint(attachedObject);
        if (point == null)
            return null;
        int key = point.GetInstanceID();
        if (!_byPoint.TryGetValue(key, out var winched) && create)
            _byPoint[key] = winched = new Winched();
        return winched;
    }

    static bool Remove(List<VehicleWinchSystem> list, VehicleWinchSystem winch)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].Pointer == winch.Pointer)
            {
                list.RemoveAt(i);
                return true;
            }
        }
        return false;
    }
}
