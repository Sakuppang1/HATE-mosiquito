using UnityEngine;
using UnityEngine.UI;
using System.Globalization;
using System.IO;

/// <summary>
/// 在屏幕左侧显示竖直麦克风音量条，并标出触发阈值（红）与复位阈值（黄）。
/// 默认使用场景中已摆放的 UI（MicVolumeHUD_Canvas），可在 Scene 视图直接拖动、缩放 MicBarPanel。
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(100)]
public class MicVolumeBarUI : MonoBehaviour
{
    [Header("数据源（可空，将在 LateUpdate 前尝试自动查找）")]
    [SerializeField] CenterZoneController micController;

    [Header("显示范围")]
    [SerializeField] float maxDisplayValue = 0.32f;
    [SerializeField] float displaySmoothSpeed = 2.2f;

    [Header("布局")]
    [Tooltip("勾选：位置与大小完全由 Scene 里 MicBarPanel 的 RectTransform 决定（推荐）。取消：用下方锚点比例由脚本覆盖。")]
    [SerializeField] bool useSceneRectLayout = true;

    [SerializeField] [Range(0f, 1f)] float barAnchorLeft = 0.02f;
    [SerializeField] [Range(0f, 1f)] float barAnchorRight = 0.13f;
    [SerializeField] [Range(0f, 1f)] float barAnchorBottom = 0.50f;
    [SerializeField] [Range(0f, 1f)] float barAnchorTop = 0.90f;
    [SerializeField] Vector2 barOffsetMin = Vector2.zero;
    [SerializeField] Vector2 barOffsetMax = Vector2.zero;
    [SerializeField] [Min(0.05f)] float barVisualScale = 1f;

    [Header("场景 UI 引用（拖 MicBarPanel / VoiceBarFill 等；留空且勾选下方才会运行时生成）")]
    [SerializeField] RectTransform barTrack;
    [SerializeField] Image volumeFillImage;
    [SerializeField] RectTransform thresholdMarker;
    [SerializeField] RectTransform releaseMarker;
    [SerializeField] Text valueLabel;

    [Tooltip("仅当上方引用为空时，在 Play 时动态创建 UI。已摆好场景 UI 时请保持关闭。")]
    [SerializeField] bool buildRuntimeUiIfNeeded = false;

    [Header("Resources/voice 进度条美术")]
    [SerializeField] bool useVoiceProgressResources = true;
    [SerializeField] string voiceBarResourcesPath = "voice/进度条";

    static Sprite s_whiteSprite;
    static Sprite s_cachedVoiceBarSprite;

    float _smoothedVol;
    bool _smoothedInit;
    bool _useImageFillForVolume;
    RectTransform _labelRect;
    RectTransform _canvasRect;

