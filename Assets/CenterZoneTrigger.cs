using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;

public class CenterZoneController : MonoBehaviour
{
    [Header("外观设置")]
    public Color normalColor = Color.white;
    public Color highlightColor = Color.yellow;

    [Header("青蛙动画")]
    public Animator frogAnimator;
    public string tongueTriggerName = "Tongue";

    [Header("吃到重音音节音效")]
    [Tooltip("留空则从 Resources 加载；路径不含扩展名，例如 bgm/青蛙声")]
    public string stressedCorrectSfxResourcePath = "bgm/青蛙声";

    [Tooltip("若指定则优先于 Resources 路径")]
    public AudioClip stressedCorrectSfxOverride;

    [Range(0f, 1f)]
    public float stressedCorrectSfxVolume = 1f;

    [Tooltip("留空则在本物体上自动添加 AudioSource（2D，不循环）")]
    public AudioSource stressedCorrectSfxSource;

    [Header("进区：非重音节机械提示音")]
    [Tooltip("非重音节进入中间判定区时播放；重音不播，等玩家自己读。留空则用程序生成的机械哔声")]
    public AudioClip unstressedCenterCueOverride;

    [Tooltip("可选 Resources 路径（不含扩展名）；仅当 Override 为空时使用")]
    public string unstressedCenterCueResourcePath = "";

    [Range(0f, 1f)]
    public float unstressedCenterCueVolume = 0.55f;

    [Tooltip("播机械提示音时短暂屏蔽声控，避免喇叭串音误触发")]
    public float unstressedCenterCueMicBlockSeconds = 0.18f;

    [Tooltip("程序生成哔声的基础频率（Hz）；不同音节会略微偏移")]
    public float proceduralCueBaseHz = 520f;

    [Tooltip("程序生成哔声时长（秒）")]
    public float proceduralCueDuration = 0.11f;

    [Header("麦克风音量 UI")]
    [Tooltip("进入场景后显示音量条（实时音量 + 触发/复位阈值线）")]
    public bool showMicLevelBar = true;

    [Tooltip("拖入场景里的 MicVolumeHud（推荐）。位置/大小在 MicVolumeHud 的 Mic Volume Bar UI 上改；留空则运行时自动创建")]
    [SerializeField] MicVolumeBarUI micVolumeBarUi;

    [Header("与单词播音联动")]
    [Tooltip("留空则自动查找场景中的 WordSpawner")]
    public WordSpawner wordSpawner;

    [Header("声控：相对环境突增（默认，适合耳机漏音/底噪）")]
    [Tooltip("关闭后使用下方「绝对阈值 + 滞回」旧逻辑")]
    public bool useRelativeAmbientMode = true;
    [Tooltip("当前音量低于等于环境估计时，环境跟踪速度（越大越快跟上安静）")]
    public float ambientFollowDownSpeed = 14f;
    [Tooltip("当前音量高于环境估计时，环境上升速度（越小越像「底噪」：持续漏音不会立刻抬高基线，便于检测「突然」喊一声）")]
    public float ambientFollowUpSpeed = 0.65f;
    [Tooltip("必须比环境基线高出这么多（同一套 RMS×灵敏度 单位），才认为足够响")]
    public float excursionOverAmbientTrigger = 0.09f;
    [Tooltip("「突出度」：当前超出量相对其平滑值的突刺；用于识别突然一嗓子（播读时会乘倍率）")]
    public float prominenceTrigger = 0.045f;
    [Tooltip("突出度低于此 + 相对音量回落后才允许下一次触发")]
    public float prominenceRearm = 0.018f;
    public float excursionRearm = 0.03f;
    [Tooltip("开局若干秒：只学习环境、不吃块")]
    public float micWarmupSeconds = 0.75f;

    [Header("声控：绝对阈值（仅当关闭相对环境模式时生效）")]
    public float sensitivity = 100f;
    public float threshold = 0.5f;
    public float releaseThreshold = -1f;
    public float eatCooldown = 0.5f;
    private float lastEatTime;

