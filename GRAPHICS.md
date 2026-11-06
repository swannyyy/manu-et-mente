# Graphics theme

The presentation theme activates automatically when MainMenu, Game, GameAI,
Tutorial, or Learn loads. Scene layouts, original textures, board geometry,
colliders, and game rules are preserved.

- Ivory/slate tiles with brass dividers and gentle vertical shading.
- Jade/copper piece finishes retain the original symbols and team identities.
- Dark backgrounds and panels; consistent ivory button labels and subtle shadows.
- Mint legal-move rings and segmented coral capture rings.
- Bilinear filtering on board rings and move markers smooths scaled edges.

Implementation: Assets/Scripts/GameVisualTheme.cs and
Assets/Resources/Visuals/BoardFinish.shader. Chessman.Activate and MovePlate.Start
apply the shared materials to spawned objects. Resources keeps the shader included
in player builds. Materials are shared rather than allocated per piece or marker.

Validation: runtime C# compiles and the game renders in Unity Play Mode. The UX
smoke checks exercise movement, capture, rotation and victory. See UX.md.

Before shipping, check MainMenu (including difficulty and online panels), Game,
GameAI and Tutorial in Play Mode at desktop and phone aspect ratios. Verify piece
symbols, legal/capture rings, rotating rings, and victory panels. Check a device
build to validate shader rendering on the target graphics API.

The main-menu text title has been replaced with the selected orbital monogram.
The scene references Assets/Art/Branding/OrbitalMonogram.png directly, with a
140-by-140 UI rectangle centered above the buttons. Aspect ratio is preserved,
raycasts are disabled, and the logo shares the menu panel visibility.

MenuUX now presents the logo in a responsive screen-space menu with a small
Manu et Mente caption. GameUX supplies the matching gameplay interface.

Palette refinement: saturated orange/emerald pieces, deep teal and warm cream
tiles, and stronger gold dividers. The shader decodes its authored sRGB palette
when using linear rendering, preventing the washed-out appearance. Player-facing
text calls the P piece Pawn; internal skill_* asset names are retained.
