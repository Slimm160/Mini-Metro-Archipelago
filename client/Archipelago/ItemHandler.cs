using Archipelago.MultiClient.Net.Models;

namespace client.Archipelago;

/// <summary>
/// Routes AP item-received packets into in-game effects.
/// Add cases here as your apworld defines new items.
/// </summary>
public static class ItemHandler
{
    public static void Handle(ItemInfo item)
    {
        Plugin.BepinLogger.LogInfo($"AP item: {item.ItemName} (id={item.ItemId})");

        switch (item.ItemName)
        {
            // Asset grants
            case "Locomotive":  GameApi.Grant.Locomotive(); break;
            case "Carriage":    GameApi.Grant.Carriage(); break;
            case "Line":        GameApi.Grant.Line(); break;
            case "Interchange": GameApi.Grant.Interchange(); break;
            case "Tunnel":
            case "Crossing":    GameApi.Grant.Crossing(); break;
            case "Bridge":      GameApi.Grant.Bridge(); break;
            case "Tram":        GameApi.Grant.Tram(); break;
            case "Shinkansen":  GameApi.Grant.Shinkansen(); break;

            // Trap items
            case "Trap: Overflow":   GameApi.Take.Overflow(); break;
            case "Trap: Lose Train": GameApi.Take.Train(); break;
            case "Trap: Lose Line":  GameApi.Take.Line(); break;

            // Map unlocks — assumes item name matches CityDatabase id
            // e.g., "Map: berlin" → MapApi.Grant.Unlock("berlin")
            default:
                if (item.ItemName != null && item.ItemName.StartsWith("Map: "))
                {
                    MapApi.Grant.Unlock(item.ItemName.Substring(5));
                }
                else
                {
                    Plugin.BepinLogger.LogWarning($"Unhandled AP item: {item.ItemName}");
                }
                break;
        }
    }
}
