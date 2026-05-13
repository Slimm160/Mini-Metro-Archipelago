using System;
using System.Reflection;
using client.Utils;
using HarmonyLib;
using Metro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace client;

public static partial class GameApi
{
    public static class Take
    {
        // Reflection handles for EndGame — the fields needed to replicate
        // the vanilla overflow → game-over flow live on Game as privates.
        private static readonly FieldInfo F_OvercrowdedStation = AccessTools.Field(typeof(Game), "overcrowdedStation");
        private static readonly FieldInfo F_ScheduledScreen    = AccessTools.Field(typeof(Game), "scheduledScreen");
        private static readonly FieldInfo F_Screens            = AccessTools.Field(typeof(Game), "screens");

        // Reflection handle for permanent delete — the totalAssets backing array on
        // AssetDatabase has no setter or decrement API.
        private static readonly FieldInfo F_TotalAssets = AccessTools.Field(typeof(AssetDatabase), "totalAssets");
        private static readonly AssetType[] AllAssetTypes = (AssetType[])Enum.GetValues(typeof(AssetType));


        // --- Asset inventory ---------------------------------------------

        /// <summary>Consume <paramref name="count"/> of an asset type from inventory.</summary>
        public static void Asset(AssetType type, int count = 1)
        {
            if (count <= 0) return;
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null) return;
                int removed = 0;
                for (int i = 0; i < count; i++)
                    if (g.AssetDatabase.ConsumeAsset(type)) removed++;
                    else break;
                Plugin.BepinLogger.LogInfo($"Consumed {removed}× {type}.");
            });
        }

        // --- Station effects ---------------------------------------------

        /// <summary>Trigger an overflow at a specific (or random) station.</summary>
        public static void Overflow(int stationIndex = -1)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null || g.City.StationCount == 0) return;
                var s = (stationIndex < 0 || stationIndex >= g.City.StationCount)
                    ? g.City.GetStation(Random.Range(0, g.City.StationCount))
                    : g.City.GetStation(stationIndex);
                s.TriggerBreach();
                Plugin.BepinLogger.LogInfo($"Trap: overflow at station {s.Id}.");
            });
        }

        /// <summary>Clear all waiting passengers at a specific (or random) station.</summary>
        public static void Peeps(int stationIndex = -1)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null || g.City.StationCount == 0) return;
                var s = (stationIndex < 0 || stationIndex >= g.City.StationCount)
                    ? g.City.GetStation(Random.Range(0, g.City.StationCount))
                    : g.City.GetStation(stationIndex);
                int cleared = s.PeepCount;
                while (s.PeepCount > 0) s.RemovePeep(s.GetPeep(0));
                Plugin.BepinLogger.LogInfo($"Cleared {cleared} peeps at station {s.Id}.");
            });
        }

        /// <summary>Clear all waiting passengers at every station.</summary>
        public static void AllPeeps()
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null) return;
                int total = 0;
                for (int i = 0; i < g.City.StationCount; i++)
                {
                    var s = g.City.GetStation(i);
                    total += s.PeepCount;
                    while (s.PeepCount > 0) s.RemovePeep(s.GetPeep(0));
                }
                Plugin.BepinLogger.LogInfo($"Cleared {total} peeps city-wide.");
            });
        }

        // --- Trains & lines ----------------------------------------------

        /// <summary>Remove a train from a specific (or random non-empty) line.</summary>
        public static void Train(int lineIndex = -1)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null || g.City.LineCount == 0) return;
                if (lineIndex >= 0 && lineIndex < g.City.LineCount)
                {
                    RemoveOneTrain(g.City.GetLine(lineIndex));
                    return;
                }
                for (int attempt = 0; attempt < g.City.LineCount; attempt++)
                {
                    var line = g.City.GetLine(Random.Range(0, g.City.LineCount));
                    if (line.TrainCount > 0) { RemoveOneTrain(line); return; }
                }
            });

            static void RemoveOneTrain(Line line)
            {
                if (line.TrainCount == 0) return;
                line.RemoveTrain(line.GetTrain(0));
                Plugin.BepinLogger.LogInfo($"Trap: removed a train (line trainCount now {line.TrainCount}).");
            }
        }

        /// <summary>Remove a specific (or random) line entirely.</summary>
        public static void Line(int lineIndex = -1)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null || g.City.LineCount == 0) return;
                var line = (lineIndex < 0 || lineIndex >= g.City.LineCount)
                    ? g.City.GetLine(Random.Range(0, g.City.LineCount))
                    : g.City.GetLine(lineIndex);
                g.City.RemoveLine(line);
                Plugin.BepinLogger.LogInfo("Trap: removed a line.");
            });
        }

        /// <summary>
        /// Remove a train and permanently delete the released assets — the locomotive
        /// (and any carriages) cannot be redeployed elsewhere. Vanilla <see cref="Train"/>
        /// returns assets to inventory; this version consumes them and decrements the
        /// <c>totalAssets</c> bookkeeping so they vanish entirely.
        /// </summary>
        public static void DeleteTrain(int lineIndex = -1)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null || g.City.LineCount == 0) return;
                Line target = null;
                if (lineIndex >= 0 && lineIndex < g.City.LineCount && g.City.GetLine(lineIndex).TrainCount > 0)
                    target = g.City.GetLine(lineIndex);
                else
                    for (int attempt = 0; attempt < g.City.LineCount; attempt++)
                    {
                        var l = g.City.GetLine(Random.Range(0, g.City.LineCount));
                        if (l.TrainCount > 0) { target = l; break; }
                    }
                if (target == null) return;
                DeletePermanently(g, () => target.RemoveTrain(target.GetTrain(0)));
                Plugin.BepinLogger.LogInfo("Permanently deleted a train.");
            });
        }

        /// <summary>
        /// Remove a line and permanently delete the released assets — the line slot,
        /// trains and carriages that were on it cannot be redeployed. See <see cref="DeleteTrain"/>.
        /// </summary>
        public static void DeleteLine(int lineIndex = -1)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null || g.City.LineCount == 0) return;
                var line = (lineIndex < 0 || lineIndex >= g.City.LineCount)
                    ? g.City.GetLine(Random.Range(0, g.City.LineCount))
                    : g.City.GetLine(lineIndex);
                DeletePermanently(g, () => g.City.RemoveLine(line));
                Plugin.BepinLogger.LogInfo("Permanently deleted a line.");
            });
        }

        /// <summary>
        /// Run a removal that releases assets back to inventory, then strip those
        /// released assets out of both <c>availableAssets</c> (via <c>ConsumeAsset</c>,
        /// which keeps the HUD asset panel in sync) and <c>totalAssets</c> (via
        /// reflection, since the field has no public mutator).
        /// </summary>
        private static void DeletePermanently(Game g, Action remove)
        {
            var ad = g.AssetDatabase;
            if (ad == null) { remove(); return; }

            var before = new int[AllAssetTypes.Length];
            for (int i = 0; i < AllAssetTypes.Length; i++)
                before[i] = ad.GetAvailableAssets(AllAssetTypes[i]);

            remove();

            if (!(F_TotalAssets.GetValue(ad) is int[] totals)) return;
            for (int i = 0; i < AllAssetTypes.Length; i++)
            {
                int delta = ad.GetAvailableAssets(AllAssetTypes[i]) - before[i];
                for (int j = 0; j < delta; j++) ad.ConsumeAsset(AllAssetTypes[i]);
                if (delta > 0) totals[(int)AllAssetTypes[i]] = Mathf.Max(0, totals[(int)AllAssetTypes[i]] - delta);
            }
        }

        // --- Run-level effects -------------------------------------------

        /// <summary>Instantly end the current run as a loss, routed through the same
        /// game-over wiring the vanilla overflow path uses.</summary>
        /// <remarks>
        /// Setting <c>Game.IsOver = true</c> alone crashes: <c>GameOverScreen.HandleTransitionIn</c>
        /// reads <c>game.OvercrowdedStation</c> into <c>focus</c> and later calls
        /// <c>focus.DoExplosion()</c>, so the screen needs a non-null overcrowded station
        /// and the subsystem <c>HandleGameOver</c> calls before <c>scheduledScreen</c> flips.
        /// FAQ mode is skipped to match the vanilla guard.
        /// </remarks>
        public static void EndGame()
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null || g.IsOver) return;
                if (g.Mode == GameMode.FAQ) return;
                if (g.City == null || g.City.StationCount == 0) return;
                if (g.OvercrowdedStation != null) return;

                var station = g.City.GetStation(Random.Range(0, g.City.StationCount));
                if (station == null) return;

                F_OvercrowdedStation.SetValue(g, station);
                station.AreEmbarksAllowed = false;
                g.TipSystem.HandleGameOver();
                g.LineBuilder.HandleGameOver();
                g.AssetBuilder.HandleGameOver();
                if (F_Screens.GetValue(g) is UI.Screen[] screens)
                    for (int i = 0; i < screens.Length; i++) screens[i]?.HandleGameOver();
                F_ScheduledScreen.SetValue(g, GameScreen.GameOver);

                Plugin.BepinLogger.LogInfo($"Game ended via API (forced game over at station {station.Id}).");
            });
        }
    }
}
