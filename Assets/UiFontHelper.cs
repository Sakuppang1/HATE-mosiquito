using UnityEngine;
using UnityEngine.UI;

/// <summary>为 UGUI Text 解析可显示中文的系统字体（Windows 常用微软雅黑 / 黑体等）。</summary>
public static class UiFontHelper
{
    static Font s_cached;

    static readonly string[] PreferredFontNames =
    {
        "Microsoft YaHei UI",
        "Microsoft YaHei",
        "SimHei",
        "PingFang SC",
        "Noto Sans CJK SC",
        "Arial Unicode MS"
    };

    public static Font GetChineseFont(int size = 32)
    {
        if (s_cached != null)
            return s_cached;

        foreach (var name in PreferredFontNames)
        {
            var font = Font.CreateDynamicFontFromOSFont(name, size);
            if (font != null)
            {
                s_cached = font;
                return s_cached;
            }
        }

        s_cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_cached == null)
            s_cached = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return s_cached;
    }

    public static void ApplyTo(Text text, int fontSize)
    {
        if (text == null)
            return;
        text.font = GetChineseFont(fontSize);
        text.fontSize = fontSize;
        text.supportRichText = false;
    }
}
