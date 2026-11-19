using UnityEngine;

/// <summary>Estilos IMGUI compartidos por el HUD y los menus.</summary>
public static class UIStyles
{
    public static GUIStyle Label(int size, TextAnchor anchor, Color color,
        bool bold = false, Font font = null)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            alignment = anchor,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            wordWrap = true
        };
        if (font != null) style.font = font;
        style.normal.textColor = color;
        return style;
    }

    public static GUIStyle Button(int size, Font font = null)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontSize = size,
            fontStyle = FontStyle.Bold,
            fixedHeight = 46
        };
        if (font != null) style.font = font;
        return style;
    }

    /// <summary>Rectangulo plano para construir paneles y barras sin depender de uGUI.</summary>
    public static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
