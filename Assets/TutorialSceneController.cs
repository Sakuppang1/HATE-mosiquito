using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// TutorialScene：按页展示教程图与配文，淡入淡出；点击可翻页（最后一页可显示开始按钮）。
/// </summary>
[ExecuteAlways]
public class TutorialSceneController : MonoBehaviour
{
    [Serializable]
    public class TutorialPage
    {
        [Tooltip("留空则从 imageResourcesPath 按页序号加载")]
        public Sprite image;

        [TextArea(2, 6)]
        public string caption;

        [Tooltip("Resources 路径（不含扩展名），例如 Tutorial/教程1")]
        public string imageResourcesPath;

        [Tooltip("图集内子 Sprite 名；留空则取最大的一张；若仍显示不全可改用整张图见 useFullTexture")]
        public string spriteName;

        [Tooltip("为 true 时使用整张 PNG（适合图集只切了一页时仍想显示全图）")]
        public bool useFullTexture;

        [Tooltip("为 true 时不显示图片，仅在屏幕中间显示 caption 文字")]
        public bool textOnlyCenter;

        [Tooltip("为 true 时进入真实玩法演示（需场景中有 GameplayRoot）")]
        public bool interactiveDemo;
    }

    [SerializeField] TutorialPage[] pages =
    {
        new TutorialPage
        {
            imageResourcesPath = "Tutorial/Tutorial1",
            spriteName = "教程1_1",
            caption = "夏天到了，烦人的蚊子又出现了\n他们常常躲在重音词块的后面，让我们来消灭它"
        },
        new TutorialPage
        {
            textOnlyCenter = true,
            caption = "在重音词块到达中间判定区域时，请发出达到一定分贝的声音，即可得分，青蛙会吃掉蚊子。\n如果触发了非重音词块则会扣分"
        },
        new TutorialPage
        {
            interactiveDemo = true,
            caption = "听发音，等重音块 PU 到中间判定区时大声喊！"
        }
    };

    [Header("淡入淡出")]
    [SerializeField] float fadeDuration = 0.65f;

    [Header("UI（留空则运行时/菜单生成）")]
    [SerializeField] CanvasGroup pageGroup;
    [SerializeField] CanvasGroup contentFadeGroup;
    [SerializeField] RectTransform imageFrameRect;
    [SerializeField] Image pageImage;
    [SerializeField] RectTransform captionRect;
    [SerializeField] Text captionText;
    [SerializeField] RectTransform centerTextRect;
    [SerializeField] Text centerBodyText;
    [SerializeField] GameObject startButtonRoot;

    [Header("图片区域（屏幕比例 0~1；勾选 Scene 布局后请在 Scene 里拖 TutorialImageFrame）")]
    [SerializeField] bool useSceneRectLayout;
    [SerializeField] Vector2 imageAnchorMin = new Vector2(0.04f, 0.22f);
    [SerializeField] Vector2 imageAnchorMax = new Vector2(0.96f, 0.92f);
    [SerializeField] Vector2 imageOffsetMin = Vector2.zero;
    [SerializeField] Vector2 imageOffsetMax = Vector2.zero;

    [Header("配文区域")]
    [SerializeField] Vector2 captionAnchorMin = new Vector2(0.06f, 0.04f);
    [SerializeField] Vector2 captionAnchorMax = new Vector2(0.94f, 0.2f);

    [Header("纯文字页（屏幕中间）")]
    [SerializeField] Vector2 centerTextAnchorMin = new Vector2(0.08f, 0.28f);
    [SerializeField] Vector2 centerTextAnchorMax = new Vector2(0.92f, 0.78f);
    [SerializeField] int centerTextFontSize = 36;

    [Header("图片显示")]
    [Tooltip("勾选：等比例完整显示（可能留边）；取消：拉伸填满框（可能变形）")]
    [SerializeField] bool preserveImageAspect = true;

    [Header("场景")]
    [SerializeField] string gameSceneName = "GameScene";
    [SerializeField] GameplayRoot gameplayRoot;
    [SerializeField] bool buildRuntimeUiIfNeeded = true;
    [SerializeField] int canvasSortOrder = 100;
    [SerializeField] int demoHintCanvasSortOrder = 200;
    [SerializeField] int startOverlayCanvasSortOrder = 300;

