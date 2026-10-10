using UnityEngine;
using TMPro;

/// <summary>
/// 每个单词开始前，在天空背景上显示整词大字预览（在青蛙与音节块后面）。
/// </summary>
public class WordBackgroundPreview : MonoBehaviour
{
    [Header("显示")]
    [SerializeField] bool buildRuntimeTextIfMissing = true;
    [SerializeField] TextMeshPro previewText;
    [SerializeField] Vector3 worldPosition = new Vector3(0f, -1.35f, 0f);
    [SerializeField] float fontSize = 11f;
    [SerializeField] Color textColor = new Color(0.29f, 0.38f, 0.40f, 0.55f);
    [SerializeField] int sortingOrder = -5;

    void Awake()
    {
        if (previewText == null && buildRuntimeTextIfMissing)
            BuildRuntimeText();

        Hide();
    }

    void BuildRuntimeText()
    {
        var go = new GameObject("WordPreviewText");
        go.transform.SetParent(transform, true);
        go.transform.position = worldPosition;

        previewText = go.AddComponent<TextMeshPro>();
        previewText.alignment = TextAlignmentOptions.Center;
        previewText.fontSize = fontSize;
        previewText.fontStyle = FontStyles.Bold;
        previewText.color = textColor;
        previewText.enableWordWrapping = false;
        previewText.overflowMode = TextOverflowModes.Overflow;
        previewText.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
            previewText.font = TMP_Settings.defaultFontAsset;
        else
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                previewText.font = font;
        }

        var renderer = previewText.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sortingOrder = sortingOrder;
    }

    public static string FormatDisplayWord(WordItem word)
    {
        if (word == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(word.wordName))
        {
            string w = word.wordName.Trim();
            if (w.Length == 1)
                return w.ToUpperInvariant();
            return char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant();
        }

        if (word.syllables == null || word.syllables.Count == 0)
            return string.Empty;

        var parts = new System.Text.StringBuilder();
        foreach (var syl in word.syllables)
        {
            if (syl == null || string.IsNullOrEmpty(syl.text))
                continue;
            parts.Append(syl.isStressed ? syl.text.ToUpperInvariant() : syl.text.ToLowerInvariant());
        }

        return parts.ToString();
    }

    public void Show(string displayWord)
    {
        if (previewText == null)
            return;

        previewText.text = displayWord ?? string.Empty;
        previewText.fontSize = fontSize;
        previewText.color = textColor;
        previewText.gameObject.SetActive(!string.IsNullOrEmpty(displayWord));
    }

    public bool IsVisible =>
        previewText != null && previewText.gameObject.activeSelf && !string.IsNullOrEmpty(previewText.text);

    public void Hide()
    {
        if (previewText == null)
            return;

        previewText.text = string.Empty;
        previewText.gameObject.SetActive(false);
    }
}
