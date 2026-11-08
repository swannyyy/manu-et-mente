// SPDX-License-Identifier: MPL-2.0
//
// SettingsUX — the Settings screen (main menu and in-game menu): sound, piece style (original
// symbols or geometric shapes) and the colour of each team. Changes apply immediately and are saved
// (PieceStyle). A preview strip shows every piece type on a light and a dark square.

using System;
using UnityEngine;
using UnityEngine.UI;

public static class SettingsUX
{
    const string Order = "PASTWHB";
    static readonly Color Cream = new Color(.95f, .83f, .58f), Teal = new Color(.055f, .19f, .24f);

    // parent: the panel to open on top of; onClose runs after Back.
    public static RectTransform Show(Transform parent, Action onClose = null) { return Build(parent, onClose); }

    static RectTransform Build(Transform parent, Action onClose)
    {
        RectTransform p = null;
        Action refresh = () => { UnityEngine.Object.Destroy(p.gameObject); Build(parent, onClose); };
        p = UXUI.Modal(parent, "Settings");

        float y = 105;
        UXUI.Button(p, "Sound: " + (AudioListener.volume > 0 ? "On" : "Off"), y, () => { UXSettings.ToggleSound(); refresh(); });
        y += 88;

        Header(p, "Piece style", y); y += 44;
        var symbols = UXUI.Button(p, "Symbols", y, () => { PieceStyle.SetStyle(PieceStyle.Style.Symbols); refresh(); }, PieceStyle.Current == PieceStyle.Style.Symbols);
        var shapes = UXUI.Button(p, "Geometric", y, () => { PieceStyle.SetStyle(PieceStyle.Style.Geometric); refresh(); }, PieceStyle.Current == PieceStyle.Style.Geometric);
        Half(symbols, y, true); Half(shapes, y, false);
        y += 86;

        Header(p, "Orange pieces · " + PieceStyle.OrangeNames[PieceStyle.OrangeIndex], y); y += 44;
        Swatches(p, y, PieceStyle.OrangeColors, PieceStyle.OrangeIndex, i => { PieceStyle.SetOrange(i); refresh(); });
        y += 82;
        Header(p, "Green pieces · " + PieceStyle.GreenNames[PieceStyle.GreenIndex], y); y += 44;
        Swatches(p, y, PieceStyle.GreenColors, PieceStyle.GreenIndex, i => { PieceStyle.SetGreen(i); refresh(); });
        y += 82;

        Header(p, "Preview", y); y += 44;
        Preview(p, y, true); y += 82;
        Preview(p, y, false); y += 92;

        UXUI.Button(p, "Reset to defaults", y, () => { PieceStyle.ResetDefaults(); refresh(); });
        y += 82;
        UXUI.Button(p, "Back", y, () => { UnityEngine.Object.Destroy(p.gameObject); if (onClose != null) onClose(); }, true);
        return p;
    }

    static void Header(Transform parent, string text, float y)
    {
        var t = UXUI.Label(parent, text, y, 40, 25);
        t.color = new Color(.95f, .78f, .45f);
        t.alignment = TextAnchor.MiddleLeft;
    }

    // Two buttons side by side on one row.
    static void Half(Button button, float y, bool left)
    {
        var r = (RectTransform)button.transform;
        r.anchorMin = new Vector2(left ? 0 : .5f, 1); r.anchorMax = new Vector2(left ? .5f : 1, 1);
        r.offsetMin = new Vector2(left ? 24 : 6, -y - 66); r.offsetMax = new Vector2(left ? -6 : -24, -y);
    }

    // A row of colour chips; the selected one has a light border.
    static void Swatches(Transform parent, float y, Color[] colors, int selected, Action<int> choose)
    {
        var row = UXUI.Panel(parent, "Swatches", Color.clear);
        UXUI.Place(row, y, 70);
        int n = colors.Length;
        for (int i = 0; i < n; i++)
        {
            int index = i;
            var chip = UXUI.Panel(row, "Colour " + i, i == selected ? UXUI.Ivory : new Color(0, 0, 0, .01f));
            chip.anchorMin = new Vector2((float)i / n, 0); chip.anchorMax = new Vector2((float)(i + 1) / n, 1);
            chip.offsetMin = new Vector2(3, 0); chip.offsetMax = new Vector2(-3, 0);
            var fill = UXUI.Panel(chip, "Fill", colors[i]);
            fill.offsetMin = new Vector2(5, 5); fill.offsetMax = new Vector2(-5, -5);
            fill.GetComponent<Image>().raycastTarget = false;
            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip.GetComponent<Image>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { if (SoundManager.Instance != null) SoundManager.Instance.PlayButtonClick(); choose(index); });
        }
    }

    // One piece of each type on alternating light and dark squares, drawn with the live piece material.
    static void Preview(Transform parent, float y, bool orange)
    {
        var row = UXUI.Panel(parent, "Preview row", Color.clear);
        UXUI.Place(row, y, 76);
        int n = Order.Length;
        Material material = GameVisualTheme.PieceMaterial;
        for (int i = 0; i < n; i++)
        {
            var cell = UXUI.Panel(row, "Cell " + i, (i % 2 == 0) == orange ? Cream : Teal);
            cell.anchorMin = new Vector2((float)i / n, 0); cell.anchorMax = new Vector2((float)(i + 1) / n, 1);
            cell.offsetMin = Vector2.zero; cell.offsetMax = Vector2.zero;
            cell.GetComponent<Image>().raycastTarget = false;

            Sprite sprite = PieceStyle.Preview(Order[i], orange);
            if (sprite != null)
            {
                var go = new GameObject("Piece", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(cell, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(4, 4); rect.offsetMax = new Vector2(-4, -4);
                var image = go.GetComponent<Image>();
                image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
                if (material != null) image.material = material;
            }
            else
            {
                // Original artwork is only known once a game has been opened; show the piece's letter.
                var label = UXUI.Label(cell, Order[i].ToString(), 0, 76, 26);
                label.color = (i % 2 == 0) == orange ? Teal : Cream;
            }
        }
    }
}
