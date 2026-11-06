# UX implementation

The runtime UI is built by MenuUX and GameUX using the shared UXUI controls.
Original serialized menus remain available as source assets; Play Mode presents
the new screen-space interface. Safe-area anchors update with device rotation and
window resizing. The gameplay camera reserves room for the header and controls.

Implemented:
- Orbital logo with a small Manu et Mente caption and clear play-mode labels.
- AI difficulty descriptions, same-device play, online play, playable learning,
  rules and a saved sound preference.
- Persistent turn/AI/waiting status and both teams' positional progress.
- Selected-piece outline, name and ability; empty-space and explicit deselection.
- Solid mint move markers and segmented coral capture markers.
- Curved-arrow rotation controls for Scholars/Sages, highlighted affected ring,
  ghost destination previews, confirm/cancel, and synchronized piece/ring motion.
- Animated moves with persistent origin/destination highlights and a move caption.
- Zoom/reset, mouse-wheel zoom, pinch zoom and touch panning when magnified.
- In-game Resume, Sound, Rules, and confirmed Leave; offline AI waits while a menu
  is open. Online play continues while the menu is open.
- Victory progress and explanations based on the actual four positional wins and
  capture of both enemy commanders. Older rules/learning text was corrected.
- Five playable exercises in GuidedLearn: select, move, capture, rotate, win.
  They use the actual board, move generation, capture and victory code. Each step
  can be restarted, and practice can be exited. The scene is enabled in builds.
- Four-character online room-code entry, cancelable matchmaking, copy-code and
  copy-invitation controls in the waiting room. Copying prepares invitation text
  for the player's messaging app; it does not send messages.
- Opponent departure/disconnection keeps a visible interruption screen and blocks
  further moves. Return to menu leaves the session; no unsupported reconnection or
  online rematch is offered.

Validation:
- Runtime scripts compiled against project Unity assemblies with and without the
  PHOTON_UNITY_NETWORKING define.
- Unity Play Mode checked the real lesson moves through positional victory, menu
  logo and rules, AI pause/resume and move/rotation execution, zoom/reset, leave/cancel,
  and the interruption input gate. Rerun in Play Mode from Tools > Manu et Mente >
  Run UX smoke checks. The runner restores the preferences it temporarily changes.
- Gameplay rendering was visually inspected in Unity's Game view.
- Physical phone gestures/safe areas and a two-device Photon match still require
  device checks; the online callback and UI paths were compiled, not live matched.

Implementation files:
Assets/Scripts/MenuUX.cs, GameUX.cs, UXUI.cs and PlayableLesson.cs.
Editor smoke checks: Assets/Editor/UXSmokeCheck.cs.

Latest smoke-check result: PASS. The test accepts both legal AI moves and
rotations and checks that neither can execute through the offline pause menu.
