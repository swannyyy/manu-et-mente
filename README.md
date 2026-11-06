# Manu et Mente

A Unity strategy board game with AI opponents, same-device play, Photon-powered online play, and interactive lessons.

## Open the project

1. Install Unity **6000.5.0f1** through Unity Hub.
2. Add this repository as a project and open it. Allow Unity to restore packages and import assets.
3. Open `Assets/Scenes/MainMenu.unity` and enter Play Mode.

The source project is in `Assets/`, `Packages/`, and `ProjectSettings/`. Generated caches, exported builds, and local player preferences are excluded from Git.

## Android builds

Install Android Build Support (including its SDK, NDK, and OpenJDK) for the editor version above. In Unity Build Profiles, select Android, use the configured scene list, and build an APK or Android App Bundle. Configure release signing locally; never commit signing keys or passwords.

Online play uses Photon PUN. Review the Photon server settings and configure your own Photon application when setting up an independent deployment.

## Project notes

- `UX.md`: interface and gameplay interaction notes.
- `GRAPHICS.md`: visual implementation notes.
- `MOVE_NOTATION.md`: move notation documentation.
- `Design/`: design concepts and source artwork.
- `manu_config.yaml`: training configuration.

## Licensing and distribution

No project-wide open-source license has been selected. Bundled third-party code and assets retain their respective licenses; their inclusion here does not relicense them.

This project requires Unity, a proprietary toolchain, and is therefore not currently eligible for the official F-Droid repository.
