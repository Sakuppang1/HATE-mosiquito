using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class WordSpawner : MonoBehaviour
{
    [Header("配置")]
    public WordData wordLibrary;
    public GameObject syllablePrefab;
    public Transform spawnPoint;
    public AudioSource audioSource; // 在 Inspector 把这个物体身上的 AudioSource 拖进来

    [Header("关卡流程")]
    [Tooltip("为 true 时单词列表无限循环（旧行为）；为 false 时播完一轮后进入结算场景")]
    public bool loopAllWordsForever = false;

    [Tooltip("Build Settings 中的场景名，需与 Assets/Scenes 下文件名一致")]
    public string endSceneName = "EndScene";

    [Tooltip("最后一个单词生成后，等待场上所有音节块移出左侧并销毁，再进入结算")]
    public bool waitForBlocksToLeaveBeforeEnd = true;

    [Tooltip("等待音节块离场的最长时间（秒），超时仍进入结算，避免卡死")]
    public float maxWaitForBlocksClearSeconds = 90f;

    [Header("背景音乐（单词块首次生成时开始循环）")]
    [Tooltip("留空则自动在本物体上添加第二个 AudioSource，专用于 BGM（与单词读音互不干扰）")]
    public AudioSource bgmAudioSource;
    [Tooltip("Resources 下路径，不含扩展名，例如 bgm/gamebgm1")]
    public string bgmResourcePath = "bgm/gamebgm1";
    [Range(0f, 1f)] public float bgmVolume = 0.35f;

    [Header("开局倒计时")]
    [Tooltip("留空则自动查找场景中的 GameStartCountdown")]
    public GameStartCountdown startCountdown;

    [Header("节奏控制 (后期可随时调整)")]
    public float delayBeforeSpawn = 0.8f; // 发音后，等待多久生成方块
    public float timeBetweenWords = 4.0f; // 两个单词之间的总间隔

    [Header("单词背景预览")]
    [Tooltip("为 true 时在音节块下落前显示整词大字背景预览")]
    public bool showWordPreview = true;
    [Tooltip("留空则自动查找场景中的 WordBackgroundPreview")]
    public WordBackgroundPreview wordPreview;
    [Tooltip("音节块生成前多久隐藏预览（秒），须小于 delayBeforeSpawn")]
    public float previewHideBeforeSpawnSeconds = 0.25f;

    [Header("麦克风防误触（不整段屏蔽，避免下一词播音时上一词无法判定）")]
    [Tooltip("每次新单词读音刚开始的一小段时间内完全屏蔽声控，抑制开头脉冲被麦克风拾取导致的误吃")]
    public float micInitialSuppressSeconds = 0.22f;
    [Tooltip("整段单词读音期间：判定区使用 阈值×该系数（>1 更难触发，减轻串音；麦克风仍可用，可大声判定上一词）")]
    public float duringWordThresholdMultiplier = 1.65f;
    [Tooltip("clip 结束后仍短暂视为播读尾声，继续应用上面的阈值倍率，避免尾音误触")]
    public float wordTtsTailPaddingSeconds = 0.12f;

    [Header("教程演示")]
    [Tooltip("为 true 时跑完词表后触发 onRunComplete，不进入结算场景")]
    public bool tutorialMode;

    [Tooltip("为 false 时需调用 BeginRun() 手动开始（教程演示用）")]
    public bool autoStartOnEnable = true;

    [Tooltip("教程模式下跳过 3-2-1 倒计时")]
    public bool skipCountdownInTutorial = true;

    [Tooltip("教程模式下是否播放 BGM")]
    public bool playBgmInTutorial;

    public UnityEvent onRunComplete;

    /// <summary>当前是否处于「单词 TTS + 尾声」窗口（用于提高声控阈值，而非完全禁用麦克风）。</summary>
    public bool IsWithinWordTts => Time.time < _wordTtsEndTime;

    float _wordTtsEndTime;

    bool _bgmStarted;
    bool _sequenceRunning;
    private int currentIndex = 0;

    void Start()
    {
        if (autoStartOnEnable)
            BeginRun();
    }

    public void BeginRun()
    {
        if (_sequenceRunning)
            return;
        StartCoroutine(WordSequence());
    }

    void EnsureBgmAudioSource()
    {
        if (bgmAudioSource != null) return;
        bgmAudioSource = gameObject.AddComponent<AudioSource>();
        bgmAudioSource.playOnAwake = false;
        bgmAudioSource.loop = true;
        bgmAudioSource.spatialBlend = 0f;
        bgmAudioSource.volume = bgmVolume;
    }

    void TryStartBgmOnFirstSpawn()
    {
        if (_bgmStarted) return;
        EnsureBgmAudioSource();
        AudioClip bgm = Resources.Load<AudioClip>(bgmResourcePath);
        if (bgm == null)
        {
            Debug.LogWarning("找不到 BGM，请确认文件在 Assets/Resources/ 下，路径为: " + bgmResourcePath);
            return;
        }
        bgmAudioSource.clip = bgm;
        bgmAudioSource.volume = bgmVolume;
        bgmAudioSource.Play();
        _bgmStarted = true;
    }

    IEnumerator WordSequence()
    {
        _sequenceRunning = true;
        yield return null;

        if (startCountdown == null)
            startCountdown = FindFirstObjectByType<GameStartCountdown>();
        bool skipCountdown = tutorialMode && skipCountdownInTutorial;
        if (!skipCountdown && startCountdown != null)
            yield return startCountdown.PlayCountdown();

        if (ScoreManager.instance != null)
            ScoreManager.instance.ResetScore();

        while (true)
        {
            if (wordLibrary == null || wordLibrary.wordList.Count == 0)
            {
                yield return null;
                continue;
            }

            if (loopAllWordsForever)
            {
                yield return RunOneWord(wordLibrary.wordList[currentIndex]);
                yield return new WaitForSeconds(timeBetweenWords - delayBeforeSpawn);
                currentIndex = (currentIndex + 1) % wordLibrary.wordList.Count;
            }
            else
            {
                for (currentIndex = 0; currentIndex < wordLibrary.wordList.Count; currentIndex++)
                {
                    yield return RunOneWord(wordLibrary.wordList[currentIndex]);
                    if (currentIndex < wordLibrary.wordList.Count - 1)
                        yield return new WaitForSeconds(timeBetweenWords - delayBeforeSpawn);
                }

                if (waitForBlocksToLeaveBeforeEnd)
                    yield return WaitForAllSyllableBlocksCleared();

                FinishRun();
                yield break;
            }
        }
    }

    IEnumerator RunOneWord(WordItem selectedWord)
    {
        if (wordPreview == null)
            wordPreview = FindFirstObjectByType<WordBackgroundPreview>();

        string previewLabel = WordBackgroundPreview.FormatDisplayWord(selectedWord);
        if (showWordPreview && wordPreview != null && !string.IsNullOrEmpty(previewLabel))
            wordPreview.Show(previewLabel);

        AudioClip clip = Resources.Load<AudioClip>("Audio/" + selectedWord.wordName.ToLower());
        if (clip != null)
        {
            _wordTtsEndTime = Time.time + clip.length + wordTtsTailPaddingSeconds;
            MicGameplayAudioGate.BlockFor(micInitialSuppressSeconds);
            audioSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning("找不到音频: " + selectedWord.wordName);
        }

        float spawnDelay = Mathf.Max(0f, delayBeforeSpawn);
        float hideLead = Mathf.Clamp(previewHideBeforeSpawnSeconds, 0.05f, spawnDelay);
        float previewVisibleSeconds = Mathf.Max(0.1f, spawnDelay - hideLead);

        if (showWordPreview && wordPreview != null)
        {
            yield return new WaitForSeconds(previewVisibleSeconds);
            wordPreview.Hide();
            float afterHide = spawnDelay - previewVisibleSeconds;
            if (afterHide > 0f)
                yield return new WaitForSeconds(afterHide);
        }
        else
        {
            yield return new WaitForSeconds(spawnDelay);
        }

        SpawnWord(selectedWord);
    }

    IEnumerator WaitForAllSyllableBlocksCleared()
    {
        float elapsed = 0f;
        while (elapsed < maxWaitForBlocksClearSeconds)
        {
            SyllableMover[] blocks = FindObjectsByType<SyllableMover>(FindObjectsSortMode.None);
            if (blocks == null || blocks.Length == 0)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("WordSpawner: 等待音节块离场超时，仍将进入 " + endSceneName);
    }

    void FinishRun()
    {
        if (bgmAudioSource != null && bgmAudioSource.isPlaying)
            bgmAudioSource.Stop();

        GameSession.WordsCompleted = wordLibrary != null ? wordLibrary.wordList.Count : 0;
        GameSession.LastScore = ScoreManager.instance != null ? ScoreManager.instance.CurrentScore : 0;

        _sequenceRunning = false;

        if (tutorialMode)
        {
            onRunComplete?.Invoke();
            return;
        }

        if (!string.IsNullOrEmpty(endSceneName))
            SceneManager.LoadScene(endSceneName);
        else
            Debug.LogWarning("WordSpawner: endSceneName 为空，无法进入结算场景。");
    }

    void SpawnWord(WordItem word)
    {
        if (!tutorialMode || playBgmInTutorial)
            TryStartBgmOnFirstSpawn();

        float offset = 0;
        foreach (var syl in word.syllables)
        {
            GameObject block = Instantiate(syllablePrefab, spawnPoint.position + new Vector3(offset, 0, 0), Quaternion.identity);
            SyllableDisplay display = block.GetComponent<SyllableDisplay>();
            if (display != null)
            {
                display.SetText(syl.isStressed ? syl.text.ToUpper() : syl.text.ToLower());
            }
            var mover = block.GetComponent<SyllableMover>();
            if (mover != null)
                mover.SetMosquito(syl.isStressed);
            offset += 2.0f;
        }
    }
}
