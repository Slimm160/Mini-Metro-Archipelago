namespace client;

public static partial class MapApi
{
    public static class Grant
    {
        /// <summary>Unlock a specific city/mode pair.</summary>
        public static void Unlock(string cityId, GameMode mode)
        {
            if (string.IsNullOrEmpty(cityId)) return;
            if (Set.Add((cityId, mode)))
                Plugin.BepinLogger.LogInfo($"MapApi: +{cityId}/{mode}");
        }

        /// <summary>Unlock a city across all four playable modes.</summary>
        public static void Unlock(string cityId)
        {
            foreach (var m in PlayableModes) Unlock(cityId, m);
        }

        /// <summary>Unlock every city in CityDatabase across all playable modes.</summary>
        public static void UnlockAll()
        {
            var db = CityDatabase.Instance;
            for (int i = 0; i < db.Count; i++) Unlock(db[i].Id);
        }
    }
}
