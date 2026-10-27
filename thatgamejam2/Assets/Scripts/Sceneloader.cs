using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 전역 씬 전환기 (DOTween + 디졸브). 씬에 배치할 필요 없음 — 게임 시작 시 자동 생성, DontDestroyOnLoad.
///
/// 사용법:
///   SceneLoader.Instance.Load(SceneNames.Game);      // 디졸브로 덮기 → 비동기 로드 → 디졸브로 걷기
///   SceneLoader.Instance.Reload();                   // 현재 씬 다시 로드
///   SceneLoader.Instance.FadeThrough(() => {...});   // 씬 로드 없이 덮기 → 콜백 → 걷기 (맵 재생성용)
///
/// 필요 파일: Resources 폴더 안의 TransitionDissolve.shader
/// 셰이더를 못 찾으면 경고를 띄우고 일반 알파 페이드로 동작함.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    const string ShaderName = "UI/TransitionDissolve";
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int AspectId = Shader.PropertyToID("_Aspect");
    static readonly int NoiseOffsetId = Shader.PropertyToID("_NoiseOffset");
    static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    static readonly int SoftnessId = Shader.PropertyToID("_Softness");

    // ── 튜닝값 (런타임 생성 오브젝트라 인스펙터 대신 여기서 수정) ──
    public float CoverDuration = 0.6f;          // 화면 덮는 시간
    public float RevealDuration = 0.6f;          // 화면 걷는 시간
    public float MinHoldTime = 0.1f;          // 완전히 덮인 상태 최소 유지 시간
    public Ease CoverEase = Ease.InOutSine;
    public Ease RevealEase = Ease.InOutSine;
    public float NoiseScale = 6f;            // 클수록 디졸브 무늬가 잘게
    public float EdgeSoftness = 0.04f;         // 경계 부드러움

    /// <summary>전환 시작 시 (인자: 목적지 씬 이름, FadeThrough면 null)</summary>
    public event Action<string> TransitionStarted;
    /// <summary>화면이 완전히 덮였을 때 (씬 로드 직전 / 콜백 직전)</summary>
    public event Action<string> ScreenCovered;
    /// <summary>걷기까지 완전히 끝났을 때</summary>
    public event Action<string> TransitionFinished;

    public bool IsTransitioning { get; private set; }

    Image _overlay;
    Material _mat;
    bool _useDissolve;
    Color _color = Color.black;
    float _progress;
    Tween _tween;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("[SceneLoader]").AddComponent<SceneLoader>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        Progress = 0f;
    }

    void OnDestroy()
    {
        _tween?.Kill();
        if (_mat != null) Destroy(_mat);
        if (Instance == this) Instance = null;
    }

    void BuildOverlay()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // 모든 UI 위
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        var go = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _overlay = go.GetComponent<Image>();
        _overlay.raycastTarget = true; // 켜져 있는 동안 클릭 차단

        var shader = Shader.Find(ShaderName);
        _useDissolve = shader != null;
        if (_useDissolve)
        {
            _mat = new Material(shader);
            _overlay.material = _mat;
        }
        else
        {
            Debug.LogWarning($"[SceneLoader] '{ShaderName}' 셰이더를 못 찾음 → 알파 페이드로 대체. " +
                             "TransitionDissolve.shader가 Resources 폴더 안에 있는지 확인.");
        }
    }

    /// <summary>0 = 투명, 1 = 완전히 덮음</summary>
    float Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            if (_useDissolve)
            {
                _mat.SetFloat(ProgressId, value);
                _overlay.color = _color;
            }
            else
            {
                var c = _color;
                c.a *= value;
                _overlay.color = c;
            }
            _overlay.enabled = value > 0f; // 0이면 렌더·레이캐스트 모두 끔
        }
    }

    // ───────────── Public API ─────────────

    public void Load(string sceneName)
    {
        if (IsTransitioning) return; // 버튼 연타 방지
        StartCoroutine(LoadRoutine(sceneName));
    }

    public void Reload() => Load(SceneManager.GetActiveScene().name);

    /// <summary>화면을 덮은 상태에서 atMidpoint를 실행하고 다시 걷어냄. 씬은 그대로.</summary>
    public void FadeThrough(Action atMidpoint)
    {
        if (IsTransitioning) return;
        StartCoroutine(FadeThroughRoutine(atMidpoint));
    }

    public void SetColor(Color c)
    {
        _color = c;
        Progress = _progress; // 즉시 반영
    }

    // ───────────── Routines ─────────────

    IEnumerator LoadRoutine(string sceneName)
    {
        IsTransitioning = true;
        TransitionStarted?.Invoke(sceneName);

        yield return Animate(1f, CoverDuration, CoverEase);
        ScreenCovered?.Invoke(sceneName);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null) // Build Profiles의 Scene List에 없음
        {
            Debug.LogError($"[SceneLoader] '{sceneName}' 씬을 찾을 수 없음. File > Build Profiles > Scene List 확인.");
            yield return Animate(0f, RevealDuration, RevealEase);
            IsTransitioning = false;
            yield break;
        }

        op.allowSceneActivation = false;
        float held = 0f;
        while (op.progress < 0.9f || held < MinHoldTime)
        {
            held += Time.unscaledDeltaTime;
            yield return null;
        }
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // 일시정지 상태에서 나갔을 때 timeScale이 0으로 남는 것 방지
        Time.timeScale = 1f;

        yield return Animate(0f, RevealDuration, RevealEase);
        IsTransitioning = false;
        TransitionFinished?.Invoke(sceneName);
    }

    IEnumerator FadeThroughRoutine(Action atMidpoint)
    {
        IsTransitioning = true;
        TransitionStarted?.Invoke(null);

        yield return Animate(1f, CoverDuration, CoverEase);
        ScreenCovered?.Invoke(null);

        try { atMidpoint?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }

        yield return null; // 새로 생성된 오브젝트들의 Start() 대기
        yield return new WaitForSecondsRealtime(MinHoldTime);

        yield return Animate(0f, RevealDuration, RevealEase);
        IsTransitioning = false;
        TransitionFinished?.Invoke(null);
    }

    IEnumerator Animate(float target, float duration, Ease ease)
    {
        if (_useDissolve)
        {
            _mat.SetFloat(AspectId, (float)Screen.width / Mathf.Max(1, Screen.height));
            _mat.SetFloat(NoiseScaleId, NoiseScale);
            _mat.SetFloat(SoftnessId, EdgeSoftness);
            // 매번 다른 무늬로 녹도록 노이즈 위치를 랜덤 이동
            _mat.SetVector(NoiseOffsetId, new Vector4(UnityEngine.Random.Range(0f, 100f), UnityEngine.Random.Range(0f, 100f), 0f, 0f));
        }

        // 클릭은 덮기 시작부터 걷기가 끝날 때까지 막음
        _overlay.enabled = true;

        _tween?.Kill();
        _tween = DOTween.To(() => Progress, x => Progress = x, target, duration)
            .SetEase(ease)
            .SetUpdate(true)          // timeScale 무시 (일시정지 중에도 동작)
            .SetLink(gameObject);
        yield return _tween.WaitForCompletion();
        _tween = null;
    }
}