    // #region agent log
    static float _hudDbgLast;
    static void HudDbg(string jsonNoBraces)
    {
        try
        {
            long ts = (long)(Time.realtimeSinceStartup * 1000f);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "debug-85435f.log"));
            string line = "{\"sessionId\":\"85435f\",\"hypothesisId\":\"H3\",\"location\":\"MicVolumeBarUI:LateUpdate\",\"message\":\"hud_norm\",\"data\":{" + jsonNoBraces + "},\"timestamp\":" + ts + "}\n";
            File.AppendAllText(path, line);
        }
        catch { }
    }
    // #endregion

    public void Initialize(CenterZoneController controller)
    {
        micController = controller;
        EnsureUiReferences();
        if (buildRuntimeUiIfNeeded && barTrack == null)
            BuildRuntimeUi();
        RefreshFillModeFromImage();
        if (!useSceneRectLayout)
        {
            ApplyBarLayout();
            ApplyBarVisualScale();
        }
    }

    void Awake()
    {
        if (s_whiteSprite == null)
        {
            var t = Texture2D.whiteTexture;
            s_whiteSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }

        EnsureUiReferences();
    }

    void Start()
    {
        if (micController == null)
            micController = FindFirstObjectByType<CenterZoneController>();

        EnsureUiReferences();
        if (buildRuntimeUiIfNeeded && barTrack == null)
            BuildRuntimeUi();

        RefreshFillModeFromImage();
        if (!useSceneRectLayout)
            ApplyBarVisualScale();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureUiReferences();

        if (buildRuntimeUiIfNeeded && barTrack == null && !Application.isPlaying)
        {
            BuildRuntimeUi();
            UnityEditor.EditorUtility.SetDirty(this);
            return;
        }

        if (!useSceneRectLayout)
        {
            ApplyBarLayout();
            ApplyBarVisualScale();
        }
    }

    [ContextMenu("生成/重建音量条 UI（会删除现有子 Canvas）")]
    public void EditorRebuildUi()
    {
        ClearBuiltUi();
        buildRuntimeUiIfNeeded = false;
        BuildRuntimeUi();
        useSceneRectLayout = true;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    [ContextMenu("从子物体自动绑定 UI 引用")]
    public void EditorAutoWireReferences()
    {
        AutoWireFromChildren();
        useSceneRectLayout = true;
        buildRuntimeUiIfNeeded = false;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    void EnsureUiReferences()
    {
        if (barTrack != null)
        {
            _canvasRect = barTrack.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (valueLabel != null)
                _labelRect = valueLabel.rectTransform;
            return;
        }

        AutoWireFromChildren();
    }

    void AutoWireFromChildren()
    {
        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
            _canvasRect = canvas.GetComponent<RectTransform>();

        barTrack = transform.Find("MicVolumeHUD_Canvas/MicBarPanel") as RectTransform;
        if (barTrack == null)
            barTrack = GetComponentInChildren<RectTransform>(true);

        if (barTrack != null && barTrack.name != "MicBarPanel")
        {
            var named = barTrack.Find("MicBarPanel") as RectTransform;
            if (named != null)
                barTrack = named;
        }

        if (barTrack == null)
            return;

        volumeFillImage = barTrack.Find("VoiceBarFill")?.GetComponent<Image>();
        if (volumeFillImage == null)
            volumeFillImage = barTrack.Find("Fill")?.GetComponent<Image>();

        thresholdMarker = barTrack.Find("ThresholdLine") as RectTransform;
        releaseMarker = barTrack.Find("ReleaseLine") as RectTransform;

        var label = transform.Find("MicVolumeHUD_Canvas/Label");
        if (label != null)
        {
            valueLabel = label.GetComponent<Text>();
            _labelRect = label as RectTransform;
        }
    }

    public void ApplyBarLayout()
    {
        if (useSceneRectLayout)
            return;

        float left = Mathf.Min(barAnchorLeft, barAnchorRight);
        float right = Mathf.Max(barAnchorLeft, barAnchorRight);
        float bottom = Mathf.Min(barAnchorBottom, barAnchorTop);
        float top = Mathf.Max(barAnchorBottom, barAnchorTop);

        if (barTrack != null)
        {
            barTrack.anchorMin = new Vector2(left, bottom);
            barTrack.anchorMax = new Vector2(right, top);
            barTrack.pivot = new Vector2(0.5f, 0.5f);
            barTrack.anchoredPosition = Vector2.zero;
            barTrack.offsetMin = barOffsetMin;
            barTrack.offsetMax = barOffsetMax;
        }

        if (_labelRect != null)
        {
            _labelRect.anchorMin = new Vector2(left, bottom - 0.12f);
            _labelRect.anchorMax = new Vector2(0.42f, bottom - 0.01f);
            _labelRect.offsetMin = Vector2.zero;
            _labelRect.offsetMax = Vector2.zero;
        }
    }

    public void ApplyBarVisualScale()
    {
        if (useSceneRectLayout || barTrack == null)
            return;

        float s = Mathf.Max(0.05f, barVisualScale);
        barTrack.localScale = new Vector3(s, s, 1f);
    }

    void ClearBuiltUi()
    {
        if (_canvasRect != null && _canvasRect.gameObject.name == "MicVolumeHUD_Canvas")
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(_canvasRect.gameObject);
            else
#endif
                Destroy(_canvasRect.gameObject);
        }

        barTrack = null;
        volumeFillImage = null;
        thresholdMarker = null;
        releaseMarker = null;
        valueLabel = null;
        _labelRect = null;
        _canvasRect = null;
    }

    void RefreshFillModeFromImage()
    {
        if (!_useImageFillForVolume && volumeFillImage != null && volumeFillImage.type == Image.Type.Filled)
            _useImageFillForVolume = true;
    }

    void LateUpdate()
    {
        if (micController == null)
            micController = FindFirstObjectByType<CenterZoneController>();
        if (micController == null || barTrack == null || volumeFillImage == null)
            return;

        float vol = micController.UiMeterLevel;
        float hi = micController.TriggerThresholdDisplay;
        float lo = micController.ReleaseThresholdDisplay;

        if (!_smoothedInit)
        {
            _smoothedVol = vol;
            _smoothedInit = true;
        }
        else
        {
            float k = Mathf.Clamp01(Time.deltaTime * displaySmoothSpeed);
            _smoothedVol = Mathf.Lerp(_smoothedVol, vol, k);
        }

        float max = Mathf.Max(0.01f, maxDisplayValue, hi * 1.15f);
        float nVol = Mathf.Clamp01(vol / max);
        float nHi = Mathf.Clamp01(hi / max);
        float nLo = Mathf.Clamp01(lo / max);

        // #region agent log
        float rt = Time.realtimeSinceStartup;
        if (rt - _hudDbgLast >= 0.45f)
        {
            _hudDbgLast = rt;
            var inv = CultureInfo.InvariantCulture;
            bool over = nVol >= nHi;
            float nVolSmoothed = Mathf.Clamp01(_smoothedVol / max);
            float micRaw = micController.CurrentMicLevel;
            HudDbg("\"hypothesisTag\":\"H3\",\"nVol\":" + nVol.ToString(inv) + ",\"nVolSmoothed\":" + nVolSmoothed.ToString(inv) + ",\"nHi\":" + nHi.ToString(inv) + ",\"nLo\":" + nLo.ToString(inv) +
                   ",\"max\":" + max.ToString(inv) + ",\"uiMeterExcursion\":" + vol.ToString(inv) + ",\"micRawScaled\":" + micRaw.ToString(inv) + ",\"smoothed\":" + _smoothedVol.ToString(inv) +
                   ",\"barAppearsOverRedLine\":" + (over ? "true" : "false"));
        }
        // #endregion

        if (_useImageFillForVolume && volumeFillImage != null && volumeFillImage.type == Image.Type.Filled)
        {
            volumeFillImage.fillAmount = Mathf.Clamp01(nVol);
        }
        else
        {
            var fillRt = volumeFillImage.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, nVol);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
        }

        PlaceHorizontalMarker(thresholdMarker, nHi, 5f);
        PlaceHorizontalMarker(releaseMarker, nLo, 9f);

        if (valueLabel != null)
        {
            float raw = micController.CurrentMicLevel;
            float amb = micController.CurrentAmbientLevel;
            valueLabel.text = $"相对环境 +{vol:F2}（平滑 {_smoothedVol:F2}）\n环境估计 {amb:F2}  原麦 {raw:F2}\n超阈≥{hi:F2}  突出复位<{lo:F2}";
        }
    }

    static void PlaceHorizontalMarker(RectTransform line, float normalizedY, float thicknessPx)
    {
        if (line == null) return;
        normalizedY = Mathf.Clamp01(normalizedY);
        line.anchorMin = new Vector2(0f, normalizedY);
        line.anchorMax = new Vector2(1f, normalizedY);
        line.pivot = new Vector2(0.5f, 0.5f);
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = new Vector2(0f, thicknessPx);
    }

    void BuildRuntimeUi()
    {
        if (barTrack != null)
            ClearBuiltUi();

        var canvasGo = new GameObject("MicVolumeHUD_Canvas");
        canvasGo.transform.SetParent(transform, false);
        _canvasRect = canvasGo.GetComponent<RectTransform>();
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("MicBarPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasGo.transform, false);
        barTrack = panel.GetComponent<RectTransform>();

        if (!useSceneRectLayout)
            ApplyBarLayout();
        else
        {
            barTrack.anchorMin = new Vector2(0.02f, 0.35f);
            barTrack.anchorMax = new Vector2(0.12f, 0.75f);
            barTrack.offsetMin = Vector2.zero;
            barTrack.offsetMax = Vector2.zero;
        }

        Sprite voiceSprite = null;
        if (useVoiceProgressResources && !string.IsNullOrEmpty(voiceBarResourcesPath))
        {
            if (s_cachedVoiceBarSprite == null)
            {
                s_cachedVoiceBarSprite = Resources.Load<Sprite>(voiceBarResourcesPath);
                if (s_cachedVoiceBarSprite == null)
                {
                    var all = Resources.LoadAll<Sprite>(voiceBarResourcesPath);
                    if (all != null && all.Length > 0)
                        s_cachedVoiceBarSprite = all[0];
                }
            }

            voiceSprite = s_cachedVoiceBarSprite;
        }

        if (voiceSprite != null)
        {
            _useImageFillForVolume = true;

            var bgGo = new GameObject("VoiceBarBackground", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(panel.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.SetAsFirstSibling();
            StretchFull(bgRt);
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = voiceSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.color = new Color(0.22f, 0.24f, 0.28f, 0.95f);
            bgImg.raycastTarget = false;

            var fillGo = new GameObject("VoiceBarFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(panel.transform, false);
            StretchFull(fillGo.GetComponent<RectTransform>());
            volumeFillImage = fillGo.GetComponent<Image>();
            volumeFillImage.sprite = voiceSprite;
            volumeFillImage.type = Image.Type.Filled;
            volumeFillImage.fillMethod = Image.FillMethod.Vertical;
            volumeFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            volumeFillImage.fillAmount = 0f;
            volumeFillImage.color = new Color(0.55f, 0.95f, 0.65f, 1f);
            volumeFillImage.raycastTarget = false;
        }
        else
        {
            _useImageFillForVolume = false;
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(panel.transform, false);
            StretchFull(bgGo.GetComponent<RectTransform>());
            bgGo.GetComponent<Image>().sprite = s_whiteSprite;
            bgGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.88f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(panel.transform, false);
            StretchFull(fillGo.GetComponent<RectTransform>());
            volumeFillImage = fillGo.GetComponent<Image>();
            volumeFillImage.sprite = s_whiteSprite;
            volumeFillImage.color = new Color(0.15f, 0.92f, 0.42f, 0.98f);
            volumeFillImage.type = Image.Type.Simple;
        }

        thresholdMarker = CreateHLine(panel.transform, "ThresholdLine", new Color(1f, 0.2f, 0.22f, 1f));
        releaseMarker = CreateHLine(panel.transform, "ReleaseLine", new Color(1f, 0.92f, 0.15f, 1f));
        thresholdMarker.SetAsLastSibling();
        releaseMarker.SetAsLastSibling();

        _canvasRect.localScale = Vector3.one;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(canvasGo);
        UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    RectTransform CreateHLine(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = s_whiteSprite;
        img.color = c;
        img.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }
}
