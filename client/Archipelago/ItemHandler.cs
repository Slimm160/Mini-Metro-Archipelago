using System.Collections.Generic;
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
///   * The shard counter is the only mutable state on this handler. <see cref="Reset"/>
///     is invoked on (re)connect so shard progress doesn't survive a disconnect —
///     the AP server replays all prior items on reconnect, which rebuilds the counts.
/// </summary>
public static class ItemHandler
{
    private const string ShardSuffix = " - Shard";

    /// <summary>City-internal-id → count of shards received so far this session.</summary>
    private static readonly Dictionary<string, int> ShardCounts = new();

    /// <summary>Drop shard progress so reconnect-replay rebuilds it from scratch.</summary>
    public static void Reset() => ShardCounts.Clear();

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
            case "New Line - Unlock":     Unlock(AssetType.Line); break;
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

            default:
                if (item.ItemName.EndsWith(ShardSuffix))
                    HandleShard(item.ItemName);
                else
                    Plugin.BepinLogger.LogWarning($"Unhandled AP item: {item.ItemName}");
                break;
        }
    }

    /// <summary>
    /// Tally a "<c>&lt;City&gt; - Shard</c>" item against the per-city counter; once the
    /// player's holdings reach <see cref="ArchipelagoData.ShardsPerMap"/>, unlock that
    /// city in the configured game mode via <see cref="MapApi.Grant.Unlock(string, GameMode)"/>.
    /// Extra shards past the threshold are silently ignored.
    /// </summary>
    private static void HandleShard(string itemName)
    {
        string display = itemName.Substring(0, itemName.Length - ShardSuffix.Length);
        string internalId = CityNames.ToInternal(display);
        if (internalId == null)
        {
            Plugin.BepinLogger.LogWarning($"Shard for unknown city: '{display}'.");
            return;
        }

        ShardCounts.TryGetValue(internalId, out int count);
        ShardCounts[internalId] = ++count;

        int threshold = ArchipelagoClient.ServerData.ShardsPerMap;
        Plugin.BepinLogger.LogInfo($"Shard {display}: {count}/{threshold}.");

        if (count == threshold)
        {
            MapApi.Grant.Unlock(internalId, ArchipelagoClient.ServerData.Mode);
            Plugin.BepinLogger.LogMessage($"Unlocked {display} ({internalId}/{ArchipelagoClient.ServerData.Mode}).");
        }
    }
}
