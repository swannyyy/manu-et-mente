// SPDX-License-Identifier: MPL-2.0
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>Shared presentation for the game's scenes; leaves layout and input intact.</summary>
public static class GameVisualTheme
{
    static Material boardMaterial;
    static Material pieceMaterial;
    static Material moveMaterial;
    static Material captureMaterial;
    static readonly Color Ink = new Color(.055f, .09f, .115f, 1f);
    static readonly Color Ivory = new Color(.96f, .92f, .82f, 1f);
    static readonly Color Brass = new Color(.78f, .63f, .39f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= Apply;
        SceneManager.sceneLoaded += Apply;
        PieceStyle.Changed -= OnPieceStyleChanged;
        PieceStyle.Changed += OnPieceStyleChanged;
    }

    // The shared piece material (also used by the Settings preview).
    public static Material PieceMaterial { get { EnsureMaterials(); return pieceMaterial; } }

    // Settings changed the team colours or the piece style: update the material and every piece on the board.
    static void OnPieceStyleChanged()
    {
        EnsureMaterials();
        PieceStyle.ApplyColors(pieceMaterial);
        foreach (var piece in Object.FindObjectsByType<Chessman>(FindObjectsInactive.Exclude)) piece.ApplySprite();
    }

    public static void FinishPiece(SpriteRenderer renderer)
    {
        EnsureMaterials();
        if (renderer != null && pieceMaterial != null) renderer.sharedMaterial = pieceMaterial;
    }

    public static void FinishMarker(SpriteRenderer renderer, bool capture)
    {
        EnsureMaterials();
        if (renderer != null && moveMaterial != null)
            renderer.sharedMaterial = capture ? captureMaterial : moveMaterial;
    }

    static void EnsureMaterials()
    {
        if (boardMaterial != null) return;
        var shader = Resources.Load<Shader>("Visuals/BoardFinish");
        if (shader == null) return;
        boardMaterial = new Material(shader) { name = "Ivory and slate board", hideFlags = HideFlags.DontSave };
        pieceMaterial = new Material(shader) { name = "Jade and copper pieces", hideFlags = HideFlags.DontSave };
        pieceMaterial.SetFloat("_Piece", 1f);
        PieceStyle.ApplyColors(pieceMaterial);
        moveMaterial = new Material(shader) { name = "Legal move ring", hideFlags = HideFlags.DontSave };
        moveMaterial.SetFloat("_Marker", 1f);
        captureMaterial = new Material(moveMaterial) { name = "Capture segmented ring", hideFlags = HideFlags.DontSave };
        captureMaterial.SetFloat("_Capture", 1f);
    }

    public static void Apply(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainMenu" && scene.name != "Game" && scene.name != "GameAI" &&
            scene.name != "Tutorial" && scene.name != "Learn" && scene.name != "GuidedLearn") return;
        EnsureMaterials();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.backgroundColor = Ink;
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.sprite != null && renderer.sprite.name.StartsWith("ring") && boardMaterial != null)
                    renderer.sharedMaterial = boardMaterial;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.name.EndsWith("Panel")) image.color = new Color(.075f, .12f, .145f, .98f);
            foreach (var button in root.GetComponentsInChildren<Button>(true)) StyleButton(button);
            foreach (var rotation in root.GetComponentsInChildren<RotationButtonManager>(true))
                rotation.activeColor = new Color(.15f, .24f, .28f);
            foreach (var title in root.GetComponentsInChildren<TMP_Text>(true))
                if (title.name == "TitleText") { title.color = Brass; title.characterSpacing = 4f; }
        }
    }

    static void StyleButton(Button button)
    {
        if (button.targetGraphic == null) return;
        button.targetGraphic.color = Color.white;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = new Color(.15f, .24f, .28f);
        colors.highlightedColor = new Color(.24f, .36f, .39f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.36f, .45f, .43f);
        colors.disabledColor = new Color(.13f, .17f, .19f, .5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = .12f;
        button.colors = colors;
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) text.color = Ivory;
        foreach (var text in button.GetComponentsInChildren<Text>(true)) text.color = Ivory;
        var shadow = button.targetGraphic.GetComponent<Shadow>();
        if (shadow == null) shadow = button.targetGraphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .28f);
        shadow.effectDistance = new Vector2(0, -3);
        shadow.useGraphicAlpha = true;
    }
}
