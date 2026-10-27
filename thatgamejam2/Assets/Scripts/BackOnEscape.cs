using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// ESC를 누르면 지정한 씬으로 돌아감 (기본: MainMenu). Options·Credits 씬에 빈 오브젝트로 하나 두면 됨.
/// </summary>
public class BackOnEscape : MonoBehaviour
{
    [SerializeField] string targetScene = SceneNames.MainMenu;

    void Update()
    {
        if (EscapePressed()) SceneLoader.Instance.Load(targetScene); // 전환 중이면 SceneLoader가 알아서 무시
    }

    static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}