using System;
using UnityEngine;

/// <summary>
/// 可复用的玩法根节点：GameScene 与 TutorialScene 共用。
/// 教程模式下演示单个单词（如 computer），完成后通知 TutorialSceneController。
/// </summary>
public class GameplayRoot : MonoBehaviour
{
    [SerializeField] WordData tutorialWordLibrary;
    [SerializeField] WordSpawner wordSpawner;

    public event Action DemoCompleted;

    void Awake()
    {
        if (wordSpawner == null)
            wordSpawner = GetComponentInChildren<WordSpawner>(true);
    }

    void OnEnable()
    {
        if (wordSpawner != null)
            wordSpawner.onRunComplete.AddListener(HandleRunComplete);
    }

    void OnDisable()
    {
        if (wordSpawner != null)
            wordSpawner.onRunComplete.RemoveListener(HandleRunComplete);
    }

    public void ConfigureForTutorial(WordData wordListOverride = null)
    {
        if (wordSpawner == null)
            wordSpawner = GetComponentInChildren<WordSpawner>(true);
        if (wordSpawner == null)
        {
            Debug.LogError("GameplayRoot: 未找到 WordSpawner。");
            return;
        }

        var library = wordListOverride != null ? wordListOverride : tutorialWordLibrary;
        wordSpawner.tutorialMode = true;
        wordSpawner.autoStartOnEnable = false;
        wordSpawner.skipCountdownInTutorial = true;
        wordSpawner.playBgmInTutorial = false;
        wordSpawner.loopAllWordsForever = false;
        wordSpawner.endSceneName = string.Empty;
        wordSpawner.wordLibrary = library;
    }

    public void BeginTutorialDemo(WordData wordListOverride = null)
    {
        ConfigureForTutorial(wordListOverride);
        gameObject.SetActive(true);

        if (ScoreManager.instance != null)
            ScoreManager.instance.ResetScore();

        wordSpawner.BeginRun();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void HandleRunComplete()
    {
        DemoCompleted?.Invoke();
    }
}
