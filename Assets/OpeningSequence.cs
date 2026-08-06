using UnityEngine;
using DG.Tweening;

public class OpeningSequence : MonoBehaviour
{
    public CanvasGroup backgroundGroup;
    public CanvasGroup uiGroup;
    public float fadeDuration = 1.5f;

    void Awake()
    {
        ResolveReferences();
    }

    void Start()
    {
        if (backgroundGroup != null)
            backgroundGroup.alpha = 1f;

        bool introHandlesUi = FindFirstObjectByType<StartSceneIntroController>() != null;
        if (introHandlesUi || uiGroup == null)
            return;

        uiGroup.alpha = 0f;
        uiGroup.DOFade(1f, fadeDuration);
    }

    void ResolveReferences()
    {
        if (backgroundGroup == null)
        {
            var panel = GameObject.Find("Panel");
            if (panel != null)
                backgroundGroup = panel.GetComponent<CanvasGroup>();
        }

        if (uiGroup == null)
        {
            var container = GameObject.Find(" UI_Container");
            if (container == null)
                container = GameObject.Find("UI_Container");
            if (container != null)
                uiGroup = container.GetComponent<CanvasGroup>();
        }
    }
}
