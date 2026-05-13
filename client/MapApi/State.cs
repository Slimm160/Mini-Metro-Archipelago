using System.Collections.Generic;

namespace client;

public static partial class MapApi
{
    public static class State
    {
        /// <summary>
        /// When true, <c>CityDefinition.IsUnlocked</c> consults <see cref="MapApi"/>'s
        /// set instead of vanilla profile/achievement gates. Toggle off to restore
        /// vanilla behavior without losing the set.
        /// </summary>
        public static bool IsActive { get; set; }

        /// <summary>All currently-unlocked (cityId, mode) pairs.</summary>
        public static IReadOnlyCollection<(string cityId, GameMode mode)> All => Set;

        public static bool IsUnlocked(string cityId, GameMode mode) =>
            Set.Contains((cityId, mode));

        /// <summary>Count of unlocked modes for a city.</summary>
        public static int UnlockedModeCount(string cityId)
        {
            int n = 0;
            foreach (var m in PlayableModes)
                if (Set.Contains((cityId, m))) n++;
            return n;
        }
    }
}
