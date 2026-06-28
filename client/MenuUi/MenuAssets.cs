using System.IO;
using System.Reflection;
using UnityEngine;

namespace client.MenuUi;


internal static class MenuAssets
{
    /// <summary>Atlas element name for the Archipelago menu logo; file: assets/ap_menu_logo.png.</summary>
    public const string ApLogo = "ap_menu_logo";

    private static string AssetDir =>
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".", "assets");

    public static bool EnsureLoaded(string elementName, string fileName)
    {
        if (Futile.atlasManager == null) return false;
        if (Futile.atlasManager.DoesContainAtlas(elementName)) return true;

        string path = Path.Combine(AssetDir, fileName);
        if (!File.Exists(path))
        {
            Plugin.BepinLogger.LogWarning($"Menu asset not found: {path} (keeping default icon).");
            return false;
        }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path)))
        {
            Plugin.BepinLogger.LogWarning($"Failed to decode PNG: {path}");
            return false;
        }

        Futile.atlasManager.LoadAtlasFromTexture(elementName, tex);
        return true;
    }
}
