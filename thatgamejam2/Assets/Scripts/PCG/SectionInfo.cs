using UnityEngine;
using UnityEngine.Tilemaps;

public enum SectionTier { Start, Easy, Mid, Hard, Goal }

/// <summary>
/// 섹션 프리팹 루트(Grid가 있는 오브젝트)에 붙이는 컴포넌트.
/// 씬 뷰에 섹션 경계·이음새 높이·안전 구역을 그려주고, 규칙 위반이면 테두리가 빨갛게 됨.
///
/// 프리팹 구조:
///   (섹션 이름) (Grid + SectionInfo)
///     ├ Ground   (Tilemap)  ← 충돌 있는 지형
///     └ Markers  (Tilemap)  ← 표시용 타일 (Marker_Tee, Marker_Hole). 게임에선 안 보임
/// 원점(0,0) = 섹션 왼쪽 아래 칸.
/// </summary>
[RequireComponent(typeof(Grid))]
public class SectionInfo : MonoBehaviour
{
    // ── 팀 공통 규칙 (회의에서 확정되면 여기만 고치면 됨) ──
    public const int Height = 16; // 섹션 높이 (칸)
    public const int DefaultWidth = 15; // 기본 섹션 너비 (칸)
    public const int SeamGroundTop = 4;  // 이음새 지면 높이: y = 0..3 은 땅, 4부터 빈칸
    public const int SeamSafeColumns = 2;  // 양 끝에서 이만큼은 평지 고정 (구멍·경사 금지)

    // 마커 타일 이름 (Tile 에셋 이름에 이 글자가 들어가면 인식)
    public const string TeeMarker = "Marker_Tee";
    public const string HoleMarker = "Marker_Hole";

    [Min(4)] public int width = DefaultWidth;
    public SectionTier tier = SectionTier.Easy;

    [Tooltip("비워두면 자식 중 이름이 'Ground'인 Tilemap을 자동으로 찾음")]
    public Tilemap ground;
    [Tooltip("비워두면 자식 중 이름이 'Markers'인 Tilemap을 자동으로 찾음")]
    public Tilemap markers;

    public Tilemap Ground => ground != null ? ground : (ground = FindMap("Ground"));
    public Tilemap Markers => markers != null ? markers : (markers = FindMap("Markers"));

    /// <summary>왼쪽에 다른 섹션이 붙는가 (Start만 아님)</summary>
    public bool HasLeftSeam => tier != SectionTier.Start;
    /// <summary>오른쪽에 다른 섹션이 붙는가 (Goal만 아님)</summary>
    public bool HasRightSeam => tier != SectionTier.Goal;

    Tilemap FindMap(string childName)
    {
        var t = transform.Find(childName);
        return t != null ? t.GetComponent<Tilemap>() : null;
    }

    /// <summary>Markers 타일맵에서 이름에 key가 들어간 타일 칸 개수와 마지막 위치</summary>
    public int CountMarker(string key, out Vector3Int cell)
    {
        cell = default;
        var tm = Markers;
        if (tm == null) return 0;
        int n = 0;
        foreach (var p in tm.cellBounds.allPositionsWithin)
        {
            var tile = tm.GetTile(p);
            if (tile != null && tile.name.Contains(key)) { n++; cell = p; }
        }
        return n;
    }

