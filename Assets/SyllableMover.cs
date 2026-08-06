using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SyllableMover : MonoBehaviour
{
    public float stepSize = 2.0f;
    public float destroyX = -12.0f;
    [Range(0.1f, 5.0f)]
    public float volumeMultiplier = 2.0f;

    private AudioSource audioSource;
    private bool hasMosquito = false;
    private bool centerCuePlayed;

    /// <summary>是否为重音音节（带蚊子，需玩家自己读）。</summary>
    public bool IsStressed => hasMosquito;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    public void SetMosquito(bool isStressed)
    {
        hasMosquito = isStressed;
    }

    /// <summary>进判定区时标记「提示音已处理」，返回 true 表示本次应播放（仅非重音且尚未播过）。</summary>
    public bool TryConsumeUnstressedCenterCue()
    {
        if (hasMosquito || centerCuePlayed)
            return false;
        centerCuePlayed = true;
        return true;
    }

    public void MoveOneStep(AudioClip clip)
    {
        transform.position += Vector3.left * stepSize;

        // 只有携带蚊子（重音）时才播放蚊子飞声（移动反馈，不是进区机械提示）
        if (hasMosquito && audioSource != null && clip != null)
        {
            if (!audioSource.isPlaying)
                audioSource.PlayOneShot(clip, volumeMultiplier);
        }

        if (transform.position.x <= destroyX)
            Destroy(gameObject);
    }

    public void StopSound()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }
}