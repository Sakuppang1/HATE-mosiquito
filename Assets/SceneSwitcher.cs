using UnityEngine;
using UnityEngine.SceneManagement; // 必须引用这个命名空间

public class SceneSwitcher : MonoBehaviour
{
    // 这个方法可以被按钮调用
    public void LoadGameScene()
    {
        // 这里的 "GameScene" 必须和你 Build Settings 里的场景文件名一模一样
        SceneManager.LoadScene("TutorialScene");
    }
}