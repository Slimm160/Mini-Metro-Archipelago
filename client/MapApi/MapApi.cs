using System.Collections.Generic;

namespace client;

/// <summary>
/// Map-related AP gameplay surface — mirrors <see cref="GameApi"/>:
///   <c>MapApi.State</c>   — query unlock state
///   <c>MapApi.Grant</c>   — unlock cities/modes
///   <c>MapApi.Take</c>    — lock cities/modes
///
/// State is a HashSet of (cityId, mode) pairs. When
/// <see cref="State.IsActive"/> is true, the patch in this folder routes
/// <c>CityDefinition.IsUnlocked</c> through that set, ignoring vanilla
/// achievement-based unlocks.
/// </summary>
public static partial class MapApi
{
    internal static readonly HashSet<(string cityId, GameMode mode)> Set = new();

    internal static readonly GameMode[] PlayableModes =
        { GameMode.CLASSIC, GameMode.ZEN, GameMode.EXTREME, GameMode.SANDBOX };
}