    /// <summary>규칙 위반 내용을 반환. 문제없으면 null.</summary>
    public string Validate()
    {
        var tm = Ground;
        if (tm == null) return "Ground Tilemap 없음";

        // 1) 이음새 안전 구역: 땅은 SeamGroundTop 아래 꽉 차 있고, 그 위는 비어 있어야 함
        //    Start의 왼쪽 끝, Goal의 오른쪽 끝은 아무것도 안 붙으므로 검사 제외 (벽 세워도 됨)
        for (int i = 0; i < SeamSafeColumns; i++)
        {
            foreach (int x in new[] { i, width - 1 - i })
            {
                if (x == i && !HasLeftSeam) continue;
                if (x == width - 1 - i && !HasRightSeam) continue;
                for (int y = 0; y < Height; y++)
                {
                    bool has = tm.HasTile(new Vector3Int(x, y, 0));
                    if (y < SeamGroundTop && !has) return $"이음새 구역 ({x},{y})에 땅이 비어 있음";
                    if (y >= SeamGroundTop && has) return $"이음새 구역 ({x},{y})에 지면보다 높은 타일";
                }
            }
        }

        // 2) 섹션 범위 밖 타일 금지 (Ground, Markers 둘 다)
        foreach (var map in new[] { tm, Markers })
        {
            if (map == null) continue;
            map.CompressBounds();
            var b = map.cellBounds;
            if (b.size.x == 0) continue;
            if (b.xMin < 0 || b.yMin < 0 || b.xMax > width || b.yMax > Height)
                return $"{map.name}: 섹션 범위({width}x{Height}) 밖에 타일 있음: {b.min} ~ {b.max}";
        }

        // 3) 마커 개수
        int tees = CountMarker(TeeMarker, out var teeCell);
        int holes = CountMarker(HoleMarker, out var holeCell);

        if (tier == SectionTier.Start)
        {
            if (Markers == null) return "Start 섹션에 Markers 타일맵 없음";
            if (tees != 1) return $"Start 섹션엔 Tee 마커가 정확히 1개 있어야 함 (현재 {tees}개)";
            if (tm.HasTile(teeCell)) return $"Tee 마커 {teeCell} 칸에 땅이 있음 (땅 바로 위 빈칸에 칠할 것)";
            if (!tm.HasTile(teeCell + Vector3Int.down)) return $"Tee 마커 {teeCell} 아래에 땅이 없음";
        }
        else if (tees > 0) return "Tee 마커는 Start 섹션에만";

        if (tier == SectionTier.Goal)
        {
            if (Markers == null) return "Goal 섹션에 Markers 타일맵 없음";
            if (holes != 1) return $"Goal 섹션엔 Hole 마커가 정확히 1개 있어야 함 (현재 {holes}개)";
            if (tm.HasTile(holeCell)) return $"Hole 마커 {holeCell} 칸에 땅이 있음 (판 구멍 칸에 칠할 것)";
        }
        else if (holes > 0) return "Hole 마커는 Goal 섹션에만";

        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("규칙 검사")]
    void ValidateMenu()
    {
        var err = Validate();
        if (err == null) Debug.Log($"[Section] {name}: OK", this);
        else Debug.LogWarning($"[Section] {name}: {err}", this);
    }
 
    void OnDrawGizmos()
    {
        var o = transform.position;
        bool ok = Validate() == null;
 
        // 섹션 경계
        Gizmos.color = ok ? new Color(0.3f, 1f, 0.4f, 0.9f) : Color.red;
        Gizmos.DrawWireCube(o + new Vector3(width, Height) * 0.5f, new Vector3(width, Height));
 
        // 이음새 안전 구역
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.12f);
        var safe = new Vector3(SeamSafeColumns, Height);
        if (HasLeftSeam)  Gizmos.DrawCube(o + new Vector3(SeamSafeColumns * 0.5f, Height * 0.5f), safe);
        if (HasRightSeam) Gizmos.DrawCube(o + new Vector3(width - SeamSafeColumns * 0.5f, Height * 0.5f), safe);
 
        // 이음새 지면선
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        if (HasLeftSeam)  Gizmos.DrawLine(o + new Vector3(-0.5f, SeamGroundTop), o + new Vector3(SeamSafeColumns, SeamGroundTop));
        if (HasRightSeam) Gizmos.DrawLine(o + new Vector3(width - SeamSafeColumns, SeamGroundTop), o + new Vector3(width + 0.5f, SeamGroundTop));
    }
#endif
}