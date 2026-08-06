using UnityEngine;

public class BeatManager : MonoBehaviour
{
    [Header("节奏设置")]
    public float beatInterval = 0.8f; 
    private float timer;

    [Header("声音设置")]
    public AudioClip mosquitoClip;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= beatInterval)
        {
            // 每当节拍时间到，触发所有单词块移动一次
            SyllableMover[] allBlocks = FindObjectsByType<SyllableMover>(FindObjectsSortMode.None);
            foreach (SyllableMover block in allBlocks)
            {
                block.MoveOneStep(mosquitoClip); 
            }
            
            // 重置计时器
            timer = 0;
        }
    }
}