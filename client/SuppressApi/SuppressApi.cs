namespace client;

/// <summary>
/// Suppresses vanilla side effects that would pollute player progress during an AP run:
///   <c>SuppressApi.State.Leaderboard</c>  — block <c>Game.SubmitScore()</c>
///   <c>SuppressApi.State.Achievements</c> — block <c>Profile.CompleteAchievement(id)</c>
///   <c>SuppressApi.State.CloudSaves</c>   — block <c>Profile.SaveToCloud(...)</c>
///
/// All toggles default to <c>false</c> (vanilla behavior). Typical wiring: flip them
/// to <c>true</c> on AP connect, back to <c>false</c> on disconnect.
/// </summary>
public static partial class SuppressApi
{
}
