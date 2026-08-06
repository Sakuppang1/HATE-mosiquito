using UnityEngine;

/// <summary>StartScene 可点击蚊子；由 Intro 控制器检测点击。</summary>
public class StartSceneMosquito : MonoBehaviour
{
    StartSceneIntroController _intro;

    public void Initialize(StartSceneIntroController intro)
    {
        _intro = intro;
    }

    public void NotifyClicked()
    {
        if (_intro != null)
            _intro.OnMosquitoClicked(gameObject);
    }
}
