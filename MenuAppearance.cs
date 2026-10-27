using UnityEngine;

namespace Silverpine.ModdingTools;

// Used by Framework Status and Audio Settings. Launcher tabs and consumer
// UI skins retain their existing appearance.
internal static class MenuAppearance
{
    internal static readonly Color PanelColor = new(0.055f, 0.065f, 0.085f, 1f);

    internal static void DrawPanel(Rect bounds)
    {
        Color previous = GUI.color;
        try
        {
            GUI.color = PanelColor;
            // Do not rely on the skin's translucent window texture to hide
            // the game/menu behind the panel's contents.
            GUI.DrawTexture(bounds, Texture2D.whiteTexture, ScaleMode.StretchToFill, alphaBlend: false);
        }
        finally
        {
            GUI.color = previous;
        }
    }
}