    private bool micArmed = true;

    /// <summary>最近一帧麦克风 RMS×灵敏度。</summary>
    public float CurrentMicLevel { get; private set; }

    /// <summary>估计的环境底噪（同一单位）。</summary>
    public float CurrentAmbientLevel { get; private set; }

    /// <summary>当前比环境高出多少（max(0, 原音-环境)）。</summary>
    public float CurrentExcursion { get; private set; }

    /// <summary>突刺程度（相对平滑超出量的突出），用于「突然」判定。</summary>
    public float CurrentProminence { get; private set; }

    /// <summary>兼容旧 UI：相对模式下等同 CurrentProminence。</summary>
    public float CurrentSpikeLevel => CurrentProminence;

    /// <summary>音量条主电平：相对模式用 CurrentExcursion，绝对模式用 CurrentMicLevel。</summary>
    public float UiMeterLevel { get; private set; }

    public float TriggerThresholdDisplay { get; private set; }
    public float ReleaseThresholdDisplay { get; private set; }

    float _ambientEstimate;
    float _excursionSmooth;
    bool _envInitialized;
    float _micWarmupEndTime;
    bool _postWarmupSmoothedInited;

    static AudioClip s_cachedStressedCorrectSfx;
    static AudioClip s_cachedUnstressedCenterCue;
    static AudioClip s_proceduralCenterCue;

    private AudioClip micClip;
    private float[] audioSamples = new float[256];
    private List<GameObject> blocksInRange = new List<GameObject>();

    /// <summary>带 Syllable 标签的块进入中间判定区时触发（用于背景整词预览等）。</summary>
    public event System.Action<GameObject> SyllableEnteredCenter;
    AudioSource _centerCueSource;

    /// <summary>上一帧 Microphone.GetPosition，用于检测写入头停滞。</summary>
    int _lastMicSamplePos = -1;
    float _lastScaledMicOutput;

