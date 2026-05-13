using client.Utils;
using UnityEngine;

namespace client;

public static partial class GameApi
{
    public static class Take
    {
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

        // --- Run-level effects -------------------------------------------

        /// <summary>Instantly end the current run (clean force-loss; no overflow animation).</summary>
        public static void EndGame()
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var g = CurrentGame;
                if (g == null || g.IsOver) return;
                g.IsOver = true;
                Plugin.BepinLogger.LogInfo("Game ended via API.");
            });
        }
    }
}
