using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameScene 开局：屏幕中央显示 3、2、1，结束后由 WordSpawner 开始单词流程。
/// </summary>
[DefaultExecutionOrder(-30)]
public class GameStartCountdown : MonoBehaviour
{
    [SerializeField] bool buildRuntimeUi = true;
    [SerializeField] int canvasSortOrder = 650;
    [SerializeField] float secondsPerStep = 0.5f;

    [SerializeField] GameObject overlayRoot;
    [SerializeField] TMP_Text countdownText;

    [SerializeField] bool disableMicDuringCountdown = true;

    static Sprite s_whiteSprite;
    CenterZoneController _micController;
    bool _micWasEnabled = true;

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
        if (buildRuntimeUi && countdownText == null)
            BuildUi();

        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("Countdown_Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        overlayRoot = new GameObject("CountdownOverlay", typeof(RectTransform));
        overlayRoot.transform.SetParent(canvasGo.transform, false);
        var overlayRt = overlayRoot.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimGo.transform.SetParent(overlayRoot.transform, false);
        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        var dimImg = dimGo.GetComponent<Image>();
        dimImg.sprite = s_whiteSprite;
        dimImg.color = new Color(0.02f, 0.02f, 0.06f, 0.45f);
        dimImg.raycastTarget = true;

        var textGo = new GameObject("CountdownText", typeof(RectTransform));
        textGo.transform.SetParent(overlayRoot.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = Vector2.zero;
        textRt.sizeDelta = new Vector2(400f, 400f);

        countdownText = textGo.AddComponent<TextMeshProUGUI>();
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.fontSize = 200f;
        countdownText.fontStyle = FontStyles.Bold;
        countdownText.color = Color.white;
        countdownText.text = "3";
        countdownText.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
            countdownText.font = TMP_Settings.defaultFontAsset;
        else
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                countdownText.font = font;
        }

        overlayRoot.SetActive(false);
    }

    public IEnumerator PlayCountdown()
    {
        if (countdownText == null && overlayRoot == null)
            yield break;

        if (disableMicDuringCountdown)
        {
            _micController = FindFirstObjectByType<CenterZoneController>();
            if (_micController != null)
            {
                _micWasEnabled = _micController.enabled;
                _micController.enabled = false;
            }
        }

        if (overlayRoot != null)
            overlayRoot.SetActive(true);

        int[] steps = { 3, 2, 1 };
        float step = Mathf.Max(0.05f, secondsPerStep);
        for (int i = 0; i < steps.Length; i++)
        {
            if (countdownText != null)
                countdownText.text = steps[i].ToString();
            yield return new WaitForSecondsRealtime(step);
        }

        if (overlayRoot != null)
            overlayRoot.SetActive(false);

        if (disableMicDuringCountdown && _micController != null)
            _micController.enabled = _micWasEnabled;
    }
}
