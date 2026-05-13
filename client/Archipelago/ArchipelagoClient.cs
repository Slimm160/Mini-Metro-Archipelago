using System;
using System.Linq;
using System.Threading;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Packets;
using client.Utils;

namespace client.Archipelago;

public class ArchipelagoClient
{
    public const string APVersion = "0.6.7";
    // Must match `game = "Mini Metro"` in apworld/MiniMetro.py — the server rejects
    // the slot with "the slot is not for this game" if these disagree.
    private const string Game = "Mini Metro";

    public static bool Authenticated;
    private bool attemptingConnection;

    public static ArchipelagoData ServerData = new();
    private DeathLinkHandler DeathLinkHandler;
    private LocationHandler LocationHandler;
    private ArchipelagoSession session;

    /// <summary>
    /// call to connect to an Archipelago session. Connection info should already be set up on ServerData
    /// </summary>
    /// <returns></returns>
    public void Connect()
    {
        if (Authenticated || attemptingConnection) return;

        try
        {
            session = ArchipelagoSessionFactory.CreateSession(ServerData.Uri);
            SetupSession();
        }
        catch (Exception e)
        {
            Plugin.BepinLogger.LogError(e);
        }

        TryConnect();
    }

    /// <summary>
    /// add handlers for Archipelago events
    /// </summary>
    private void SetupSession()
    {
        session.MessageLog.OnMessageReceived += message => ArchipelagoConsole.LogMessage(message.ToString());
        session.Items.ItemReceived += OnItemReceived;
        session.Socket.ErrorReceived += OnSessionErrorReceived;
        session.Socket.SocketClosed += OnSessionSocketClosed;
    }

    /// <summary>
    /// attempt to connect to the server with our connection info
    /// </summary>
    private void TryConnect()
    {
        try
        {
            // it's safe to thread this function call but unity notoriously hates threading so do not use excessively
            ThreadPool.QueueUserWorkItem(
                _ => HandleConnectResult(
                    session.TryConnectAndLogin(
                        Game,
                        ServerData.SlotName,
                        ItemsHandlingFlags.AllItems,
                        new System.Version(APVersion),
                        password: ServerData.Password,
                        requestSlotData: ServerData.NeedSlotData
                    )));
        }
        catch (Exception e)
        {
            Plugin.BepinLogger.LogError(e);
            HandleConnectResult(new LoginFailure(e.ToString()));
            attemptingConnection = false;
        }
    }

    /// <summary>
    /// handle the connection result and do things
    /// </summary>
    /// <param name="result"></param>
    private void HandleConnectResult(LoginResult result)
    {
        string outText;
        if (result.Successful)
        {
            var success = (LoginSuccessful)result;

            ServerData.SetupSession(success.SlotData, session.RoomState.Seed);
            Authenticated = true;

            DeathLinkHandler = new(session.CreateDeathLinkService(), ServerData.SlotName, ServerData.DeathLink);
            LocationHandler = new LocationHandler(session, ServerData);
            session.Locations.CompleteLocationChecksAsync(ServerData.CheckedLocations.ToArray());

            ApplyInitialState();

            outText = $"Successfully connected to {ServerData.Uri} as {ServerData.SlotName}!";

            ArchipelagoConsole.LogMessage(outText);
        }
        else
        {
            var failure = (LoginFailure)result;
            outText = $"Failed to connect to {ServerData.Uri} as {ServerData.SlotName}.";
            outText = failure.Errors.Aggregate(outText, (current, error) => current + $"\n    {error}");

            Plugin.BepinLogger.LogError(outText);

            Authenticated = false;
            Disconnect();
        }

        ArchipelagoConsole.LogMessage(outText);
        attemptingConnection = false;
    }

    /// <summary>
    /// something went wrong, or we need to properly disconnect from the server. cleanup and re null our session
    /// </summary>
    private void Disconnect()
    {
        Plugin.BepinLogger.LogDebug("disconnecting from server...");
        DeathLinkHandler?.Dispose();
        DeathLinkHandler = null;
        LocationHandler?.Dispose();
        LocationHandler = null;
        session?.Socket.DisconnectAsync();
        session = null;
        Authenticated = false;
        TearDownState();
    }