    int _pageIndex;
    bool _isTransitioning;
    bool _inDemoMode;
    Tweener _fadeTween;
    static Sprite s_whiteSprite;
    CanvasGroup _demoHintGroup;
    Text _demoHintText;
    CanvasGroup _startOverlayGroup;
    bool _startLoadTriggered;
    GameObject _tutorialCanvasRoot;

    void Awake()
    {
        EnsureWhiteSprite();
        EnsureBlackCamera();
        EnsureUiEventSystem();
        if (buildRuntimeUiIfNeeded && pageGroup == null)
            BuildRuntimeUi();
        EnsureFadeStructure();
        ApplyLayout();
    }

    void Start()
    {
        if (!Application.isPlaying)
            return;

        TryResolveGameplayRoot();
        if (gameplayRoot != null)
            gameplayRoot.Hide();

        if (startButtonRoot != null)
            startButtonRoot.SetActive(false);

        EnsureFadeStructure();
        if (pageGroup != null)
            pageGroup.alpha = 1f;
        if (GetFadeGroup() != null)
            GetFadeGroup().alpha = 0f;

        _pageIndex = 0;
        StartCoroutine(ShowPageWithFade(_pageIndex));
    }

    void OnDestroy()
    {
        if (!Application.isPlaying)
            return;
        _fadeTween?.Kill();
        if (gameplayRoot != null)
            gameplayRoot.DemoCompleted -= OnDemoCompleted;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureWhiteSprite();
        ApplyLayout();
    }

