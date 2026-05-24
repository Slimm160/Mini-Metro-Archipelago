using Archipelago.MultiClient.Net.Models;

namespace client.Archipelago;

/// <summary>
/// Routes AP item-received packets into in-game effects. Names match
/// <c>apworld/data/items.json</c> exactly — any rename there must mirror here.
///
/// Design notes:
///   * The five "<c>... - Unlock</c>" items don't grant a free asset — they expand
///     the upgrade picker's allowed-type filter (<c>AllowedPicks</c>). Without an
///     unlock, the corresponding type never appears in the upgrade panel; the
///     <c>Skip</c> button rescues a player whose panel ends up empty.
///   * "<c>Progressive Map Shard</c>" is a single pooled item; each one received
///     advances the counter, and crossing a multiple of <c>ShardsPerMap</c> unlocks
///     the next city in <c>ArchipelagoData.UnlockableMaps</c> order.
///   * Counter state is rebuilt on each (re)connect via <see cref="Reset"/> — the AP
///     server replays all prior items, so the counter just runs forward from zero.
/// </summary>
public static class ItemHandler
{
    private const string ProgressiveShardName = "Progressive Map Shard";

    /// <summary>Total Progressive Map Shards received this session.</summary>
    private static int ProgressiveShardCount;

    private static int currentLimit = 0; // tracked to know how much to increase on each "New Line - Unlock"

    /// <summary>Drop shard progress so reconnect-replay rebuilds it from scratch.</summary>
    public static void Reset() => ProgressiveShardCount = 0;

    /// <summary>
    /// Record an unlocked asset type. Writes the persistent <see cref="UnlockState"/>
    /// (consulted on every GameStarted) and the live <c>AllowedPicks</c> filter
    /// (consulted by the next picker open in the current run).
    /// </summary>
    private static void Unlock(AssetType type)
    {
        UnlockState.Add(type);
        GameApi.Grant.AllowPickType(type);
    }

    private static void IncreaseLimit(AssetType type)
    {
        UnlockState.Add(type);
        currentLimit++;
        GameApi.Grant.AllowPickType(type, currentLimit);
    }

    public static void Handle(ItemInfo item)
    {
        if (item?.ItemName == null)
        {
            Plugin.BepinLogger.LogWarning("AP item with null name — skipping.");
            return;
        }

        Plugin.BepinLogger.LogInfo($"AP item: {item.ItemName} (id={item.ItemId})");

        switch (item.ItemName)
        {
            // --- Asset unlocks → upgrade picker filter ---
            // Each unlock writes to BOTH UnlockState (persistent across map opens,
            // re-applied on every GameStarted) and the live AllowedPicks via
            // AllowPickType (so a mid-run unlock takes effect on the next picker).
            case "New Line - Unlock":     IncreaseLimit(AssetType.Line); break;
            case "Interchange - Unlock":  Unlock(AssetType.Interchange); break;
            case "Shinkansen - Unlock":   Unlock(AssetType.Shinkansen); break;
            case "Tunnel/Bridge - Unlock":
                // Cities use exactly one crossing style; granting both is harmless because
                // the per-city upgrade pool only contains the relevant one.
                Unlock(AssetType.Crossing);
                Unlock(AssetType.Bridge);
                break;
            case "Carriage - Unlock":     Unlock(AssetType.Carriage); break;

            // --- Useful filler ---
            case "Extra Locomotive": GameApi.Grant.Locomotive(1); break;
            case "Extra Carriage":   GameApi.Grant.Carriage(1); break;
            case "Budget Increase":  GameApi.Grant.UpgradePick(1); break;
            case "Clear Station":    GameApi.Take.Peeps(); break;

            // --- Traps ---
            case "Rush Hour":  GameApi.Take.Overflow(); break;
            case "Renovation": GameApi.Take.AllLines(); break;
            case "Delayed":    GameApi.State.DecrementWeek(1); break;
            case "Derailed":   GameApi.Take.DeleteTrain(); break;

            // --- Progressive map unlock ---
            case ProgressiveShardName: HandleProgressiveShard(); break;

            default:
                Plugin.BepinLogger.LogWarning($"Unhandled AP item: {item.ItemName}");
                break;
        }
    }

    /// <summary>
    /// Increment the global progressive-shard counter and, if the count just crossed
    /// a multiple of <c>ShardsPerMap</c>, unlock the next city in
    /// <c>ArchipelagoData.UnlockableMaps</c> order. Each shard takes the player one
    /// step closer to the next map — the Nth (1-indexed) map unlocks at
    /// <c>N × ShardsPerMap</c>. Extras past the last map are silently no-op.
    /// </summary>
    private static void HandleProgressiveShard()
    {
        ProgressiveShardCount++;

        int perMap = ArchipelagoClient.ServerData.ShardsPerMap;
        if (perMap <= 0) perMap = 1; // defensive; should never happen
        var unlockables = ArchipelagoClient.ServerData.UnlockableMaps;

        // mapIndex = 0 means "haven't reached the first map's threshold yet";
        // 1..unlockables.Count means "just earned the Nth map".
        int mapIndex = ProgressiveShardCount / perMap;
        int progressInto = ProgressiveShardCount % perMap;

        Plugin.BepinLogger.LogInfo(
            $"Progressive shards: {ProgressiveShardCount} total " +
            $"(map {mapIndex}/{unlockables.Count}, {progressInto}/{perMap} toward next).");

        // Only unlock on the exact threshold crossing — i.e., when this shard pushed
        // the count to a multiple of perMap AND we're within the unlockable-maps range.
        if (progressInto != 0) return;
        if (mapIndex < 1 || mapIndex > unlockables.Count) return;

        string display = unlockables[mapIndex - 1];
        string internalId = CityNames.ToInternal(display);
        if (internalId == null)
        {
            Plugin.BepinLogger.LogWarning($"Progressive unlock target '{display}' not in CityNames bridge.");
            return;
        }

        MapApi.Grant.Unlock(internalId, ArchipelagoClient.ServerData.Mode);
        Plugin.BepinLogger.LogMessage(
            $"Unlocked {display} ({internalId}/{ArchipelagoClient.ServerData.Mode}) — " +
            $"map {mapIndex}/{unlockables.Count}.");
    }
}
