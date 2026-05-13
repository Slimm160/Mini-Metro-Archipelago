namespace client;

public static partial class MapApi
{
    public static class Take
    {
        /// <summary>Lock a specific city/mode pair.</summary>
        public static void Lock(string cityId, GameMode mode)
        {
            if (Set.Remove((cityId, mode)))
                Plugin.BepinLogger.LogInfo($"MapApi: -{cityId}/{mode}");
        }

        /// <summary>Lock a city across all four playable modes.</summary>
        public static void Lock(string cityId)
        {
            foreach (var m in PlayableModes) Lock(cityId, m);
        }

        /// <summary>Clear every unlocked pair. Useful on AP disconnect/reset.</summary>
        public static void Clear()
        {
            Set.Clear();
            Plugin.BepinLogger.LogInfo("MapApi: cleared.");
        }
    }
}
