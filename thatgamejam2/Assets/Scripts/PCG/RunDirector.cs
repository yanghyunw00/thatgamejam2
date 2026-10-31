using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 판(런)의 흐름 담당.
///   시작 → 1번 리전 생성 → 공을 Tee에
///   홀인 → 디졸브로 덮기 → 다음 리전 생성 + 공 Tee로 → 디졸브 걷기
/// 리전 하나 = 레벨 하나. Levels 목록 순서대로 진행, 끝나면 마지막 레벨 반복(loopLastLevel) 또는 런 종료.
///
/// 다른 팀원이 쓸 신호:
///   RegionStarted(리전번호, 레벨)  → 타이머 리셋, 배경 교체(레벨 단위) 등
///   RegionCleared(리전번호)        → 점수 기록
///   RunCompleted                   → 스코어보드
/// </summary>
public class RunDirector : MonoBehaviour
{
    [SerializeField] RegionGenerator generator;
    [Tooltip("씬에 있는 공 (Rigidbody2D가 붙은 오브젝트)")]
    [SerializeField] Rigidbody2D ball;
    [SerializeField] List<LevelSet> levels = new();

    [Tooltip("마지막 레벨까지 깨면 마지막 레벨을 계속 반복 (끄면 RunCompleted)")]
    [SerializeField] bool loopLastLevel = true;
    [Tooltip("0이면 매번 랜덤. 숫자를 넣으면 같은 맵이 다시 나옴")]
    [SerializeField] int seed = 0;
    [SerializeField] bool showSeed = true;
    [Tooltip("낙사 높이 아래로 떨어지면 Tee로 되돌림. 동윤 사망 처리 붙으면 끄기")]
    [SerializeField] bool respawnBelowKillY = true;

    public event Action<int, LevelSet> RegionStarted;
    public event Action<int> RegionCleared;
    public event Action RunCompleted;

    public int RunSeed { get; private set; }
    public int RegionIndex { get; private set; }
    public RegionInfo Current => generator != null ? generator.Current : null;

    bool _advancing;

    void Start()
    {
        if (generator == null || ball == null)
        {
            Debug.LogError("[Run] RunDirector에 Generator 또는 Ball이 연결 안 됨");
            enabled = false;
            return;
        }
        RunSeed = seed != 0 ? seed : UnityEngine.Random.Range(1, int.MaxValue);
        BuildRegion(0);
    }

    void Update()
    {
        var cur = Current;
        if (respawnBelowKillY && cur != null && ball.position.y < cur.KillY)
            RespawnBall();
    }

    /// <summary>공을 현재 리전의 Tee로 되돌림 (낙사 처리 등에서 호출)</summary>
    public void RespawnBall()
    {
        var cur = Current;
        if (cur == null) return;
        ball.linearVelocity = Vector2.zero;
        ball.angularVelocity = 0f;
        ball.position = cur.TeePosition;
        ball.transform.position = cur.TeePosition;
        ball.WakeUp();
    }

    [ContextMenu("다음 리전으로 (테스트)")]
    public void SkipRegion() => OnHoled();

    // ───────────── 내부 ─────────────

    LevelSet LevelFor(int i)
    {
        if (levels.Count == 0) return null;
        if (i < levels.Count) return levels[i];
        return loopLastLevel ? levels[^1] : null;
    }

    int RegionSeed(int i) => unchecked(RunSeed * 486187739 + i * 16777619);

    void BuildRegion(int i)
    {
        var level = LevelFor(i);
        if (level == null) { RunCompleted?.Invoke(); return; }

        RegionIndex = i;
        var info = generator.Build(level, i, RegionSeed(i), ball);
        if (info == null) return;

        if (info.Hole != null) info.Hole.Holed += OnHoled;
        RespawnBall();
        RegionStarted?.Invoke(i, level);
    }

    void OnHoled()
    {
        if (_advancing) return;
        _advancing = true;
        RegionCleared?.Invoke(RegionIndex);
        StartCoroutine(Advance());
    }

    IEnumerator Advance()
    {
        var loader = SceneLoader.Instance;
        while (loader.IsTransitioning) yield return null;

        int next = RegionIndex + 1;
        if (LevelFor(next) == null)
        {
            RunCompleted?.Invoke();
            _advancing = false;
            yield break;
        }

        loader.FadeThrough(() => BuildRegion(next));
        while (loader.IsTransitioning) yield return null;
        _advancing = false;
    }

    void OnGUI()
    {
        if (!showSeed || Current == null) return;
        GUI.Label(new Rect(8, 8, 600, 22),
            $"Seed {RunSeed}  ·  Region {RegionIndex + 1} ({Current.Level.name})");
    }
}