    // #region agent log
    static string AgentDbgPath() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "debug-85435f.log"));
    static float _agentDbgLastSummary;
    static float _agentDbgLastMic;

    static void AgentDbgLine(string hypothesisId, string location, string message, string jsonObjectNoBraces)
    {
        try
        {
            long ts = (long)(Time.realtimeSinceStartup * 1000f);
            string line = "{\"sessionId\":\"85435f\",\"hypothesisId\":\"" + hypothesisId + "\",\"location\":\"" + location +
                          "\",\"message\":\"" + message + "\",\"data\":{" + jsonObjectNoBraces + "},\"timestamp\":" + ts + "}\n";
            File.AppendAllText(AgentDbgPath(), line);
        }
        catch { /* ignore */ }
    }
    // #endregion

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            micClip = Microphone.Start(null, true, 1, 44100);
            Debug.Log("麦克风已就绪。");
        }
        else
        {
            Debug.LogError("未检测到麦克风！");
        }

        if (showMicLevelBar)
        {
            if (micVolumeBarUi == null)
                micVolumeBarUi = FindFirstObjectByType<MicVolumeBarUI>();

            if (micVolumeBarUi == null)
            {
                var hudGo = new GameObject("MicVolumeHud");
                micVolumeBarUi = hudGo.AddComponent<MicVolumeBarUI>();
            }

            micVolumeBarUi.Initialize(this);
        }

        if (wordSpawner == null)
            wordSpawner = FindFirstObjectByType<WordSpawner>();

        _micWarmupEndTime = Time.time + Mathf.Max(0f, micWarmupSeconds);
        TriggerThresholdDisplay = threshold;
        ReleaseThresholdDisplay = EffectiveReleaseThreshold();
        EnsureStressedCorrectSfxSource();
    }

    void EnsureStressedCorrectSfxSource()
    {
        if (stressedCorrectSfxSource != null) return;
        stressedCorrectSfxSource = GetComponent<AudioSource>();
        if (stressedCorrectSfxSource != null) return;

        stressedCorrectSfxSource = gameObject.AddComponent<AudioSource>();
        stressedCorrectSfxSource.playOnAwake = false;
        stressedCorrectSfxSource.loop = false;
        stressedCorrectSfxSource.spatialBlend = 0f;
    }

    AudioClip ResolveStressedCorrectSfx()
    {
        if (stressedCorrectSfxOverride != null)
            return stressedCorrectSfxOverride;

        if (string.IsNullOrEmpty(stressedCorrectSfxResourcePath))
            return null;

        if (s_cachedStressedCorrectSfx == null)
        {
            s_cachedStressedCorrectSfx = Resources.Load<AudioClip>(stressedCorrectSfxResourcePath);
            if (s_cachedStressedCorrectSfx == null)
                Debug.LogWarning("CenterZone: 未找到重音正确音效 Resources 路径: " + stressedCorrectSfxResourcePath);
        }

        return s_cachedStressedCorrectSfx;
    }

    void PlayStressedCorrectSfx()
    {
        AudioClip clip = ResolveStressedCorrectSfx();
        if (clip == null) return;

        EnsureStressedCorrectSfxSource();
        if (stressedCorrectSfxSource == null) return;

        stressedCorrectSfxSource.PlayOneShot(clip, stressedCorrectSfxVolume);
    }

    AudioClip ResolveUnstressedCenterCue()
    {
        if (unstressedCenterCueOverride != null)
            return unstressedCenterCueOverride;

        if (!string.IsNullOrEmpty(unstressedCenterCueResourcePath))
        {
            if (s_cachedUnstressedCenterCue == null)
            {
                s_cachedUnstressedCenterCue = Resources.Load<AudioClip>(unstressedCenterCueResourcePath);
                if (s_cachedUnstressedCenterCue == null)
                    Debug.LogWarning("CenterZone: 未找到非重音进区提示音: " + unstressedCenterCueResourcePath);
            }
            if (s_cachedUnstressedCenterCue != null)
                return s_cachedUnstressedCenterCue;
        }

        return GetOrCreateProceduralCenterCue();
    }

    AudioClip GetOrCreateProceduralCenterCue()
    {
        if (s_proceduralCenterCue != null)
            return s_proceduralCenterCue;

        const int sampleRate = 22050;
        float duration = Mathf.Clamp(proceduralCueDuration, 0.04f, 0.35f);
        int sampleCount = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        float hz = Mathf.Clamp(proceduralCueBaseHz, 180f, 1400f);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float env = 1f;
            float attack = 0.008f;
            float release = 0.035f;
            if (t < attack)
                env = t / attack;
            else if (t > duration - release)
                env = Mathf.Max(0f, (duration - t) / release);

            // 方波 + 轻微二次谐波，偏机械感
            float phase = t * hz;
            float square = (phase - Mathf.Floor(phase)) < 0.5f ? 1f : -1f;
            float harmonic = Mathf.Sin(t * hz * 2f * Mathf.PI) * 0.22f;
            samples[i] = (square * 0.55f + harmonic) * env;
        }

        s_proceduralCenterCue = AudioClip.Create("ProceduralSyllableCue", sampleCount, 1, sampleRate, false);
        s_proceduralCenterCue.SetData(samples, 0);
        return s_proceduralCenterCue;
    }

    void PlayUnstressedCenterCue(GameObject block)
    {
        AudioClip clip = ResolveUnstressedCenterCue();
        if (clip == null) return;

        if (_centerCueSource == null)
        {
            _centerCueSource = gameObject.AddComponent<AudioSource>();
            _centerCueSource.playOnAwake = false;
            _centerCueSource.loop = false;
            _centerCueSource.spatialBlend = 0f;
        }

        float pitch = 1f;
        var tmp = block != null ? block.GetComponentInChildren<TextMeshPro>() : null;
        if (tmp != null && !string.IsNullOrEmpty(tmp.text))
        {
            int h = tmp.text.Trim().ToLowerInvariant().GetHashCode();
            pitch = 0.92f + (Mathf.Abs(h) % 17) * 0.012f;
        }

        _centerCueSource.pitch = pitch;
        _centerCueSource.PlayOneShot(clip, unstressedCenterCueVolume);

        MicGameplayAudioGate.BlockFor(unstressedCenterCueMicBlockSeconds);
    }

    void TryPlayCenterSyllableCue(GameObject block)
    {
        if (block == null) return;

        var mover = block.GetComponent<SyllableMover>();
        if (mover != null)
        {
            // 重音：不播机械音，等玩家自己读
            if (mover.IsStressed)
                return;
            if (!mover.TryConsumeUnstressedCenterCue())
                return;
        }
        else if (IsStressedSyllable(block))
        {
            return;
        }

        PlayUnstressedCenterCue(block);
    }

    float EffectiveReleaseThreshold()
    {
        float low = releaseThreshold < 0f ? threshold * 0.55f : releaseThreshold;
        return Mathf.Min(low, threshold * 0.9f);
    }

    void UpdateAmbientEstimate(float vol, float dt, bool warmup)
    {
        if (!_envInitialized)
        {
            _ambientEstimate = vol;
            _envInitialized = true;
            return;
        }

        if (warmup)
        {
            float kw = 1f - Mathf.Exp(-18f * dt);
            _ambientEstimate = Mathf.Lerp(_ambientEstimate, vol, kw);
            return;
        }

        if (vol <= _ambientEstimate)
        {
            float k = 1f - Mathf.Exp(-Mathf.Max(0.05f, ambientFollowDownSpeed) * dt);
            _ambientEstimate = Mathf.Lerp(_ambientEstimate, vol, k);
        }
        else
        {
            float k = 1f - Mathf.Exp(-Mathf.Max(0.02f, ambientFollowUpSpeed) * dt);
            _ambientEstimate = Mathf.Lerp(_ambientEstimate, vol, k);
        }
    }

    void Update()
    {
        float vol = GetMicVolume();
        CurrentMicLevel = vol;

        if (wordSpawner == null)
            wordSpawner = FindFirstObjectByType<WordSpawner>();

        float ttsMult = 1f;
        if (wordSpawner != null && wordSpawner.IsWithinWordTts)
            ttsMult = Mathf.Max(1f, wordSpawner.duringWordThresholdMultiplier);

        float dt = Time.deltaTime;
        bool warmup = Time.time < _micWarmupEndTime;

        if (useRelativeAmbientMode)
        {
            UpdateAmbientEstimate(vol, dt, warmup);

            float excursion = Mathf.Max(0f, vol - _ambientEstimate);
            CurrentAmbientLevel = _ambientEstimate;
            CurrentExcursion = excursion;

            if (!warmup && !_postWarmupSmoothedInited)
            {
                _excursionSmooth = excursion;
                _postWarmupSmoothedInited = true;
            }

            float lagK = 1f - Mathf.Exp(-7.5f * dt);
            _excursionSmooth = Mathf.Lerp(_excursionSmooth, excursion, lagK);
            float prominence = Mathf.Max(0f, excursion - _excursionSmooth);
            CurrentProminence = prominence;

            float excTrig = excursionOverAmbientTrigger * ttsMult;
            float promTrig = prominenceTrigger * ttsMult;
            TriggerThresholdDisplay = excTrig;
            ReleaseThresholdDisplay = prominenceRearm;

            UiMeterLevel = excursion;

            // #region agent log
            float rt = Time.realtimeSinceStartup;
            if (rt - _agentDbgLastSummary >= 0.45f)
            {
                _agentDbgLastSummary = rt;
                var inv = CultureInfo.InvariantCulture;
                AgentDbgLine("H2", "CenterZoneTrigger:Update:relative",
                    "relative_mode_state",
                    "\"useRelative\":true,\"vol\":" + vol.ToString(inv) +
                    ",\"ambient\":" + _ambientEstimate.ToString(inv) +
                    ",\"ambMinusVol\":" + (_ambientEstimate - vol).ToString(inv) +
                    ",\"excursion\":" + excursion.ToString(inv) +
                    ",\"excursionGtExcTrig\":" + (excursion > excTrig ? "true" : "false") +
                    ",\"prominence\":" + prominence.ToString(inv) +
                    ",\"excTrig\":" + excTrig.ToString(inv) +
                    ",\"promTrig\":" + promTrig.ToString(inv) +
                    ",\"ttsMult\":" + ttsMult.ToString(inv) +
                    ",\"warmup\":" + (warmup ? "true" : "false"));
            }
            // #endregion
        }
        else
        {
            float release = EffectiveReleaseThreshold() * ttsMult;
            float triggerThreshold = threshold * ttsMult;
            TriggerThresholdDisplay = triggerThreshold;
            ReleaseThresholdDisplay = release;
            CurrentAmbientLevel = 0f;
            CurrentExcursion = 0f;
            CurrentProminence = 0f;
            UiMeterLevel = vol;

            // #region agent log
            float rt2 = Time.realtimeSinceStartup;
            if (rt2 - _agentDbgLastSummary >= 0.45f)
            {
                _agentDbgLastSummary = rt2;
                var inv = CultureInfo.InvariantCulture;
                AgentDbgLine("H3", "CenterZoneTrigger:Update:absolute",
                    "absolute_mode_state",
                    "\"useRelative\":false,\"vol\":" + vol.ToString(inv) +
                    ",\"trigger\":" + triggerThreshold.ToString(inv) +
                    ",\"release\":" + release.ToString(inv) +
                    ",\"ttsMult\":" + ttsMult.ToString(inv));
            }
            // #endregion
        }

        if (MicGameplayAudioGate.IsBlocking)
        {
            micArmed = false;
            return;
        }

        if (warmup)
            return;

        if (useRelativeAmbientMode)
        {
            float excTrig = excursionOverAmbientTrigger * ttsMult;
            float promTrig = prominenceTrigger * ttsMult;

            if (CurrentProminence < prominenceRearm && CurrentExcursion < excursionRearm * ttsMult)
                micArmed = true;

            if (micArmed &&
                CurrentExcursion > excTrig &&
                CurrentProminence > promTrig &&
                Time.time >= lastEatTime + Mathf.Max(0.05f, eatCooldown))
            {
                EatOneBlock(vol);
                micArmed = false;
            }
        }
        else
        {
            float release = EffectiveReleaseThreshold() * ttsMult;
            float triggerThreshold = threshold * ttsMult;

            if (vol < release)
                micArmed = true;

            if (micArmed && vol > triggerThreshold && Time.time >= lastEatTime + Mathf.Max(0.05f, eatCooldown))
            {
                EatOneBlock(vol);
                micArmed = false;
            }
        }
    }

    void EatOneBlock(float volAtEat)
    {
        // 无论是否真有方块可吃，一次「声控触发」都应消耗冷却；否则 lastEatTime 不前进，
        // 在「无块 / 块未进区」但音量条件持续满足时，会反复 rear → 反复判定为触发（像是一直在触发）。
        lastEatTime = Time.time;

        if (blocksInRange.Count > 0)
        {
            GameObject block = blocksInRange[0];

            if (block != null)
            {
                if (IsStressedSyllable(block))
                {
                    ScoreManager.instance.AddScore(1);
                    PlayStressedCorrectSfx();
                }
                else
                    ScoreManager.instance.AddScore(-1);

                blocksInRange.RemoveAt(0);

                if (frogAnimator != null && !string.IsNullOrEmpty(tongueTriggerName))
                    frogAnimator.SetTrigger(tongueTriggerName);

                Destroy(block);
                Debug.Log("成功吃掉一个音节！");
            }
        }

        if (useRelativeAmbientMode)
        {
            _ambientEstimate = Mathf.Lerp(_ambientEstimate, volAtEat, 0.35f);
            float newExc = Mathf.Max(0f, volAtEat - _ambientEstimate);
            _excursionSmooth = Mathf.Lerp(_excursionSmooth, newExc, 0.5f);
            // 平滑值不得低于当前超出量，否则 prominence = excursion - smooth 会在同一嗓门下持续为正，造成连续多帧满足「突刺」条件
            _excursionSmooth = Mathf.Max(_excursionSmooth, newExc);
        }
    }

    static bool IsStressedSyllable(GameObject block)
    {
        if (block == null) return false;
        var tmp = block.GetComponentInChildren<TextMeshPro>();
        if (tmp == null) return false;
        string t = tmp.text.Trim();
        if (string.IsNullOrEmpty(t)) return false;
        return t == t.ToUpper();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Syllable"))
        {
            SpriteRenderer sr = collision.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = highlightColor;

            blocksInRange.Add(collision.gameObject);
            TryPlayCenterSyllableCue(collision.gameObject);
            SyllableEnteredCenter?.Invoke(collision.gameObject);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Syllable"))
        {
            SpriteRenderer sr = collision.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = normalColor;

            blocksInRange.Remove(collision.gameObject);
        }
    }

    float GetMicVolume()
    {
        if (micClip == null) return 0f;

        const int chunk = 256;
        int clipSamples = micClip.samples;
        if (clipSamples < chunk) return 0f;

        int micPos = Microphone.GetPosition(null);
        if (micPos < 0) return 0f;

        // 写入头尚未跨过 chunk：读环缓冲会越界或需分段，旧代码用 offset=0 会从缓冲「开头」取数，
        // 与当前麦克风写入位置无关 → 静音时仍可出现恒定的假 scaled（日志：micPos==0 && offset==0）。
        if (micPos < chunk) return 0f;

        if (micPos == _lastMicSamplePos)
            return _lastScaledMicOutput;

        int offset = micPos - chunk;
        micClip.GetData(audioSamples, offset);

        float total = 0;
        float smin = float.MaxValue;
        float smax = float.MinValue;
        foreach (float s in audioSamples)
        {
            total += Mathf.Abs(s);
            if (s < smin) smin = s;
            if (s > smax) smax = s;
        }
        float rmsMean = total / chunk;
        float scaled = rmsMean * sensitivity;

        // #region agent log
        float rt = Time.realtimeSinceStartup;
        if (rt - _agentDbgLastMic >= 0.45f)
        {
            _agentDbgLastMic = rt;
            var inv = CultureInfo.InvariantCulture;
            string dev = Microphone.devices.Length > 0 ? Microphone.devices[0].Replace("\\", "/") : "none";
            AgentDbgLine("H1", "CenterZoneTrigger:GetMicVolume", "mic_buffer_stats",
                "\"rmsMean\":" + rmsMean.ToString(inv) +
                ",\"scaled\":" + scaled.ToString(inv) +
                ",\"sensitivity\":" + sensitivity.ToString(inv) +
                ",\"micPos\":" + micPos +
                ",\"offset\":" + offset +
                ",\"clipSamples\":" + clipSamples +
                ",\"micPosLtChunk\":" + (micPos < chunk ? "true" : "false") +
                ",\"smin\":" + smin.ToString(inv) +
                ",\"smax\":" + smax.ToString(inv) +
                ",\"clipHz\":" + (micClip != null ? micClip.frequency : 0) +
                ",\"device\":\"" + dev + "\"");
        }
        // #endregion

        _lastScaledMicOutput = scaled;
        _lastMicSamplePos = micPos;
        return scaled;
    }
}
