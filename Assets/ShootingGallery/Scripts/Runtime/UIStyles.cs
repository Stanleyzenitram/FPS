using UnityEngine;

/// <summary>Estilos IMGUI compartidos por el HUD y los menus.</summary>
public static class UIStyles
{
    public static GUIStyle Label(int size, TextAnchor anchor, Color color, bool bold = false)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            alignment = anchor,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            wordWrap = true
        };
        style.normal.textColor = color;
        return style;
    }

    public static GUIStyle Button(int size)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontSize = size,
            fontStyle = FontStyle.Bold,
            fixedHeight = 46
        };
        return style;
    }
}
