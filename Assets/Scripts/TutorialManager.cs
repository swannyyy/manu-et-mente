using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public Button backButton;
    public ScrollRect scrollRect; // Add this
    public RectTransform viewport;
    public RectTransform content;

    void Start()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(BackToMenu);
        }
        
        // Check if scrolling is needed
        CheckScrollNeeded();
    }

    void CheckScrollNeeded()
    {
        if (scrollRect != null && viewport != null && content != null)
        {
            // Wait one frame for layout to update
            StartCoroutine(CheckScrollAfterLayout());
        }
    }

    System.Collections.IEnumerator CheckScrollAfterLayout()
    {
        // Wait for layout to rebuild
        yield return new WaitForEndOfFrame();
        
        // Check if content is taller than viewport
        float viewportHeight = viewport.rect.height;
        float contentHeight = content.rect.height;
        
        Debug.Log($"Viewport height: {viewportHeight}, Content height: {contentHeight}");
        
        if (contentHeight <= viewportHeight)
        {
            // Content fits, disable scrolling
            scrollRect.vertical = false;
            scrollRect.enabled = false;
            
            // Also hide scrollbar if you have one
            if (scrollRect.verticalScrollbar != null)
            {
                scrollRect.verticalScrollbar.gameObject.SetActive(false);
            }
        }
        else
        {
            // Content doesn't fit, enable scrolling
            scrollRect.vertical = true;
            scrollRect.enabled = true;
            
            if (scrollRect.verticalScrollbar != null)
            {
                scrollRect.verticalScrollbar.gameObject.SetActive(true);
            }
        }
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}