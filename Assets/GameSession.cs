using UnityEngine;

/// <summary>跨场景保存一局结束时的统计（结算页读取）。</summary>
public static class GameSession
{
    public static int LastScore;
    public static int WordsCompleted;

    public static void Clear()
    {
        LastScore = 0;
        WordsCompleted = 0;
    }
}
