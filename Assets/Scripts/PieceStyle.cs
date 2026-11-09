// SPDX-License-Identifier: MPL-2.0
//
// PieceStyle — the player's choice of piece look (Settings): the artwork style (original symbols or
// the procedurally drawn geometric shapes) and the colour of each team. Saved in PlayerPrefs and
// applied live: the piece shader reads the team colours from the shared piece material, and pieces on
// the board swap their sprite when the style changes.

using System;
using System.Collections.Generic;
using UnityEngine;

public static class PieceStyle
{
    public enum Style { Symbols = 0, Geometric = 1 }

    public const string KeyStyle = "PieceStyle", KeyOrange = "PieceOrange", KeyGreen = "PieceGreen";

    // Team colour presets (sRGB). Index 0 is the default.
    public static readonly string[] OrangeNames = { "Terracotta", "Rust", "Burnt orange", "Amber", "Apricot", "Vivid orange" };
    public static readonly Color[] OrangeColors =
    {
        Hex(0xCC6B49), Hex(0xB04E2D), Hex(0xDD7A2E), Hex(0xE6A53F), Hex(0xF2B48C), Hex(0xFF5C06)
    };
    public static readonly string[] GreenNames = { "Forest", "Moss", "Sage", "Mint", "Emerald", "Vivid green" };
    public static readonly Color[] GreenColors =
    {
        Hex(0x3B7D57), Hex(0x6E8F3C), Hex(0x8DB08F), Hex(0x86D3AA), Hex(0x1FAA68), Hex(0x0DB352)
    };

    // Raised after any setting changes; GameVisualTheme refreshes the material and the pieces.
    public static event Action Changed;

    static bool loaded;
    static Style style;
    static int orangeIndex, greenIndex;
    static readonly Dictionary<int, Sprite> geometric = new Dictionary<int, Sprite>();
    static readonly Dictionary<int, Sprite> symbolic = new Dictionary<int, Sprite>();

    static Color Hex(int rgb) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f); }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            style = PlayerPrefs.GetInt(KeyStyle, 0) == 1 ? Style.Geometric : Style.Symbols;
            orangeIndex = Mathf.Clamp(PlayerPrefs.GetInt(KeyOrange, 0), 0, OrangeColors.Length - 1);
            greenIndex = Mathf.Clamp(PlayerPrefs.GetInt(KeyGreen, 0), 0, GreenColors.Length - 1);
        }
        catch (Exception) { style = Style.Symbols; orangeIndex = greenIndex = 0; }
    }

    public static Style Current { get { Load(); return style; } }
    public static int OrangeIndex { get { Load(); return orangeIndex; } }
    public static int GreenIndex { get { Load(); return greenIndex; } }
    public static Color OrangeColor { get { Load(); return OrangeColors[orangeIndex]; } }
    public static Color GreenColor { get { Load(); return GreenColors[greenIndex]; } }

    public static void SetStyle(Style s) { Load(); if (style == s) return; style = s; Save(); }
    public static void SetOrange(int index) { Load(); if (orangeIndex == index) return; orangeIndex = Mathf.Clamp(index, 0, OrangeColors.Length - 1); Save(); }
    public static void SetGreen(int index) { Load(); if (greenIndex == index) return; greenIndex = Mathf.Clamp(index, 0, GreenColors.Length - 1); Save(); }
    public static void ResetDefaults() { Load(); style = Style.Symbols; orangeIndex = greenIndex = 0; Save(); }

    static void Save()
    {
        try
        {
            PlayerPrefs.SetInt(KeyStyle, (int)style);
            PlayerPrefs.SetInt(KeyOrange, orangeIndex);
            PlayerPrefs.SetInt(KeyGreen, greenIndex);
            PlayerPrefs.Save();
        }
        catch (Exception) { }
        if (Changed != null) Changed();
    }

    // The piece shader's team colours (authored in sRGB; passed as vectors so Unity does not convert them).
    public static void ApplyColors(Material material)
    {
        if (material == null) return;
        Color o = OrangeColor, g = GreenColor;
        material.SetVector("_OrangeTeam", new Vector4(o.r, o.g, o.b, 1));
        material.SetVector("_GreenTeam", new Vector4(g.r, g.g, g.b, 1));
    }

    public static char Letter(string pieceType) { return string.IsNullOrEmpty(pieceType) ? 'P' : pieceType[0]; }

    static int Key(char letter, bool orange) { return letter * 2 + (orange ? 0 : 1); }

    // The sprite to show for a piece: the original artwork passed in, or the geometric shape if that
    // style is selected. Original artwork is remembered so the Settings preview can show it anywhere.
    public static Sprite Resolve(char letter, bool orange, Sprite original)
    {
        if (original != null) symbolic[Key(letter, orange)] = original;
        return Current == Style.Geometric ? Geometric(letter, orange) ?? original : original;
    }

    // Sprite for the preview in Settings: shows the selected style (null if the artwork is not known yet).
    public static Sprite Preview(char letter, bool orange)
    {
        if (Current == Style.Geometric) return Geometric(letter, orange);
        Sprite s; return symbolic.TryGetValue(Key(letter, orange), out s) ? s : null;
    }

    public static Sprite Geometric(char letter, bool orange)
    {
        int key = Key(letter, orange);
        Sprite sprite;
        if (geometric.TryGetValue(key, out sprite) && sprite != null) return sprite;
        const int size = GeometricGlyphs.Size;
        byte[] rgba = GeometricGlyphs.Render(letter, orange, size);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)           // texture rows run bottom-up; the glyph rows top-down
            for (int x = 0; x < size; x++)
            {
                int i = ((size - 1 - y) * size + x) * 4;
                pixels[y * size + x] = new Color32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
            }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
            name = "Geometric " + letter + (orange ? " orange" : " green")
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        // Same world size as the artwork sprites (4.37 units across), so pieces keep their scale.
        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size / 4.37f, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.DontSave;
        geometric[key] = sprite;
        return sprite;
    }
}
