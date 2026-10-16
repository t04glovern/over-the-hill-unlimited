using System;
using HarmonyLib;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Frames every car in the lobby. The game's lobby camera is a fixed, nearly level, narrow (25°) shot of the 4 vanilla
/// spawn points, so it can't see the rows PerPlayerTablePatches adds behind them. When the lobby camera turns on in a
/// session for more than 4, this raises it to look down over the rows from the same direction, aimed at their middle,
/// and backs it off until every car the session can hold fits. Sessions for 4 or fewer keep the game's own shot.
///
/// The lobby opens before Fusion knows the session's size, so the camera is framed again each time the lobby updates its
/// cars, which it does right after opening and whenever someone joins or leaves. That runs before the game places the
/// cars' name labels, so they follow the camera.
/// </summary>
public class LobbyCameraPatches
{
    // Degrees above the cars the camera looks down from
    const float Elevation = 30f;

    // How much of the view's width and height the cars may fill
    const float FillWidth = 0.85f;
    const float FillHeight = 0.7f;

    // Roughly a car: half its width, its height, and half its length
    static readonly Vector3 CarSize = new(1.2f, 2.2f, 2.5f);

    // The game's shot, so each framing starts from it rather than from the last one
    static int _cameraId;
    static Vector3 _gamePosition;
    static Quaternion _gameRotation;
    static int _framedCars;

    // StateLobbyMenu.Open turns the lobby camera on (it inlines ActivateCamera, so patching that does nothing)
    [HarmonyPatch(typeof(StateLobbyMenu), nameof(StateLobbyMenu.Open))]
    [HarmonyPostfix]
    static void FrameOnOpen(StateLobbyMenu __instance)
    {
        FrameEveryCar(__instance);
    }

    [HarmonyPatch(typeof(StateLobbyMenu), nameof(StateLobbyMenu.UpdateCarsStatus))]
    [HarmonyPrefix]
    static void FrameOnUpdate(StateLobbyMenu __instance)
    {
        FrameEveryCar(__instance);
    }

    // Starts from the game's own shot each time, so framing again never compounds
    static void FrameEveryCar(StateLobbyMenu lobbyMenu)
    {
        var lobby = lobbyMenu.lobby;
        var camera = lobby != null ? lobby.lobbyCamera : null;
        if (camera == null)
            return;

        var cameraTransform = camera.transform;
        if (_cameraId != camera.GetInstanceID())
        {
            _cameraId = camera.GetInstanceID();
            _gamePosition = cameraTransform.position;
            _gameRotation = cameraTransform.rotation;
        }
        cameraTransform.SetPositionAndRotation(_gamePosition, _gameRotation);

        int cars = SessionSize();
        if (!UnlimitedMode.Enabled || cars <= Plugin.VanillaMaxPlayers)
        {
            _framedCars = 0;
            return;
        }
        PerPlayerTablePatches.PadLobbySpawnPoints(lobby);
        var spawnPoints = lobby.spawnPoints;
        cars = Math.Min(cars, spawnPoints.Count);

        // Aim at the middle of the cars, from the game's direction but higher up
        var centre = Vector3.zero;
        for (int i = 0; i < cars; i++)
            centre += spawnPoints[i].transform.position;
        centre = centre / cars + Vector3.up * (CarSize.y / 2);
        var away = Vector3.ProjectOnPlane(_gamePosition - centre, Vector3.up).normalized;
        float elevation = Elevation * Mathf.Deg2Rad;
        var direction = away * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation);
        var rotation = Quaternion.LookRotation(-direction, Vector3.up);

        // Back off until every corner of every car is inside the view. Moving the camera back by d along its view
        // direction adds d to each point's depth and leaves its offset from the centre of the view alone.
        float tanV = Mathf.Tan(camera.m_Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * Screen.width / Mathf.Max(1, Screen.height);
        var viewRight = rotation * Vector3.right;
        var viewUp = rotation * Vector3.up;
        var viewForward = rotation * Vector3.forward;
        float distance = 0;
        for (int i = 0; i < cars; i++)
        {
            var spawn = spawnPoints[i].transform;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = spawn.position
                            + spawn.right * ((corner & 1) == 0 ? -CarSize.x : CarSize.x)
                            + spawn.up * ((corner & 2) == 0 ? 0 : CarSize.y)
                            + spawn.forward * ((corner & 4) == 0 ? -CarSize.z : CarSize.z)
                            - centre;
                float depth = Vector3.Dot(point, viewForward);
                distance = Math.Max(distance, Math.Max(
                    Mathf.Abs(Vector3.Dot(point, viewRight)) / (tanH * FillWidth) - depth,
                    Mathf.Abs(Vector3.Dot(point, viewUp)) / (tanV * FillHeight) - depth));
            }
        }

        cameraTransform.SetPositionAndRotation(centre + direction * distance, rotation);
        if (_framedCars != cars)
            Plugin.Logger.LogInfo($"Lobby camera raised to frame {cars} cars, {distance:F0} m from them");
        _framedCars = cars;
    }

    // The most players the session can hold, which is how many cars the lobby can end up with
    static int SessionSize()
    {
        var overland = OverlandNetworkRunner.Instance;
        var runner = overland != null ? overland.NetworkRunner : null;
        var session = runner != null ? runner.SessionInfo : null;
        return session != null && session.IsValid ? session.MaxPlayers : 0;
    }
}
