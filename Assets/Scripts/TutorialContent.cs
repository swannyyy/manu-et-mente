// SPDX-License-Identifier: MPL-2.0
using UnityEngine;
using TMPro;

public class TutorialContent : MonoBehaviour
{
    [System.Serializable]
    public class TutorialSection
    {
        public string title;
        [TextArea(5, 20)]
        public string content;
    }

    public TutorialSection[] sections;
    public GameObject sectionPrefab;
    public Transform contentParent;
    public float sectionSpacing = 50f;

    void Start()
    {
        GenerateTutorial();
    }

    void GenerateTutorial()
    {
        float yPosition = -100f; // Start below title

        foreach (TutorialSection section in sections)
        {
            // Create section container
            GameObject sectionObj = new GameObject(section.title);
            sectionObj.transform.SetParent(contentParent, false);
            
            RectTransform sectionRect = sectionObj.AddComponent<RectTransform>();
            sectionRect.anchorMin = new Vector2(0, 1);
            sectionRect.anchorMax = new Vector2(1, 1);
            sectionRect.pivot = new Vector2(0.5f, 1);
            sectionRect.anchoredPosition = new Vector2(0, yPosition);
            sectionRect.sizeDelta = new Vector2(0, 0);

            // Add title
            GameObject titleObj = new GameObject("SectionTitle");
            titleObj.transform.SetParent(sectionObj.transform, false);
            
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, 0);
            titleRect.sizeDelta = new Vector2(-40, 50);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = section.title;
            titleText.fontSize = 36;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(1f, 0.84f, 0f); // Gold
            titleText.alignment = TextAlignmentOptions.Left;

            // Add content
            GameObject contentObj = new GameObject("SectionContent");
            contentObj.transform.SetParent(sectionObj.transform, false);
            
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = new Vector2(0, -60);
            contentRect.sizeDelta = new Vector2(-40, 200); // Will auto-adjust

            TextMeshProUGUI contentText = contentObj.AddComponent<TextMeshProUGUI>();
            contentText.text = section.content;
            contentText.fontSize = 20;
            contentText.color = Color.white;
            contentText.alignment = TextAlignmentOptions.TopLeft;
            contentText.textWrappingMode = TextWrappingModes.Normal;

            // Force text to update to get preferred height
            contentText.ForceMeshUpdate();
            float contentHeight = contentText.preferredHeight;
            contentRect.sizeDelta = new Vector2(-40, contentHeight);

            // Update section height
            float totalHeight = 60 + contentHeight + 20;
            sectionRect.sizeDelta = new Vector2(0, totalHeight);

            yPosition -= (totalHeight + sectionSpacing);
        }

        // Update content panel height
        RectTransform contentPanelRect = contentParent.GetComponent<RectTransform>();
        contentPanelRect.sizeDelta = new Vector2(contentPanelRect.sizeDelta.x, Mathf.Abs(yPosition) + 100);
    }
}