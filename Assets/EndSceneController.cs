using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>结算场景：显示本局得分与完成单词数，可返回开始或再来一局。</summary>
public class EndSceneController : MonoBehaviour
{
    [SerializeField] string gameSceneName = "GameScene";
    [SerializeField] string startSceneName = "StartScene";

    void Awake()
    {
        EnsureEventSystem();
    }

    void Start()
    {
        BuildUi();
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("EndCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        var panelImg = panel.GetComponent<Image>();
        panelImg.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);

        int score = GameSession.LastScore;
        int words = GameSession.WordsCompleted;

        CreateText(panel.transform, "Title", "本局结束", 56, new Vector2(0, 220), new Vector2(1400, 100), Color.white);
        CreateText(panel.transform, "Score", $"得分：{score}\n完成单词数：{words}", 42, new Vector2(0, 40), new Vector2(1400, 280), new Color(0.92f, 0.94f, 1f, 1f));

        CreateButton(panel.transform, "BtnAgain", "再来一局", new Vector2(-220, -200), () => SceneManager.LoadScene(gameSceneName));
        CreateButton(panel.transform, "BtnHome", "返回开始", new Vector2(220, -200), () => SceneManager.LoadScene(startSceneName));
    }

    static void CreateText(Transform parent, string name, string content, int fontSize, Vector2 anchoredPos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var tx = go.GetComponent<Text>();
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (tx.font == null)
            tx.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tx.text = content;
        tx.fontSize = fontSize;
        tx.color = color;
        tx.alignment = TextAnchor.MiddleCenter;
    }

    static void CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(320, 72);
        var img = go.GetComponent<Image>();
        var t = Texture2D.whiteTexture;
        img.sprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        img.color = new Color(0.28f, 0.52f, 0.95f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
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
        tx.fontSize = 28;
        tx.color = Color.white;
        tx.alignment = TextAnchor.MiddleCenter;
    }
}
