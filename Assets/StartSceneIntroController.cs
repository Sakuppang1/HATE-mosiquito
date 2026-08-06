using UnityEngine;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// StartScene：生成若干只随机飞舞的蚊子，点击消灭；全部消灭后显示标题与开始按钮。
/// </summary>
public class StartSceneIntroController : MonoBehaviour
{
    [Header("蚊子")]
    [Tooltip("留空则从 Resources 加载 mosquito/MosquitoAnimated")]
    [SerializeField] GameObject mosquitoPrefab;

    [SerializeField] string mosquitoPrefabResourcesPath = "mosquito/MosquitoAnimated";

    [SerializeField] [Min(1)] int mosquitoCount = 3;

    [SerializeField] float mosquitoScale = 0.22f;

    [Tooltip("蚊子 CircleCollider2D 半径（世界单位，会随蚊子缩放一起变大变小）。越大越容易点中。")]
    [SerializeField] float clickColliderRadius = 0.45f;

    [SerializeField] float boundsMargin = 0.6f;

    [SerializeField] float flySpeedMin = 1.2f;

    [SerializeField] float flySpeedMax = 2.8f;

    [SerializeField] int mosquitoSortingOrder = 100;

    [Tooltip("相对 UI 平面再靠近摄像机一点，避免被全屏 Panel 挡住")]
    [SerializeField] float mosquitoZOffsetFromUi = 0.2f;

    [Header("摄像机")]
    [SerializeField] Camera targetCamera;

    [Header("消灭蚊子后显示（可拖场景物体；留空则按名称查找 Title / Button）")]
    [SerializeField] GameObject titleObject;

    [SerializeField] GameObject startButtonObject;

    [Tooltip("占位：若上面都未指定，则显示此组（可之后再换成正式美术）")]
    [SerializeField] GameObject placeholderUiRoot;

    [Tooltip("场景 UI_Container 的 CanvasGroup；开场关闭射线阻挡以便点击蚊子")]
    [SerializeField] CanvasGroup uiContainerGroup;

    [Header("消灭蚊子后 UI")]
    [SerializeField] float uiFadeInDuration = 1.2f;

    [Header("开场 BGM")]
    [Tooltip("留空则自动查找场景中的 BGM_Manager")]
    [SerializeField] AudioSource introBgmSource;

    [SerializeField] float introBgmFadeOutDuration = 1f;

    int _aliveCount;
    Tweener _uiFadeTween;
    Tweener _bgmFadeTween;
    float _introBgmInitialVolume = 1f;
    float _mosquitoRenderZ;
    static GameObject s_cachedMosquitoPrefab;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        ResolveUiReferences();
        CacheIntroBgm();
        PrepareUiForIntro();

