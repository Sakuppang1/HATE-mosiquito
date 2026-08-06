using UnityEngine;

/// <summary>
/// 仅在极短时间内屏蔽声控吃块（例如新单词读音开头脉冲）。整段播音防误触由 WordSpawner + CenterZone 提高阈值完成，避免长时间无法判定。
/// </summary>
public static class MicGameplayAudioGate
{
    static float s_blockUntilTime;

    public static bool IsBlocking => Time.time < s_blockUntilTime;

    /// <summary>从当前时刻起，在若干秒内忽略声控吃块（多次调用取结束时间最晚的一次）。</summary>
    public static void BlockFor(float durationSeconds)
    {
        if (durationSeconds <= 0f) return;
        float end = Time.time + durationSeconds;
        if (end > s_blockUntilTime)
            s_blockUntilTime = end;
    }

    public static void Clear()
    {
        s_blockUntilTime = 0f;
    }
}
