<iframe frameborder="0" src="https://itch.io/embed/5329925?linkback=true" width="552" height="167"><a href="https://swanny95.itch.io/manu-et-mente">Manu et Mente by Swanny95</a></iframe>
# Manu et Mente

*Hand and Mind.* A two-player abstract strategy game on a circular, four-ring board, built in Unity. Move a piece, or rotate a pair of rings instead.

Play against a computer opponent with adjustable difficulty, against a friend on the same device, or online. Learn with guided exercises, solve puzzles, and review your games with post-game analysis and stats.

## How to play

Each side has Pawns, Scholars, Sages and two commanders, the **Hand** and the **Mind**. Tap a piece to see its legal moves: mint rings are moves, segmented coral rings are captures. Scholars and Sages can rotate their ring pair instead of moving. After three rotations in a row (by either player) the next player must move a piece; if they cannot, the game is a draw. Diagonal pieces may need an enabler deeper in the board.

Win with any one of these:

- **Hand + Mind** in the centre D ring.
- **Hand or Mind + 3 Pawns** in D.
- **Comeback win:** only after losing one commander, your last commander in D plus 4 Pawns in C/c.
- **Capture both enemy commanders** (Hand and Mind).

Leaving a game against the AI or online counts as a loss.

## Features

- Offline AI opponent with adjustable difficulty. It is a pure C# engine (negamax with alpha-beta, transposition table, quiescence search, iterative deepening) with a linear evaluation tuned by self-play and Texel tuning; difficulty sets search depth, noise and a chance of a random move.
- Same-device and Photon-powered online play.
- Guided learning exercises and a set of puzzles.
- Post-game analysis with a win-probability bar, plus player statistics.
- Settings: sound, six colour presets per team (terracotta and forest green by default), and a choice of the original symbol artwork or a geometric piece style.
- Free, with no ads.

## Open the project

1. Install Unity **6000.5.0f1** through Unity Hub.
2. Add this repository as a project and open it. Allow Unity to restore packages and import assets.
3. Open `Assets/Scenes/MainMenu.unity` and enter Play Mode.

The source project is in `Assets/`, `Packages/`, and `ProjectSettings/`. Generated caches, exported builds, and local player preferences are excluded from Git.

## Android builds

Install Android Build Support (including its SDK, NDK, and OpenJDK) for the editor version above. In Unity Build Profiles, select Android, use the configured scene list, and build an APK or Android App Bundle. Configure release signing locally; never commit signing keys or passwords.

Online play uses Photon PUN. Review the Photon server settings and configure your own Photon application when setting up an independent deployment.

## Project notes

- `Assets/Scripts/`: game rules, UI, AI engine (`ManuAI`, `AIagent.cs`), analysis, puzzles and the piece style/settings code.

- `UX.md`: interface and gameplay interaction notes.
- `GRAPHICS.md`: visual implementation notes.
- `MOVE_NOTATION.md`: move notation documentation.
- `Design/`: design concepts and source artwork.
- `manu_config.yaml`: training configuration.

## Licensing and distribution

The original code in Manu et Mente by [swannyyy](https://github.com/swannyyy) is licensed under the **Mozilla Public License 2.0 (MPL-2.0)**. See [LICENSE](LICENSE) for the full terms. This covers the original C# files in `Assets/Scripts/` and `Assets/Editor/`, and `Assets/Resources/Visuals/BoardFinish.shader`; these files carry MPL-2.0 notices.

MPL 2.0 permits commercial use. When distributing covered software, the source of MPL-covered files, including modifications, must be made available under MPL 2.0 as required by the license. Separate files may use other licenses.

Original non-code material, including artwork and documentation, remains licensed under [Creative Commons Attribution–NonCommercial 4.0 International (CC BY-NC 4.0)](https://creativecommons.org/licenses/by-nc/4.0/). See [LICENSE-CC-BY-NC-4.0.txt](LICENSE-CC-BY-NC-4.0.txt). Suggested attribution: “Manu et Mente by swannyyy — https://github.com/swannyyy/manu-et-mente — CC BY-NC 4.0.” The noncommercial restriction does not apply to the MPL-covered code.

These licenses apply only to material the project author has the right to license. Bundled third-party code, fonts, sounds, artwork, and other assets retain their respective licenses and notices, including Photon, TextMesh Pro, and Unity packages; their inclusion here does not relicense them.


