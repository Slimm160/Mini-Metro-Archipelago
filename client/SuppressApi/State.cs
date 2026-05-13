namespace client;

public static partial class SuppressApi
{
    public static class State
    {
        /// <summary>When true, <c>Game.SubmitScore()</c> is no-op'd (Steam leaderboard upload skipped).</summary>
        public static bool Leaderboard { get; set; }

        /// <summary>When true, <c>Profile.CompleteAchievement(id)</c> is no-op'd (vanilla achievement awards skipped).</summary>
        public static bool Achievements { get; set; }

        /// <summary>When true, <c>Profile.SaveToCloud(...)</c> is no-op'd (cloud profile writes skipped).</summary>
        public static bool CloudSaves { get; set; }

        /// <summary>Convenience: enable all suppressions at once.</summary>
        public static void EnableAll() { Leaderboard = true; Achievements = true; CloudSaves = true; }

        /// <summary>Convenience: disable all suppressions at once.</summary>
        public static void DisableAll() { Leaderboard = false; Achievements = false; CloudSaves = false; }
    }
}
