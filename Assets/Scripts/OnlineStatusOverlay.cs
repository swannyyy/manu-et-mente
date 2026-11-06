// SPDX-License-Identifier: MPL-2.0
using UnityEngine;
using UnityEngine.UI;

// Shows a small banner during online play telling you whether the opponent has joined yet.
// Reads NetworkController's statics (IsOnline / MatchReady), so it needs no per-scene wiring:
// it bootstraps itself once, builds its own Canvas + Text at runtime, and stays hidden whenever
// the game isn't online (local PvP, vs-AI, tutorial, menus).
public class OnlineStatusOverlay : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<OnlineStatusOverlay>() != null) return;
        GameObject go = new GameObject("OnlineStatusOverlay");
        go.AddComponent<OnlineStatusOverlay>();
        DontDestroyOnLoad(go);
    }

    private Text label;
    private Text nameLabel;     // persistent: "<nickname> — <points> pts" for the local player (+ opponent once known)
    private GameObject canvasObject;

    // Seconds to keep the "opponent joined" confirmation on screen before hiding it.
    private const float JoinedBannerSeconds = 4f;
    private bool wasMatchReady;
    private float hideJoinedAt = -1f;

    private void Awake()
    {
        BuildUI();
        SetVisible(false);
    }

    private void Update()
    {
        if (!NetworkController.IsOnline)
        {
            SetVisible(false);
            if (nameLabel != null) nameLabel.enabled = false;
            wasMatchReady = false;
            return;
        }

        // Always show the local player's name with their points beside it during online play; append
        // the opponent's name + points once we've received them (at match end).
        if (nameLabel != null)
        {
            nameLabel.enabled = true;
            string text = PlayerProfile.Nickname + " — " + PlayerProfile.Points + " pts";
            if (!string.IsNullOrEmpty(NetworkController.OpponentNickname))
                text += "\nvs " + NetworkController.OpponentNickname + " — " + NetworkController.OpponentPoints + " pts";
            nameLabel.text = text;
        }

        if (NetworkController.MatchReady)
        {
            // First frame the opponent is present: show the confirmation, then auto-hide it so it
            // doesn't sit on top of the board for the whole match.
            if (!wasMatchReady)
            {
                wasMatchReady = true;
                hideJoinedAt = Time.unscaledTime + JoinedBannerSeconds;
            }

            if (Time.unscaledTime < hideJoinedAt)
            {
                label.text = "Opponent joined — game on!";
                label.color = new Color(0.4f, 1f, 0.5f, 1f);
                SetVisible(true);
            }
            else
            {
                SetVisible(false);
            }
        }
        else
        {
            // Waiting: keep the banner up the whole time so the player knows nobody has joined yet.
            wasMatchReady = false;
            label.text = "Waiting for opponent to join…";
            label.color = new Color(1f, 0.85f, 0.4f, 1f);
            SetVisible(true);
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasObject != null && canvasObject.activeSelf != visible)
            canvasObject.SetActive(visible);
    }

    private void BuildUI()
    {
        canvasObject = new GameObject("OnlineStatusCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // draw above the board and existing UI

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textObject = new GameObject("StatusText");
        textObject.transform.SetParent(canvasObject.transform, false);

        label = textObject.AddComponent<Text>();
        // Unity 6 removed the built-in Arial.ttf; LegacyRuntime.ttf is the supported replacement.
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 40;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        // Anchor a wide strip across the top of the screen.
        RectTransform rt = label.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta = new Vector2(0f, 90f);

        // A drop shadow so the text stays readable over the bright board.
        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);

        // Persistent name + points label, anchored top-left so it never overlaps the centered banner.
        GameObject nameObject = new GameObject("NamePointsText");
        nameObject.transform.SetParent(canvasObject.transform, false);

        nameLabel = nameObject.AddComponent<Text>();
        nameLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameLabel.fontSize = 32;
        nameLabel.fontStyle = FontStyle.Bold;
        nameLabel.alignment = TextAnchor.UpperLeft;
        nameLabel.color = Color.white;
        nameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameLabel.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform nameRt = nameLabel.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(0f, 1f);
        nameRt.pivot = new Vector2(0f, 1f);
        nameRt.anchoredPosition = new Vector2(30f, -150f);
        nameRt.sizeDelta = new Vector2(600f, 90f);

        Shadow nameShadow = nameObject.AddComponent<Shadow>();
        nameShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        nameShadow.effectDistance = new Vector2(2f, -2f);

        nameLabel.enabled = false;
    }
}
