using System;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using BepInEx;
using client.Utils;

namespace client.Archipelago;

/// <summary>
/// Bridges Archipelago's death-link bounce feature with the in-game run state.
///
/// Outgoing: subscribes to <see cref="GameApi.Events.GameEnded"/> and broadcasts a
/// death-link when the local run ends — unless the end was caused by an incoming
/// death-link (echo guard).
///
/// Incoming: on receive, marshals to the Unity main thread and forces a game
/// over via <see cref="GameApi.Take.EndGame"/>.
/// </summary>
public class DeathLinkHandler : IDisposable
{
    private bool deathLinkEnabled;
    private readonly string slotName;
    private readonly DeathLinkService service;
    private bool suppressNextOutgoing;

    public DeathLinkHandler(DeathLinkService deathLinkService, string name, bool enableDeathLink = false)
    {
        service = deathLinkService;
        slotName = name;
        deathLinkEnabled = enableDeathLink;

        service.OnDeathLinkReceived += DeathLinkReceived;
        GameApi.Events.GameEnded += OnGameEnded;

        if (deathLinkEnabled) service.EnableDeathLink();
    }

    public void Dispose()
    {
        service.OnDeathLinkReceived -= DeathLinkReceived;
        GameApi.Events.GameEnded -= OnGameEnded;
        if (deathLinkEnabled) service.DisableDeathLink();
    }

    /// <summary>Toggle death-link participation at runtime.</summary>
    public void ToggleDeathLink()
    {
        deathLinkEnabled = !deathLinkEnabled;
        if (deathLinkEnabled) service.EnableDeathLink();
        else service.DisableDeathLink();
    }

    /// <summary>Server delivered a death — kill us on the next main-thread tick.</summary>
    private void DeathLinkReceived(DeathLink deathLink)
    {
        Plugin.BepinLogger.LogMessage(deathLink.Cause.IsNullOrWhiteSpace()
            ? $"Received Death Link from: {deathLink.Source}"
            : deathLink.Cause);

        MainThreadDispatcher.Enqueue(() =>
        {
            if (!GameApi.State.IsInGame || GameApi.State.IsOver) return;
            suppressNextOutgoing = true;
            GameApi.Take.EndGame();
        });
    }

    /// <summary>
    /// Local run ended — broadcast a death only if this was an actual game-over
    /// (<see cref="Game.IsOver"/>), not a quit-to-menu. <c>GameEnded</c> fires from
    /// <c>ClearGame</c>, which also runs when the player exits voluntarily, so
    /// without this gate every quit would send a deathlink. Also suppressed when
    /// the local end was itself caused by an incoming deathlink (echo guard).
    /// </summary>
    private void OnGameEnded(Game game)
    {
        if (suppressNextOutgoing) { suppressNextOutgoing = false; return; }
        if (game == null || !game.IsOver) return;
        SendDeathLink();
    }

    private void SendDeathLink()
    {
        if (!deathLinkEnabled) return;

        try
        {
            Plugin.BepinLogger.LogMessage("sharing your death...");
            service.SendDeathLink(new DeathLink(slotName));
        }
        catch (Exception e)
        {
            Plugin.BepinLogger.LogError(e);
        }
    }
}
