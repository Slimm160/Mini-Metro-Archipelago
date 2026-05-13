using System;

namespace client;

public static partial class GameApi
{
    public static class Events
    {
        public static event Action<Game> GameStarted;
        public static event Action<Game> GameEnded;
        public static event Action<Game, int> ScoreIncreased;
        /// <summary>Fires when <see cref="Game.StartWeek"/> runs (i.e., the upgrade picker just closed). May not fire if the picker is skipped.</summary>
        public static event Action<Game, int> WeekStarted;
        /// <summary>Fires every time <c>Game.Week</c> increments — driven by a <see cref="Game.Update"/> postfix poll. Catches clock-time changes that don't go through the picker.</summary>
        public static event Action<Game, int> WeekChanged;
        public static event Action<Game, Station> OverflowStarted;

        internal static void RaiseGameStarted(Game g) => GameStarted?.Invoke(g);
        internal static void RaiseGameEnded(Game g) => GameEnded?.Invoke(g);
        internal static void RaiseScoreIncreased(Game g, int score) => ScoreIncreased?.Invoke(g, score);
        internal static void RaiseWeekStarted(Game g, int week) => WeekStarted?.Invoke(g, week);
        internal static void RaiseWeekChanged(Game g, int week) => WeekChanged?.Invoke(g, week);
        internal static void RaiseOverflowStarted(Game g, Station s) => OverflowStarted?.Invoke(g, s);
    }
}