        SpawnMosquitoes();
    }

    void OnDestroy()
    {
        _uiFadeTween?.Kill();
        _bgmFadeTween?.Kill();
    }

    void CacheIntroBgm()
    {
        if (introBgmSource == null)
        {
            var bgmGo = GameObject.Find("BGM_Manager");
            if (bgmGo != null)
                introBgmSource = bgmGo.GetComponent<AudioSource>();
        }

        if (introBgmSource != null)
            _introBgmInitialVolume = introBgmSource.volume;
    }

    void FadeOutIntroBgm()
    {
        if (introBgmSource == null)
        {
            var bgmGo = GameObject.Find("BGM_Manager");
            if (bgmGo != null)
                introBgmSource = bgmGo.GetComponent<AudioSource>();
        }

        if (introBgmSource == null || !introBgmSource.isPlaying)
            return;

        _bgmFadeTween?.Kill();
        _bgmFadeTween = introBgmSource
            .DOFade(0f, Mathf.Max(0.05f, introBgmFadeOutDuration))
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                introBgmSource.Stop();
                introBgmSource.volume = _introBgmInitialVolume;
            });
    }

    void PrepareUiForIntro()
    {
        if (uiContainerGroup == null)
        {
            var container = GameObject.Find(" UI_Container");
            if (container == null)
                container = GameObject.Find("UI_Container");
            if (container != null)
                uiContainerGroup = container.GetComponent<CanvasGroup>();
        }

        EnsureUiObjectsActive();
        if (uiContainerGroup != null)
        {
            _uiFadeTween?.Kill();
            uiContainerGroup.alpha = 0f;
            uiContainerGroup.blocksRaycasts = false;
            uiContainerGroup.interactable = false;
        }

        // 全屏背景 Panel 会挡住射线；开场只点蚊子，暂时关闭阻挡
        var panel = GameObject.Find("Panel");
        if (panel != null)
        {
            var panelGroup = panel.GetComponent<CanvasGroup>();
            if (panelGroup != null)
                panelGroup.blocksRaycasts = false;

            var panelImage = panel.GetComponent<UnityEngine.UI.Image>();
            if (panelImage != null)
                panelImage.raycastTarget = false;
        }
    }

    void Update()
    {
        if (!TryGetPrimaryClickScreenPosition(out Vector2 screenPos))
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        float depth = _mosquitoRenderZ - targetCamera.transform.position.z;
        var worldPoint = new Vector3(screenPos.x, screenPos.y, depth);
        Vector3 world = targetCamera.ScreenToWorldPoint(worldPoint);
        var hit = Physics2D.OverlapPoint(world);
        if (hit == null)
            return;

        var mosquito = hit.GetComponentInParent<StartSceneMosquito>();
        if (mosquito != null)
            mosquito.NotifyClicked();
    }

    void ResolveUiReferences()
    {
        if (titleObject == null)
        {
            var t = GameObject.Find("Title");
            if (t != null) titleObject = t;
        }

        if (startButtonObject == null)
        {
            var b = GameObject.Find("Button");
            if (b != null) startButtonObject = b;
        }
    }

    void EnsureUiObjectsActive()
    {
        if (titleObject != null)
            titleObject.SetActive(true);
        if (startButtonObject != null)
            startButtonObject.SetActive(true);
        if (placeholderUiRoot != null)
            placeholderUiRoot.SetActive(true);
    }

    void FadeInUi()
    {
        EnsureUiObjectsActive();

        if (uiContainerGroup == null)
        {
            var container = GameObject.Find(" UI_Container");
            if (container == null)
                container = GameObject.Find("UI_Container");
            if (container != null)
                uiContainerGroup = container.GetComponent<CanvasGroup>();
        }

        if (uiContainerGroup == null)
            return;

        _uiFadeTween?.Kill();
        uiContainerGroup.alpha = 0f;
        uiContainerGroup.blocksRaycasts = false;
        uiContainerGroup.interactable = false;

        _uiFadeTween = uiContainerGroup
            .DOFade(1f, Mathf.Max(0.05f, uiFadeInDuration))
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                uiContainerGroup.blocksRaycasts = true;
                uiContainerGroup.interactable = true;
            });
    }

    void SpawnMosquitoes()
    {
        GameObject prefab = ResolveMosquitoPrefab();
        if (prefab == null)
        {
            Debug.LogError("StartSceneIntroController: 未找到蚊子预制体。");
            RestorePanelRaycasts();
            FadeOutIntroBgm();
            FadeInUi();
            return;
        }

        _mosquitoRenderZ = ComputeMosquitoRenderZ(targetCamera);

        _aliveCount = 0;
        for (int i = 0; i < mosquitoCount; i++)
        {
            GameObject inst = Instantiate(prefab);
            inst.name = "StartMosquito_" + (i + 1);

            float scale = Mathf.Max(0.05f, mosquitoScale);
            inst.transform.localScale = new Vector3(scale, scale, 1f);

            var sr = inst.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = mosquitoSortingOrder;
                sr.sortingLayerName = "Default";
            }

            var fly = inst.GetComponent<StartSceneMosquitoFly>();
            if (fly == null)
                fly = inst.AddComponent<StartSceneMosquitoFly>();
            fly.Initialize(targetCamera, boundsMargin, Random.Range(flySpeedMin, flySpeedMax), _mosquitoRenderZ);

            var click = inst.GetComponent<StartSceneMosquito>();
            if (click == null)
                click = inst.AddComponent<StartSceneMosquito>();
            click.Initialize(this);

            var col = inst.GetComponent<CircleCollider2D>();
            if (col == null)
                col = inst.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = clickColliderRadius;

            _aliveCount++;
        }
    }

    GameObject ResolveMosquitoPrefab()
    {
        if (mosquitoPrefab != null)
            return mosquitoPrefab;

        if (string.IsNullOrEmpty(mosquitoPrefabResourcesPath))
            return null;

        if (s_cachedMosquitoPrefab == null)
            s_cachedMosquitoPrefab = Resources.Load<GameObject>(mosquitoPrefabResourcesPath);

        return s_cachedMosquitoPrefab;
    }

    float ComputeMosquitoRenderZ(Camera cam)
    {
        if (cam == null)
            return 0f;

        float uiPlaneZ = cam.transform.position.z;
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas == null || !canvas.isActiveAndEnabled)
                continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera != cam)
                continue;

            uiPlaneZ = cam.transform.position.z + canvas.planeDistance;
            break;
        }

        return uiPlaneZ - Mathf.Max(0.01f, mosquitoZOffsetFromUi);
    }

    public void OnMosquitoClicked(GameObject mosquitoObject)
    {
        if (mosquitoObject == null)
            return;

        Destroy(mosquitoObject);
        _aliveCount = Mathf.Max(0, _aliveCount - 1);

        if (_aliveCount <= 0)
        {
            RestorePanelRaycasts();
            FadeOutIntroBgm();
            FadeInUi();
        }
    }

    void RestorePanelRaycasts()
    {
        var panel = GameObject.Find("Panel");
        if (panel == null)
            return;

        var panelGroup = panel.GetComponent<CanvasGroup>();
        if (panelGroup != null)
            panelGroup.blocksRaycasts = true;

        var panelImage = panel.GetComponent<UnityEngine.UI.Image>();
        if (panelImage != null)
            panelImage.raycastTarget = true;
    }

    static bool TryGetPrimaryClickScreenPosition(out Vector2 screenPos)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPos = mouse.position.ReadValue();
            return true;
        }

        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = touch.primaryTouch.position.ReadValue();
            return true;
        }

        screenPos = default;
        return false;
#else
        if (Input.GetMouseButtonDown(0))
        {
            screenPos = Input.mousePosition;
            return true;
        }

        screenPos = default;
        return false;
#endif
    }
}