    [ContextMenu("生成教程 UI 到场景（保存场景后可在 Scene 视图拖动调节）")]
    public void EditorBuildUiToScene()
    {
        if (pageGroup != null)
            ClearBuiltUi();
        BuildRuntimeUi();
        buildRuntimeUiIfNeeded = false;
        useSceneRectLayout = true;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    void Update()
    {
        if (!Application.isPlaying || _isTransitioning || !WasPrimaryClickThisFrame())
            return;

        if (_inDemoMode)
            return;

        if (_startOverlayGroup != null && _startOverlayGroup.gameObject.activeSelf)
        {
            OnStartGameClicked();
            return;
        }

        if (startButtonRoot != null && startButtonRoot.activeSelf)
        {
            OnStartGameClicked();
            return;
        }

        if (_pageIndex < pages.Length - 1)
        {
            _pageIndex++;
            StartCoroutine(ShowPageWithFade(_pageIndex));
            return;
        }

        if (startButtonRoot != null && !startButtonRoot.activeSelf)
            StartCoroutine(ShowStartButtonWithFade());
    }

    void EnsureWhiteSprite()
    {
        if (s_whiteSprite != null)
            return;
        var t = Texture2D.whiteTexture;
        s_whiteSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
    }

    public void ApplyLayout()
    {
        if (useSceneRectLayout)
            return;

        if (imageFrameRect != null)
            ApplyAnchoredRect(imageFrameRect, imageAnchorMin, imageAnchorMax, imageOffsetMin, imageOffsetMax);

        if (captionRect != null)
            ApplyAnchoredRect(captionRect, captionAnchorMin, captionAnchorMax, Vector2.zero, Vector2.zero);

        if (centerTextRect != null)
            ApplyAnchoredRect(centerTextRect, centerTextAnchorMin, centerTextAnchorMax, Vector2.zero, Vector2.zero);

        if (centerBodyText != null)
            UiFontHelper.ApplyTo(centerBodyText, centerTextFontSize);

        if (pageImage != null)
            pageImage.preserveAspect = preserveImageAspect;
    }

    static void ApplyAnchoredRect(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rt == null)
            return;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    IEnumerator ShowPageWithFade(int index)
    {
        _isTransitioning = true;

        if (pages != null && index >= 0 && index < pages.Length && pages[index].interactiveDemo)
        {
            yield return StartInteractiveDemo(index);
            _isTransitioning = false;
            yield break;
        }

        if (pageGroup == null)
        {
            ApplyPageContent(index);
            _isTransitioning = false;
            yield break;
        }

        _fadeTween?.Kill();
        GetFadeGroup().interactable = false;
        GetFadeGroup().blocksRaycasts = false;

        if (GetFadeGroup().alpha > 0.01f)
            yield return FadeGroupTo(0f);

        ApplyPageContent(index);
        yield return FadeGroupTo(1f);

        GetFadeGroup().interactable = true;
        GetFadeGroup().blocksRaycasts = true;
        _isTransitioning = false;
    }

    IEnumerator FadeGroupTo(float targetAlpha)
    {
        var group = GetFadeGroup();
        if (group == null)
            yield break;

        float duration = Mathf.Max(0.05f, fadeDuration);
        bool done = false;
        _fadeTween = group
            .DOFade(targetAlpha, duration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => done = true);
        while (!done)
            yield return null;
    }

    IEnumerator ShowStartButtonWithFade()
    {
        _isTransitioning = true;
        _inDemoMode = false;
        HideDemoHint();

        EnsureStartOverlayUi();
        if (_startOverlayGroup != null)
        {
            _startOverlayGroup.gameObject.SetActive(true);
            _startOverlayGroup.interactable = true;
            _startOverlayGroup.blocksRaycasts = true;
            _startOverlayGroup.alpha = 0f;
            bool done = false;
            _startOverlayGroup.DOFade(1f, fadeDuration).OnComplete(() => done = true);
            while (!done)
                yield return null;
        }
        else if (startButtonRoot != null)
        {
            startButtonRoot.SetActive(true);
            var cg = startButtonRoot.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = startButtonRoot.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            bool done = false;
            cg.DOFade(1f, fadeDuration).OnComplete(() => done = true);
            while (!done)
                yield return null;
        }

        _isTransitioning = false;
    }

    bool TryResolveGameplayRoot()
    {
        if (gameplayRoot != null)
            return true;

        gameplayRoot = FindFirstObjectByType<GameplayRoot>(FindObjectsInactive.Include);
        if (gameplayRoot != null)
            return true;

        var prefab = Resources.Load<GameObject>("Prefabs/GameplayRoot");
        if (prefab == null)
            return false;

        var instance = Instantiate(prefab);
        instance.name = "GameplayRoot";
        gameplayRoot = instance.GetComponent<GameplayRoot>();
        return gameplayRoot != null;
    }

    IEnumerator StartInteractiveDemo(int index)
    {
        TryResolveGameplayRoot();

        if (gameplayRoot == null)
        {
            Debug.LogError("TutorialSceneController: 未找到 GameplayRoot，请运行菜单 HATE → Build GameplayRoot Prefab And Wire Tutorial。");
            yield break;
        }

        if (pageGroup != null)
        {
            _fadeTween?.Kill();
            pageGroup.interactable = false;
            pageGroup.blocksRaycasts = false;
            var fade = GetFadeGroup();
            if (fade != null && fade.alpha > 0.01f)
                yield return FadeGroupTo(0f);
        }

        HideTutorialPageUi();

        EnsureDemoHintUi();
        var hint = pages != null && index >= 0 && index < pages.Length ? pages[index].caption : string.Empty;
        ShowDemoHint(hint);

        _inDemoMode = true;
        gameplayRoot.DemoCompleted -= OnDemoCompleted;
        gameplayRoot.DemoCompleted += OnDemoCompleted;
        gameplayRoot.BeginTutorialDemo();
    }

    void OnDemoCompleted()
    {
        StartCoroutine(ShowStartButtonWithFade());
    }

    void EnsureStartOverlayUi()
    {
        if (_startOverlayGroup != null)
            return;

        var canvasGo = new GameObject("StartOverlayCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = startOverlayCanvasSortOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var overlayGo = new GameObject("StartOverlay", typeof(RectTransform), typeof(CanvasGroup));
        overlayGo.transform.SetParent(canvasGo.transform, false);
        StretchFull(overlayGo.GetComponent<RectTransform>());
        _startOverlayGroup = overlayGo.GetComponent<CanvasGroup>();

        var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimGo.transform.SetParent(overlayGo.transform, false);
        StretchFull(dimGo.GetComponent<RectTransform>());
        var dimImg = dimGo.GetComponent<Image>();
        dimImg.sprite = s_whiteSprite;
        dimImg.color = new Color(0.02f, 0.03f, 0.06f, 0.5f);
        dimImg.raycastTarget = true;

        var startGo = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        startGo.transform.SetParent(overlayGo.transform, false);
        var startRt = startGo.GetComponent<RectTransform>();
        startRt.anchorMin = startRt.anchorMax = new Vector2(0.5f, 0.5f);
        startRt.pivot = new Vector2(0.5f, 0.5f);
        startRt.anchoredPosition = Vector2.zero;
        startRt.sizeDelta = new Vector2(320f, 88f);
        startGo.GetComponent<Image>().sprite = s_whiteSprite;
        startGo.GetComponent<Image>().color = new Color(0.25f, 0.72f, 0.45f, 1f);
        startGo.GetComponent<Button>().onClick.AddListener(OnStartGameClicked);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(startGo.transform, false);
        StretchFull(labelGo.GetComponent<RectTransform>());
        var label = labelGo.AddComponent<Text>();
        label.text = "Start";
        UiFontHelper.ApplyTo(label, 40);
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        _startOverlayGroup.alpha = 0f;
        overlayGo.SetActive(false);
    }

    void EnsureDemoHintUi()
    {
        if (_demoHintGroup != null)
            return;

        var canvasGo = new GameObject("DemoHintCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = demoHintCanvasSortOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("DemoHintPanel", typeof(RectTransform), typeof(CanvasGroup));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.08f, 0.82f);
        panelRt.anchorMax = new Vector2(0.92f, 0.96f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        _demoHintGroup = panelGo.GetComponent<CanvasGroup>();

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(panelGo.transform, false);
        StretchFull(bgGo.GetComponent<RectTransform>());
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite = s_whiteSprite;
        bgImg.color = new Color(0.05f, 0.06f, 0.1f, 0.82f);
        bgImg.raycastTarget = false;

        var textGo = new GameObject("HintText", typeof(RectTransform));
        textGo.transform.SetParent(panelGo.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>());
        _demoHintText = textGo.AddComponent<Text>();
        UiFontHelper.ApplyTo(_demoHintText, 30);
        _demoHintText.alignment = TextAnchor.MiddleCenter;
        _demoHintText.color = new Color(0.96f, 0.96f, 0.98f, 1f);
        _demoHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _demoHintText.verticalOverflow = VerticalWrapMode.Overflow;
        _demoHintText.raycastTarget = false;

        _demoHintGroup.alpha = 0f;
        panelGo.SetActive(false);
    }

    void ShowDemoHint(string message)
    {
        EnsureDemoHintUi();
        if (_demoHintText != null)
            _demoHintText.text = message ?? string.Empty;
        if (_demoHintGroup == null)
            return;

        _demoHintGroup.gameObject.SetActive(true);
        _demoHintGroup.DOKill();
        _demoHintGroup.DOFade(1f, 0.35f);
    }

    void HideDemoHint()
    {
        if (_demoHintGroup == null)
            return;
        _demoHintGroup.DOKill();
        _demoHintGroup.alpha = 0f;
        _demoHintGroup.gameObject.SetActive(false);
    }

    void ApplyPageContent(int index)
    {
        if (pages == null || index < 0 || index >= pages.Length)
            return;

        var page = pages[index];

        if (page.interactiveDemo)
            return;

        bool textOnly = page.textOnlyCenter;
        if (imageFrameRect != null)
            imageFrameRect.gameObject.SetActive(!textOnly);
        if (captionRect != null)
            captionRect.gameObject.SetActive(!textOnly);
        if (centerTextRect != null)
            centerTextRect.gameObject.SetActive(textOnly);

        if (textOnly)
        {
            if (pageImage != null)
                pageImage.enabled = false;
            if (centerBodyText != null)
            {
                centerBodyText.text = page.caption ?? string.Empty;
                UiFontHelper.ApplyTo(centerBodyText, centerTextFontSize);
            }

            if (captionText != null)
                captionText.text = string.Empty;
            return;
        }

        if (pageImage != null)
        {
            var sprite = ResolveSprite(page);
            pageImage.sprite = sprite;
            pageImage.preserveAspect = preserveImageAspect;
            pageImage.color = Color.white;
            pageImage.enabled = true;
            if (sprite == null)
                Debug.LogWarning("TutorialSceneController: 第 " + (index + 1) + " 页教程图未加载。");
        }

        if (captionText != null)
            captionText.text = page.caption ?? string.Empty;

        if (centerBodyText != null)
            centerBodyText.text = string.Empty;
    }

    Sprite ResolveSprite(TutorialPage page)
    {
        if (page.image != null && !page.useFullTexture)
            return page.image;

        if (string.IsNullOrEmpty(page.imageResourcesPath))
            return page.image;

        if (page.useFullTexture)
        {
            var full = TryLoadFullTextureSprite(page.imageResourcesPath);
            if (full != null)
                return full;
        }

        var pathsToTry = new[] { page.imageResourcesPath, "Tutorial/Tutorial1", "Tutorial/教程1" };
        foreach (var path in pathsToTry)
        {
            if (string.IsNullOrEmpty(path))
                continue;
            var sprite = TryLoadSpriteAtPath(path, page.spriteName);
            if (sprite != null)
                return sprite;
        }

        return page.image;
    }

    static Sprite TryLoadFullTextureSprite(string resourcesPath)
    {
        var tex = Resources.Load<Texture2D>(resourcesPath);
        if (tex == null)
            return null;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite TryLoadSpriteAtPath(string resourcesPath, string preferredSpriteName)
    {
        if (!string.IsNullOrEmpty(preferredSpriteName))
        {
            var all = Resources.LoadAll<Sprite>(resourcesPath);
            if (all != null && all.Length > 0)
            {
                foreach (var s in all)
                {
                    if (s != null && s.name == preferredSpriteName)
                        return s;
                }

                return PickLargestSprite(all);
            }
        }

        var single = Resources.Load<Sprite>(resourcesPath);
        if (single != null)
            return single;

        var fallbackAll = Resources.LoadAll<Sprite>(resourcesPath);
        if (fallbackAll != null && fallbackAll.Length > 0)
            return PickLargestSprite(fallbackAll);

        return TryLoadFullTextureSprite(resourcesPath);
    }

    static Sprite PickLargestSprite(Sprite[] sprites)
    {
        Sprite best = null;
        float bestArea = 0f;
        foreach (var s in sprites)
        {
            if (s == null)
                continue;
            float area = s.rect.width * s.rect.height;
            if (area > bestArea)
            {
                bestArea = area;
                best = s;
            }
        }

        return best;
    }

    public void OnStartGameClicked()
    {
        if (_startLoadTriggered)
            return;
        _startLoadTriggered = true;

        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("TutorialSceneController: gameSceneName 为空，无法进入游戏场景。");
            _startLoadTriggered = false;
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    CanvasGroup GetFadeGroup()
    {
        if (contentFadeGroup != null)
            return contentFadeGroup;
        return pageGroup;
    }

    void EnsureBlackCamera()
    {
        var cam = Camera.main;
        if (cam == null)
            return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
    }

    void HideTutorialPageUi()
    {
        CacheTutorialCanvasRoot();
        if (_tutorialCanvasRoot != null)
            _tutorialCanvasRoot.SetActive(false);
    }

    void CacheTutorialCanvasRoot()
    {
        if (_tutorialCanvasRoot != null || pageGroup == null)
            return;

        var canvas = pageGroup.GetComponentInParent<Canvas>();
        if (canvas != null)
            _tutorialCanvasRoot = canvas.gameObject;
    }

    void EnsureFadeStructure()
    {
        if (pageGroup == null)
            return;

        var bg = pageGroup.transform.Find("Background");
        if (bg != null)
        {
            var bgImg = bg.GetComponent<Image>();
            if (bgImg != null)
                bgImg.color = Color.black;
        }

        pageGroup.alpha = 1f;

        if (contentFadeGroup != null)
            return;

        var existing = pageGroup.transform.Find("ContentFadeRoot");
        if (existing != null)
        {
            contentFadeGroup = existing.GetComponent<CanvasGroup>();
            return;
        }

        if (bg == null)
            return;

        var contentGo = new GameObject("ContentFadeRoot", typeof(RectTransform), typeof(CanvasGroup));
        contentGo.transform.SetParent(pageGroup.transform, false);
        StretchFull(contentGo.GetComponent<RectTransform>());
        contentFadeGroup = contentGo.GetComponent<CanvasGroup>();

        var toMove = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in pageGroup.transform)
        {
            if (child != bg && child != contentGo.transform)
                toMove.Add(child);
        }

        foreach (var child in toMove)
            child.SetParent(contentGo.transform, false);
    }

    void EnsureUiEventSystem()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            return;
        }

        if (eventSystem.GetComponent<BaseInputModule>() != null)
            return;

#if ENABLE_INPUT_SYSTEM
        eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
    }

    void ClearBuiltUi()
    {
        var canvas = transform.Find("TutorialCanvas");
        if (canvas != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(canvas.gameObject);
            else
#endif
                Destroy(canvas.gameObject);
        }

        pageGroup = null;
        contentFadeGroup = null;
        _tutorialCanvasRoot = null;
        imageFrameRect = null;
        pageImage = null;
        captionRect = null;
        captionText = null;
        centerTextRect = null;
        centerBodyText = null;
        startButtonRoot = null;
    }

    void BuildRuntimeUi()
    {
        var canvasGo = new GameObject("TutorialCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var root = new GameObject("PageRoot", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvasGo.transform, false);
        StretchFull(root.GetComponent<RectTransform>());
        pageGroup = root.GetComponent<CanvasGroup>();
        pageGroup.alpha = 1f;

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(root.transform, false);
        StretchFull(bgGo.GetComponent<RectTransform>());
        bgGo.GetComponent<Image>().sprite = s_whiteSprite;
        bgGo.GetComponent<Image>().color = Color.black;
        bgGo.GetComponent<Image>().raycastTarget = true;

        var contentGo = new GameObject("ContentFadeRoot", typeof(RectTransform), typeof(CanvasGroup));
        contentGo.transform.SetParent(root.transform, false);
        StretchFull(contentGo.GetComponent<RectTransform>());
        contentFadeGroup = contentGo.GetComponent<CanvasGroup>();

        var imageFrameGo = new GameObject("TutorialImageFrame", typeof(RectTransform), typeof(Image));
        imageFrameGo.transform.SetParent(contentGo.transform, false);
        imageFrameRect = imageFrameGo.GetComponent<RectTransform>();
        var frameImg = imageFrameGo.GetComponent<Image>();
        frameImg.sprite = s_whiteSprite;
        frameImg.color = new Color(0.92f, 0.93f, 0.96f, 1f);
        frameImg.raycastTarget = false;

        var imageGo = new GameObject("TutorialImage", typeof(RectTransform), typeof(Image));
        imageGo.transform.SetParent(imageFrameGo.transform, false);
        StretchFull(imageGo.GetComponent<RectTransform>());
        pageImage = imageGo.GetComponent<Image>();
        pageImage.raycastTarget = false;
        pageImage.color = Color.white;

        var textGo = new GameObject("Caption", typeof(RectTransform));
        textGo.transform.SetParent(contentGo.transform, false);
        captionRect = textGo.GetComponent<RectTransform>();
        captionText = textGo.AddComponent<Text>();
        UiFontHelper.ApplyTo(captionText, 34);
        captionText.alignment = TextAnchor.MiddleCenter;
        captionText.color = new Color(0.95f, 0.95f, 0.98f, 1f);
        captionText.lineSpacing = 1.15f;
        captionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        captionText.verticalOverflow = VerticalWrapMode.Overflow;
        captionText.raycastTarget = false;

        var centerGo = new GameObject("CenterBodyText", typeof(RectTransform));
        centerGo.transform.SetParent(contentGo.transform, false);
        centerTextRect = centerGo.GetComponent<RectTransform>();
        centerBodyText = centerGo.AddComponent<Text>();
        UiFontHelper.ApplyTo(centerBodyText, centerTextFontSize);
        centerBodyText.alignment = TextAnchor.MiddleCenter;
        centerBodyText.color = new Color(0.95f, 0.95f, 0.98f, 1f);
        centerBodyText.lineSpacing = 1.2f;
        centerBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        centerBodyText.verticalOverflow = VerticalWrapMode.Overflow;
        centerBodyText.raycastTarget = false;
        centerGo.SetActive(false);

        var startGo = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        startGo.transform.SetParent(contentGo.transform, false);
        var startRt = startGo.GetComponent<RectTransform>();
        startRt.anchorMin = startRt.anchorMax = new Vector2(0.5f, 0.08f);
        startRt.pivot = new Vector2(0.5f, 0.5f);
        startRt.sizeDelta = new Vector2(280f, 72f);
        startGo.GetComponent<Image>().sprite = s_whiteSprite;
        startGo.GetComponent<Image>().color = new Color(0.25f, 0.72f, 0.45f, 1f);
        startGo.GetComponent<Button>().onClick.AddListener(OnStartGameClicked);

        var startLabelGo = new GameObject("Label", typeof(RectTransform));
        startLabelGo.transform.SetParent(startGo.transform, false);
        StretchFull(startLabelGo.GetComponent<RectTransform>());
        var startLabel = startLabelGo.AddComponent<Text>();
        startLabel.text = "开始游戏";
        UiFontHelper.ApplyTo(startLabel, 32);
        startLabel.alignment = TextAnchor.MiddleCenter;
        startLabel.color = Color.white;
        startLabel.raycastTarget = false;

        startButtonRoot = startGo;
        startButtonRoot.SetActive(false);

        ApplyLayout();
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    static bool WasPrimaryClickThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            return true;
        return false;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}
