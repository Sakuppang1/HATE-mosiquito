using UnityEngine;
using TMPro; // 必须导入这个空间

public class ScoreManager : MonoBehaviour 
{
    // 单例模式，让其他脚本可以随时随地调用 ScoreManager.instance.AddScore(1)
    public static ScoreManager instance;

    // 拖入你在场景中创建的那个 3D Object -> Text - TextMeshPro
    public TextMeshPro scoreText; 

    private int currentScore = 0;

    public int CurrentScore => currentScore;

    /// <summary>新一局开始时将分数清零（例如从结算返回 GameScene）。</summary>
    public void ResetScore()
    {
        currentScore = 0;
        UpdateScoreDisplay();
    }

    void Awake()
    {
        // 确保只有一个 ScoreManager 存在
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 初始化显示
        UpdateScoreDisplay();
    }

    public void AddScore(int amount)
    {
        currentScore += amount;
        
        // 限制分数不小于 0
        if (currentScore < 0) currentScore = 0;

        UpdateScoreDisplay();
    }

    void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString();
        }
        else
        {
            Debug.LogWarning("ScoreText 槽位未在 Inspector 中赋值！");
        }
    }
}