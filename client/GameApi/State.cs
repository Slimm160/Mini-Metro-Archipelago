using System.Reflection;
using HarmonyLib;
using UnityEngine;
using client.Utils;

namespace client;

public static partial class GameApi
{
    public static class State
    {
        public static Game Game => CurrentGame;
        public static bool IsInGame => Game != null;

        // --- Run state ----------------------------------------------------

        public static int Score => Game?.Score ?? 0;
        public static int Week => Game?.Week ?? 0;
        public static int Day => Game?.City?.Clock?.Day ?? 0;
        public static string CityId => Game?.City?.Definition?.Id;
        public static GameMode? Mode => Game?.Mode;
        public static bool IsOver => Game?.IsOver ?? false;

        public static bool IsPaused
        {
            get => Game?.IsPaused ?? false;
            set { if (Game != null) Game.IsPaused = value; }
        }

        // --- Topology counts ---------------------------------------------

        public static int LineCount => Game?.City?.LineCount ?? 0;
        public static int MaxLineCount => Game?.City?.MaxLineCount ?? 0;
        public static int StationCount => Game?.City?.StationCount ?? 0;

        public static Line LineAt(int index)
        {
            var c = Game?.City;
            return (c == null || index < 0 || index >= c.LineCount) ? null : c.GetLine(index);
        }

        public static Station StationAt(int index)
        {
            var c = Game?.City;
            return (c == null || index < 0 || index >= c.StationCount) ? null : c.GetStation(index);
        }

        public static Line RandomLine()
        {
            var c = Game?.City;
            return (c == null || c.LineCount == 0) ? null : c.GetLine(Random.Range(0, c.LineCount));
        }

        public static Station RandomStation()
        {
            var c = Game?.City;
            return (c == null || c.StationCount == 0) ? null : c.GetStation(Random.Range(0, c.StationCount));
        }

        // --- Asset inventory ---------------------------------------------

        public static int AssetTotal(AssetType type)     => Game?.AssetDatabase?.GetTotalAssets(type) ?? 0;
        public static int AssetAvailable(AssetType type) => Game?.AssetDatabase?.GetAvailableAssets(type) ?? 0;
        public static int AssetUsed(AssetType type)      => Game?.AssetDatabase?.GetChosenAssets(type) ?? 0;

        /// <summary>The asset types currently allowed in upgrade picks (empty = vanilla pool).</summary>
        public static System.Collections.Generic.IReadOnlyCollection<AssetType> AllowedPickTypes => AllowedPicks;

        // --- Passenger counts --------------------------------------------

        public static int WaitingPeepCount
        {
            get
            {
                var c = Game?.City;
                if (c == null) return 0;
                int total = 0;
                for (int i = 0; i < c.StationCount; i++) total += c.GetStation(i).PeepCount;
                return total;
            }
        }

        public static int ImpatientPeepCount => Game?.City?.ImpatientPeepCount ?? 0;

        // --- Time controls -----------------------------------------------

        /// <summary>
        /// Advance to Monday of the week <paramref name="amount"/> weeks from now. Both the
        /// week counter and the day-of-week label update (adding raw 7×day intervals would
        /// preserve the day-of-week and leave <c>ClockWidget</c>'s cached label stale).
        /// </summary>
        public static void IncrementWeek(int amount = 1)
        {
            if (amount <= 0) return;
            MainThreadDispatcher.Enqueue(() =>
            {
                var clock = Game?.City?.Clock;
                if (clock == null) return;
                int currentDay = clock.Day;
                int newDay = (currentDay - currentDay % 7) + 7 * amount;
                clock.Time = newDay * clock.DayLength;
                Plugin.BepinLogger.LogInfo($"Week → {clock.Week} (Mon of week +{amount}).");
            });
        }

        /// <summary>
        /// Rewind to Monday of the week <paramref name="amount"/> weeks ago, floored at week 0.
        /// </summary>
        public static void DecrementWeek(int amount = 1)
        {
            if (amount <= 0) return;
            MainThreadDispatcher.Enqueue(() =>
            {
                var clock = Game?.City?.Clock;
                if (clock == null) return;
                int currentDay = clock.Day;
                int newDay = Mathf.Max(0, (currentDay - currentDay % 7) - 7 * amount);
                clock.Time = newDay * clock.DayLength;
                Plugin.BepinLogger.LogInfo($"Week → {clock.Week} (Mon of week -{amount}).");
            });
        }

        // Reflection handles for SkipUpgrade — cached at static-init.
        private static readonly FieldInfo F_GameScreens     = AccessTools.Field(typeof(Game), "screens");
        private static readonly FieldInfo F_LocoChoiceCount = AccessTools.Field(typeof(NewAssetScreen), "locomotiveChoiceCount");
        private static readonly FieldInfo F_AssetChoiceCount= AccessTools.Field(typeof(NewAssetScreen), "assetChoiceCount");
        private static readonly PropertyInfo P_AvailLoco    = AccessTools.Property(typeof(NewAssetScreen), "AvailableLocomotives");
        private static readonly MethodInfo M_BuildLoco      = AccessTools.Method(typeof(NewAssetScreen), "BuildLocomotivePanel");
        private static readonly MethodInfo M_BuildAssets    = AccessTools.Method(typeof(NewAssetScreen), "BuildAssetsPanel");

        /// <summary>
        /// "Pretend pick" — advances the upgrade picker to the next pick (or closes it if
        /// this was the last one in the session) without granting any asset. Mirrors what
        /// <c>NewAssetScreen.OnAsset</c> does after a real pick, but skips the inventory
        /// grant. Remaining picks in the same picker session stay intact and are presented
        /// in sequence; the screen closes naturally on the last skip.
        /// </summary>
        public static void SkipUpgrade()
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = Game;
                if (g == null) return;

                // No picker open — nothing to skip
                if (g.Screen != GameScreen.NewAsset)
                {
                    if (g.PendingAssetCount > 0)
                    {
                        g.PendingAssetCount--;
                        Plugin.BepinLogger.LogInfo($"Decremented pending picks ({g.PendingAssetCount} remaining).");
                    }
                    return;
                }

                if (!(F_GameScreens.GetValue(g) is UI.Screen[] screens)) return;
                int idx = (int)GameScreen.NewAsset;
                if (idx < 0 || idx >= screens.Length) return;
                if (!(screens[idx] is NewAssetScreen newAssetScreen)) return;

                int loco  = (int)F_LocoChoiceCount.GetValue(newAssetScreen);
                int asset = (int)F_AssetChoiceCount.GetValue(newAssetScreen);
                int avail = (int)P_AvailLoco.GetValue(newAssetScreen);

                if (loco > 0 && avail > 0)
                {
                    M_BuildLoco.Invoke(newAssetScreen, null);
                    Plugin.BepinLogger.LogInfo("Skipped a locomotive pick.");
                }
                else if (asset > 0)
                {
                    M_BuildAssets.Invoke(newAssetScreen, null);
                    Plugin.BepinLogger.LogInfo("Skipped an asset pick.");
                }
                else
                {
                    g.Screen = GameScreen.None;
                    Plugin.BepinLogger.LogInfo("Closed picker (last pick skipped).");
                }
            });
        }
    }
}
