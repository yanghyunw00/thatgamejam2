using DG.Tweening;
using UnityEngine;

/// <summary>
/// 스플래시 연출: 검은 화면 → 팀 이름 페이드 인 → 유지 → 디졸브로 덮고 MainMenu 로드 → 디졸브로 걷힘.
/// (덮기/로드/걷기는 SceneLoader가 처리)
/// </summary>
public class SplashController : MonoBehaviour
{
    [Tooltip("팀 이름 텍스트에 붙인 CanvasGroup")]
    [SerializeField] CanvasGroup title;

    [SerializeField] float startDelay = 0.5f;  // 검은 화면만 보이는 시간
    [SerializeField] float fadeInDuration = 1.2f;  // 팀 이름 페이드 인
    [SerializeField] float holdDuration = 1.5f;  // 팀 이름 유지
    [SerializeField] Ease fadeEase = Ease.OutSine;

    void Start()
    {
        if (title == null)
        {
            Debug.LogError("[Splash] title(CanvasGroup)이 연결 안 됨");
            return;
        }

        title.alpha = 0f;

        DOTween.Sequence()
            .AppendInterval(startDelay)
            .Append(title.DOFade(1f, fadeInDuration).SetEase(fadeEase))
            .AppendInterval(holdDuration)
            .AppendCallback(() => SceneLoader.Instance.Load(SceneNames.MainMenu))
            .SetLink(gameObject);
    }
}