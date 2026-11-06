using UnityEngine;
using UnityEngine.EventSystems;

// Unity's OnMouseUp callbacks fire reliably from a mouse in the Editor but are unreliable
// (often never fire) from touch input on mobile devices. This router replaces that path for
// touch: on each tap it raycasts in 2D and invokes the same public OnMouseUp() the mouse path
// uses, so piece selection and moves behave identically on desktop and phone.
//
// It bootstraps itself once at startup and survives scene loads, so no scene wiring is needed.
public class TouchInputRouter : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<TouchInputRouter>() != null) return;
        GameObject go = new GameObject("TouchInputRouter");
        go.AddComponent<TouchInputRouter>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        // We dispatch taps ourselves, so stop Unity from also synthesizing mouse events from
        // touches — otherwise OnMouseUp could fire twice on devices where simulation does work.
        Input.simulateMouseWithTouches = false;
    }

    private void LateUpdate()
    {
        if (Input.touchCount == 0)
        {
            if(Input.GetMouseButtonUp(0) && !GameUX.BlockInput &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) &&
                GameUX.Instance != null && GameUX.Instance.InBoard(Input.mousePosition))
            {
                var cam=ResolveCamera();
                if(cam!=null && Physics2D.OverlapPoint(cam.ScreenToWorldPoint(Input.mousePosition))==null)GameUX.Instance.Deselect();
            }
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Ended) return;

        // Ignore taps that land on UI (buttons, panels) so they don't also hit the board behind.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            return;

        if (GameUX.BlockInput || (GameUX.Instance != null && GameUX.Instance.ConsumeGesture)) return;
        if (GameUX.Instance != null && !GameUX.Instance.InBoard(touch.position)) return;
        DispatchTap(touch.position);
    }

    private Camera cachedCamera;

    // Camera.main only finds a camera tagged "MainCamera"; the GameAI scene's camera is Untagged,
    // so fall back to any camera in the scene. Re-resolves automatically after a scene load
    // (the destroyed camera compares == null).
    private Camera ResolveCamera()
    {
        if (cachedCamera != null) return cachedCamera;
        cachedCamera = Camera.main;
        if (cachedCamera == null) cachedCamera = FindAnyObjectByType<Camera>();
        return cachedCamera;
    }

    private void DispatchTap(Vector2 screenPosition)
    {
        Camera cam = ResolveCamera();
        if (cam == null) return;

        Vector2 worldPoint = cam.ScreenToWorldPoint(screenPosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
        if (hits == null || hits.Length == 0) { GameUX.Instance?.Deselect(); return; }

        // A move plate is the action layer drawn on top of pieces (including a capture target),
        // so if one is under the tap it always wins over the piece beneath it.
        foreach (Collider2D hit in hits)
        {
            MovePlate plate = hit.GetComponent<MovePlate>();
            if (plate != null)
            {
                plate.OnMouseUp();
                return;
            }
        }

        foreach (Collider2D hit in hits)
        {
            Chessman piece = hit.GetComponent<Chessman>();
            if (piece != null)
            {
                piece.OnMouseUp();
                return;
            }
        }
    }
}
