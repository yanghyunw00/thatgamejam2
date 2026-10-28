using UnityEngine;

/// <summary>
/// 메뉴 씬의 Button.OnClick에 연결하는 용도.
/// 아무 오브젝트에 붙이고, 버튼 인스펙터의 OnClick에서 이 함수들을 고르면 됨.
/// </summary>
public class MenuButtons : MonoBehaviour
{
    public void Play() => SceneLoader.Instance.Load(SceneNames.Game);
    public void Options() => SceneLoader.Instance.Load(SceneNames.Options);
    public void Credits() => SceneLoader.Instance.Load(SceneNames.Credits);
    public void BackToMenu() => SceneLoader.Instance.Load(SceneNames.MainMenu);
    public void Retry() => SceneLoader.Instance.Reload();

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}