    /// <summary>
    /// Bring the in-game world into the state implied by slot data: map unlocks gated to
    /// the configured mode and only for cities listed in <c>starting_maps</c>, plus the
    /// upgrade-picker filter narrowed to baseline locomotive types until the apworld's
    /// "<c>... - Unlock</c>" items expand it. Runs on the main thread because it mutates
    /// shared Unity state (<c>AllowedPicks</c> is consulted from the picker postfix).
    /// </summary>
    private void ApplyInitialState()
    {
        MainThreadDispatcher.Enqueue(() =>
        {
            ItemHandler.Reset();
            UnlockState.Reset();

            // Repopulate map unlock set: start from empty so a reconnect drops stale
            // entries, then add starting maps for the configured mode only.
            MapApi.State.IsActive = false;
            MapApi.Take.Clear();
            var mode = ServerData.Mode;
            foreach (string display in ServerData.StartingMaps)
            {
                string internalId = CityNames.ToInternal(display);
                if (internalId == null)
                {
                    Plugin.BepinLogger.LogWarning($"Starting map '{display}' not in CityNames bridge — skipping.");
                    continue;
                }
                MapApi.Grant.Unlock(internalId, mode);
            }
            MapApi.State.IsActive = true;

            // Hook GameStarted so we re-apply the picker filter and strip locked
            // starting inventory on every map open — SetGame clears AllowedPicks
            // and the city's setup re-adds initial assets each time, so without this
            // re-apply the filter is dead after the first map open.
            GameApi.Events.GameStarted -= OnGameStartedApplyUnlocks;
            GameApi.Events.GameStarted += OnGameStartedApplyUnlocks;

            Plugin.BepinLogger.LogInfo(
                $"AP init: mode={mode}, starting={ServerData.StartingMaps.Count}, " +
                $"unlockable={ServerData.UnlockableMaps.Count}, shards/map={ServerData.ShardsPerMap}, " +
                $"goal={ServerData.MapsToComplete}@W{ServerData.TargetWeek}.");
        });
    }

    /// <summary>
    /// Asset types the player always keeps from the city's starting inventory, even
    /// when the corresponding "<c>... - Unlock</c>" item hasn't arrived yet. Lines and
    /// water-crossings give the player something to play with on entry — the AP
    /// unlocks instead gate the upgrade picker's ability to grant *more* of them.
    /// Locomotive/Tram are here for symmetry but are also in <see cref="UnlockState"/>'s
    /// baseline.
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<AssetType> KeepInitialTypes = new()
    {
        AssetType.Line,
        AssetType.Crossing,
        AssetType.Bridge,
        AssetType.Locomotive,
        AssetType.Tram,
    };

    /// <summary>
    /// Per-map application of AP unlock state. Fires on every <see cref="GameApi.Events.GameStarted"/>
    /// (i.e., right after the city's <c>InitialAssets</c> have been added via
    /// <c>AddAsset</c>). Re-seeds <c>AllowedPicks</c> from <see cref="UnlockState"/>
    /// and permanently strips any starting inventory whose type the player hasn't
    /// unlocked AND isn't in the always-keep set (<see cref="KeepInitialTypes"/>).
    /// </summary>
    private static void OnGameStartedApplyUnlocks(Game game)
    {
        // Re-seed the picker filter directly (we're already on the main thread; the
        // dispatched LimitPicksTo would queue for next frame, leaving a one-frame
        // window where the picker is unfiltered).
        GameApi.AllowedPicks.Clear();
        foreach (var t in UnlockState.Unlocked) GameApi.AllowedPicks.Add(t);

        // Strip starting inventory for types the player hasn't unlocked AND that
        // aren't on the keep-initial list (e.g., Carriage, Interchange, Shinkansen).
        var ad = game?.AssetDatabase;
        if (ad == null) return;
        foreach (AssetType type in System.Enum.GetValues(typeof(AssetType)))
        {
            if (type == AssetType.None || type == AssetType.Count) continue;
            if (UnlockState.Contains(type) || KeepInitialTypes.Contains(type)) continue;
            int n = ad.GetAvailableAssets(type);
            if (n > 0) GameApi.Take.PermanentRemove(type, n, isInitial: true);
        }
    }

    /// <summary>Restore vanilla unlock and picker behavior when AP is no longer driving the game.</summary>
    private void TearDownState()
    {
        GameApi.Events.GameStarted -= OnGameStartedApplyUnlocks;
        MainThreadDispatcher.Enqueue(() =>
        {
            MapApi.State.IsActive = false;
            MapApi.Take.Clear();
            GameApi.Grant.ClearPickLimit();
            ItemHandler.Reset();
            UnlockState.Reset();
        });
    }

    public void SendMessage(string message)
    {
        session.Socket.SendPacketAsync(new SayPacket { Text = message });
    }

    /// <summary>
    /// we received an item so reward it here
    /// </summary>
    /// <param name="helper">item helper which we can grab our item from</param>
    private void OnItemReceived(ReceivedItemsHelper helper)
    {
        var receivedItem = helper.DequeueItem();

        if (helper.Index <= ServerData.Index) return;

        ServerData.Index++;

        ItemHandler.Handle(receivedItem);
    }

    /// <summary>
    /// something went wrong with our socket connection
    /// </summary>
    /// <param name="e">thrown exception from our socket</param>
    /// <param name="message">message received from the server</param>
    private void OnSessionErrorReceived(Exception e, string message)
    {
        Plugin.BepinLogger.LogError(e);
        ArchipelagoConsole.LogMessage(message);
    }

    /// <summary>
    /// something went wrong closing our connection. disconnect and clean up
    /// </summary>
    /// <param name="reason"></param>
    private void OnSessionSocketClosed(string reason)
    {
        Plugin.BepinLogger.LogError($"Connection to Archipelago lost: {reason}");
        Disconnect();
    }
}