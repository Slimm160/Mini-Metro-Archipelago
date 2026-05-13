using client.Utils;

namespace client;

public static partial class GameApi
{
    public static class Grant
    {
        /// <summary>Add <paramref name="count"/> of an asset type to inventory. Thread-safe.</summary>
        public static void Asset(AssetType type, int count = 1, bool isInitial = false)
        {
            if (count <= 0) return;
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null) { Plugin.BepinLogger.LogWarning($"Grant.Asset({type}) ignored — no active game."); return; }
                for (int i = 0; i < count; i++) g.AssetDatabase.AddAsset(type, isInitial);
                Plugin.BepinLogger.LogInfo($"Granted {count}× {type}.");
            });
        }

        public static void Locomotive(int count = 1)  => Asset(AssetType.Locomotive, count);
        public static void Carriage(int count = 1)    => Asset(AssetType.Carriage, count);
        public static void Line(int count = 1)        => Asset(AssetType.Line, count);
        public static void Interchange(int count = 1) => Asset(AssetType.Interchange, count);
        /// <summary>Water-crossing upgrade for cities with <c>CrossingStyle.Tunnel</c> (e.g., London).</summary>
        public static void Crossing(int count = 1)    => Asset(AssetType.Crossing, count);
        /// <summary>Water-crossing upgrade for cities with <c>CrossingStyle.Bridge</c> (e.g., some Asian cities).</summary>
        public static void Bridge(int count = 1)      => Asset(AssetType.Bridge, count);
        public static void Tram(int count = 1)        => Asset(AssetType.Tram, count);
        public static void Shinkansen(int count = 1)  => Asset(AssetType.Shinkansen, count);

        /// <summary>Force the upgrade picker to open <paramref name="count"/> more times.</summary>
        public static void UpgradePick(int count = 1)
        {
            if (count <= 0) return;
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null) return;
                g.PendingAssetCount += count;
            });
        }

        /// <summary>
        /// Restrict every upgrade picker for the rest of this run to only present these
        /// asset types. Persistent — re-applied on each picker open via a Harmony prefix
        /// on <c>NewAssetScreen.HandleTransitionIn</c>. Cleared automatically on game end.
        /// Pass an empty list (or call <see cref="ClearPickLimit"/>) to return to vanilla.
        /// </summary>
        public static void LimitPicksTo(params AssetType[] types)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                AllowedPicks.Clear();
                if (types != null) foreach (var t in types) AllowedPicks.Add(t);
                Plugin.BepinLogger.LogInfo(AllowedPicks.Count == 0
                    ? "Pick limit cleared."
                    : $"Pick pool limited to: {string.Join(", ", AllowedPicks)}");
            });
        }

        /// <summary>Return the upgrade picker to its vanilla random pool.</summary>
        public static void ClearPickLimit() => LimitPicksTo();

        /// <summary>
        /// Add a single asset type to the upgrade picker's allowed set without
        /// clearing what was already there. Idempotent — calling with a type that's
        /// already permitted is a no-op. Used to drive incremental unlocks from AP.
        /// </summary>
        public static void AllowPickType(AssetType type)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                if (AllowedPicks.Add(type))
                    Plugin.BepinLogger.LogInfo($"Pick pool +{type} → {{{string.Join(", ", AllowedPicks)}}}.");
            });
        }
    }
}
