using UnityEngine;
using TMPro;

public class SyllableDisplay : MonoBehaviour
{
    [Header("音节文字")]
    public TextMeshPro syllableText;

    [Header("重音：右上角蚊子（做法 A：Resources 预制体 + Animator）")]
    [Tooltip("留空则从 Resources 路径加载；可在 Inspector 直接拖预制体覆盖")]
    public GameObject mosquitoPrefabOverride;

    [Tooltip("Resources 路径，不含扩展名，例如 mosquito/MosquitoAnimated")]
    public string mosquitoPrefabResourcesPath = "mosquito/MosquitoAnimated";

    [Tooltip("相对方块本地坐标，右上角偏移")]
    public Vector2 mosquitoLocalOffset = new Vector2(0.42f, 0.42f);

    [Tooltip("蚊子在父物体下的缩放系数")]
    public float mosquitoLocalScale = 0.14f;

    [Tooltip("渲染排序，需大于方块底图")]
    public int mosquitoSortingOrder = 5;

    [Header("重音：备用（仅当预制体加载失败时使用单张 Sprite）")]
    public Sprite mosquitoSpriteOverride;

    [Tooltip("Resources 路径，不含扩展名")]
    public string mosquitoSpriteResourcesPath = "mosquito/蚊子1";

    const string MosquitoChildName = "MosquitoIcon";

    Transform _mosquitoIconRoot;
    static GameObject s_cachedPrefabTemplate;
    static Sprite s_cachedFallbackSprite;

    public void SetText(string newText)
    {
        if (syllableText != null)
            syllableText.text = newText;

        UpdateMosquitoIcon(IsStressedSyllableText(newText));
    }

    static bool IsStressedSyllableText(string t)
    {
        if (string.IsNullOrEmpty(t)) return false;
        t = t.Trim();
        if (t.Length == 0) return false;
        if (t != t.ToUpper()) return false;
        foreach (char c in t)
        {
            if (char.IsLetter(c))
                return true;
        }
        return false;
    }

    GameObject ResolvePrefabTemplate()
    {
        if (mosquitoPrefabOverride != null)
            return mosquitoPrefabOverride;

        if (string.IsNullOrEmpty(mosquitoPrefabResourcesPath))
            return null;

        if (s_cachedPrefabTemplate == null)
        {
            s_cachedPrefabTemplate = Resources.Load<GameObject>(mosquitoPrefabResourcesPath);
            if (s_cachedPrefabTemplate == null)
                Debug.LogWarning("SyllableDisplay: 未找到蚊子预制体 Resources 路径: " + mosquitoPrefabResourcesPath);
        }

        return s_cachedPrefabTemplate;
    }

    Sprite ResolveFallbackSprite()
    {
        if (mosquitoSpriteOverride != null)
            return mosquitoSpriteOverride;

        if (s_cachedFallbackSprite != null)
            return s_cachedFallbackSprite;

        if (!string.IsNullOrEmpty(mosquitoSpriteResourcesPath))
        {
            s_cachedFallbackSprite = Resources.Load<Sprite>(mosquitoSpriteResourcesPath);
            if (s_cachedFallbackSprite == null)
            {
                var all = Resources.LoadAll<Sprite>(mosquitoSpriteResourcesPath);
                if (all != null && all.Length > 0)
                    s_cachedFallbackSprite = all[0];
            }
        }

        if (s_cachedFallbackSprite == null)
            Debug.LogWarning("SyllableDisplay: 备用蚊子 Sprite 未找到: " + mosquitoSpriteResourcesPath);

        return s_cachedFallbackSprite;
    }

    void ApplyMosquitoLayout(Transform t)
    {
        t.localRotation = Quaternion.identity;
        t.localPosition = new Vector3(mosquitoLocalOffset.x, mosquitoLocalOffset.y, -0.01f);
        float s = Mathf.Max(0.01f, mosquitoLocalScale);
        t.localScale = new Vector3(s, s, 1f);
    }

    void ApplySortingToRenderers(Transform root)
    {
        var parentSr = GetComponent<SpriteRenderer>();
        int layerId = parentSr != null ? parentSr.sortingLayerID : 0;
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingLayerID = layerId;
            sr.sortingOrder = mosquitoSortingOrder;
        }
    }

    void UpdateMosquitoIcon(bool stressed)
    {
        if (!stressed)
        {
            if (_mosquitoIconRoot != null)
                _mosquitoIconRoot.gameObject.SetActive(false);
            return;
        }

        GameObject prefab = ResolvePrefabTemplate();
        if (prefab != null)
        {
            if (_mosquitoIconRoot != null)
            {
                _mosquitoIconRoot.gameObject.SetActive(true);
                ApplyMosquitoLayout(_mosquitoIconRoot);
                ApplySortingToRenderers(_mosquitoIconRoot);
                var anim = _mosquitoIconRoot.GetComponentInChildren<Animator>(true);
                if (anim != null)
                    anim.enabled = true;

                return;
            }

            GameObject inst = Instantiate(prefab, transform);
            inst.name = MosquitoChildName;
            _mosquitoIconRoot = inst.transform;
            ApplyMosquitoLayout(_mosquitoIconRoot);
            ApplySortingToRenderers(_mosquitoIconRoot);
            var a = _mosquitoIconRoot.GetComponentInChildren<Animator>(true);
            if (a != null)
            {
                a.enabled = true;
                a.Rebind();
            }

            return;
        }

        // —— 备用：单 Sprite + SpriteRenderer（无预制体时）——
        Sprite spr = ResolveFallbackSprite();
        if (spr == null)
            return;

        if (_mosquitoIconRoot != null)
        {
            _mosquitoIconRoot.gameObject.SetActive(true);
            var sr0 = _mosquitoIconRoot.GetComponent<SpriteRenderer>();
            if (sr0 != null)
                sr0.sprite = spr;
            ApplyMosquitoLayout(_mosquitoIconRoot);
            ApplySortingToRenderers(_mosquitoIconRoot);
            return;
        }

        var iconGo = new GameObject(MosquitoChildName);
        iconGo.transform.SetParent(transform, false);
        _mosquitoIconRoot = iconGo.transform;
        var sr = iconGo.AddComponent<SpriteRenderer>();
        sr.sprite = spr;
        sr.color = Color.white;
        ApplyMosquitoLayout(_mosquitoIconRoot);
        ApplySortingToRenderers(_mosquitoIconRoot);
    }
}
