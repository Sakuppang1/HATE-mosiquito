using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内暂停：Canvas 上右上角停止按钮；暂停后全屏半透明幕布，中央继续按钮（图来自 Assets/bottom）。
/// 使用 Time.timeScale 与 AudioListener.pause。
/// </summary>
[DefaultExecutionOrder(-40)]
public class GamePauseUI : MonoBehaviour
{
    [SerializeField] bool buildRuntimeUi = true;
    [SerializeField] int canvasSortOrder = 600;

    [Tooltip("右上角停止/暂停按钮图，默认 Assets/bottom/暂停键")]
    [SerializeField] Sprite stopButtonSprite;

    [Tooltip("幕布中央继续按钮图，默认 Assets/bottom/继续按钮")]
    [SerializeField] Sprite continueButtonSprite;

    [Header("停止按钮布局（锚点右上）")]
    [Tooltip("相对屏幕右上角的 anchoredPosition，单位参考 CanvasScaler 1920×1080")]
    [SerializeField] Vector2 stopAnchoredOffset = new Vector2(-28f, -28f);

    [Tooltip("(0,0)=自动：有 Sprite 时取 Max(最小尺寸, 图素尺寸)；无图时用「无图时尺寸」。非 (0,0) 则强制为该 sizeDelta")]
    [SerializeField] Vector2 stopSizeDelta = Vector2.zero;

    [SerializeField] Vector2 stopAutoMinWhenSprite = new Vector2(96f, 96f);
    [SerializeField] Vector2 stopFallbackNoSpriteSize = new Vector2(140f, 52f);

    [Header("继续按钮布局（锚点屏幕中心）")]
    [Tooltip("相对屏幕中心的 anchoredPosition 偏移")]
    [SerializeField] Vector2 continueAnchoredOffset = Vector2.zero;

    [Tooltip("(0,0)=自动：有 Sprite 时在 Clamp 范围内用图素尺寸；无图时用「无图时尺寸」。非 (0,0) 则强制为该 sizeDelta")]
    [SerializeField] Vector2 continueSizeDelta = Vector2.zero;

    [SerializeField] Vector2 continueAutoClampMin = new Vector2(160f, 48f);
    [SerializeField] Vector2 continueAutoClampMax = new Vector2(720f, 280f);
    [SerializeField] Vector2 continueFallbackNoSpriteSize = new Vector2(280f, 72f);

    Button _pauseBtn;
    Button _continueBtn;
    GameObject _overlayRoot;
    float _savedTimeScale = 1f;
    bool _isPaused;
    static Sprite s_whiteSprite;

    void Awake()
    {
        if (s_whiteSprite == null)
        {
            var t = Texture2D.whiteTexture;
            s_whiteSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    void Start()
    {
        if (buildRuntimeUi)
            BuildUi();
        RefreshInteractable();
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("PauseMenu_Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 全屏暂停层（默认隐藏）：半透明底 + 中央继续
        _overlayRoot = new GameObject("PauseOverlay", typeof(RectTransform));
        _overlayRoot.transform.SetParent(canvasGo.transform, false);
        var overlayRt = _overlayRoot.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimGo.transform.SetParent(_overlayRoot.transform, false);
        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        var dimImg = dimGo.GetComponent<Image>();
        dimImg.sprite = s_whiteSprite;
        dimImg.color = new Color(0.02f, 0.02f, 0.04f, 0.62f);
        dimImg.raycastTarget = true;

        _continueBtn = CreateCenterImageButton(_overlayRoot.transform, continueButtonSprite, "继续", ResumeGame);

        _overlayRoot.SetActive(false);

        // 停止按钮在幕布之上，始终可点（暂停时通过 interactable 禁止重复暂停）
        _pauseBtn = CreateCornerStopButton(canvasGo.transform, stopButtonSprite, PauseGame);
    }

    Button CreateCornerStopButton(Transform parent, Sprite sprite, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("StopButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = stopAnchoredOffset;

        var img = go.GetComponent<Image>();
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;
            if (stopSizeDelta.sqrMagnitude > 0.0001f)
                rt.sizeDelta = stopSizeDelta;
            else
            {
                rt.sizeDelta = new Vector2(
                    Mathf.Max(stopAutoMinWhenSprite.x, sprite.rect.width),
                    Mathf.Max(stopAutoMinWhenSprite.y, sprite.rect.height));
            }
        }
        else
        {
            img.sprite = s_whiteSprite;
            img.color = new Color(0.35f, 0.55f, 0.95f, 1f);
            rt.sizeDelta = stopSizeDelta.sqrMagnitude > 0.0001f ? stopSizeDelta : stopFallbackNoSpriteSize;
            AddCenteredText(go.transform, "停止", 22);
        }

        return btn;
    }

    Button CreateCenterImageButton(Transform parent, Sprite sprite, string fallbackLabel, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = continueAnchoredOffset;

        var img = go.GetComponent<Image>();
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;
            if (continueSizeDelta.sqrMagnitude > 0.0001f)
                rt.sizeDelta = continueSizeDelta;
            else
            {
                float w = Mathf.Clamp(sprite.rect.width, continueAutoClampMin.x, continueAutoClampMax.x);
                float h = Mathf.Clamp(sprite.rect.height, continueAutoClampMin.y, continueAutoClampMax.y);
                rt.sizeDelta = new Vector2(w, h);
            }
        }
        else
        {
            img.sprite = s_whiteSprite;
            img.color = new Color(0.25f, 0.75f, 0.4f, 1f);
            rt.sizeDelta = continueSizeDelta.sqrMagnitude > 0.0001f ? continueSizeDelta : continueFallbackNoSpriteSize;
            AddCenteredText(go.transform, fallbackLabel, 26);
        }

        return btn;
    }

    static void AddCenteredText(Transform parent, string label, int fontSize)
    {
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(parent, false);
        var tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        var tx = textGo.GetComponent<Text>();
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (tx.font == null)
            tx.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tx.text = label;
        tx.fontSize = fontSize;
        tx.color = Color.white;
        tx.alignment = TextAnchor.MiddleCenter;
    }

    void PauseGame()
    {
        if (_isPaused) return;
        _savedTimeScale = Time.timeScale > 0.0001f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        _isPaused = true;
        if (_overlayRoot != null)
            _overlayRoot.SetActive(true);
        RefreshInteractable();
    }

    void ResumeGame()
    {
        if (!_isPaused) return;
        AudioListener.pause = false;
        Time.timeScale = _savedTimeScale > 0.0001f ? _savedTimeScale : 1f;
        _isPaused = false;
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
        RefreshInteractable();
    }

    void RefreshInteractable()
    {
        if (_pauseBtn != null)
            _pauseBtn.interactable = !_isPaused;
        if (_continueBtn != null)
            _continueBtn.interactable = _isPaused;
    }

    void OnDestroy()
    {
        if (_isPaused)
        {
            AudioListener.pause = false;
            Time.timeScale = _savedTimeScale > 0.0001f ? _savedTimeScale : 1f;
        }
    }
}
