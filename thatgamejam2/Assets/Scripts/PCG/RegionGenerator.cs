using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>생성된 리전 정보. 다른 팀원 코드는 여기서 값만 읽어가면 됨.</summary>
public class RegionInfo
{
    public int Index;
    public int Seed;
    public LevelSet Level;
    public Vector2 TeePosition;     // 공 시작 위치 (Tee 셀 중앙)
    public Vector2 HolePosition;    // 컵 위치 (Hole 셀 중앙)
    public float KillY;             // 공이 이보다 아래로 떨어지면 낙사
    public Rect Bounds;             // 리전 전체 영역 (카메라 제한 등에 사용)
    public HoleTrigger Hole;
    public List<string> SectionNames = new();
}

/// <summary>
/// 리전 생성기. LevelSet을 받아 Start → 섹션 N개 → Goal 을 오른쪽으로 이어 붙인다.
/// - 섹션들의 Ground/Deco 셀을 하나의 타일맵으로 복사 (이음새 없는 콜라이더 하나로 합침)
/// - Markers 레이어는 복사하지 않음 (게임 화면에 안 보임). Tee/Hole 위치만 읽어서 사용
/// - 흐름(언제 만들지, 다음 리전 등)은 RunDirector가 담당
/// </summary>
public class RegionGenerator : MonoBehaviour
{
    [Header("컵 판정")]
    [Tooltip("이 속도 이하로 컵 안에 있어야 홀인. 아주 크게(999) 하면 닿자마자 홀인")]
    [SerializeField] float holeMaxSpeed = 1.5f;
    [SerializeField] float holeSettleTime = 0.15f;
    [Tooltip("컵 위치에 세울 보이는 깃발 (선택)")]
    [SerializeField] GameObject holeFlagPrefab;

    [Header("맵 경계")]
    [Tooltip("리전 양 끝에 보이지 않는 높은 벽")]
    [SerializeField] bool addSideWalls = true;
    [Tooltip("리전 바닥에서 이만큼 아래가 낙사 높이")]
    [SerializeField] float killMargin = 3f;

    public RegionInfo Current { get; private set; }

    Transform _root;

    public RegionInfo Build(LevelSet level, int index, int seed, Rigidbody2D ball)
    {
        Clear();

        var rng = new System.Random(seed);
        var info = new RegionInfo { Index = index, Seed = seed, Level = level };

        // ── 1. 섹션 고르기 ──
        var start = PickOne(level.starts, rng);
        var goal = PickOne(level.goals, rng);
        if (start == null || goal == null)
        {
            Debug.LogError($"[Region] '{level.name}' 레벨에 Start 또는 Goal 섹션이 없음");
            return null;
        }
        int min = Mathf.Max(0, level.minSections);
        int max = Mathf.Max(min, level.maxSections);
        int count = rng.Next(min, max + 1);
        var middle = PickUnique(level.sections, count, rng);

        var order = new List<SectionInfo> { start };
        order.AddRange(middle);
        order.Add(goal);

        // ── 2. 리전 오브젝트 만들기 ──
        _root = new GameObject($"Region_{index + 1}").transform;
        _root.SetParent(transform, false);
        _root.gameObject.AddComponent<Grid>();
        var ground = CreateMap("Ground");
        var deco = CreateMap("Deco");

        // ── 3. 섹션 셀 복사 ──
        // 섹션은 비활성 상태로 잠깐 꺼내서 읽고 지움 (섹션 자체 콜라이더가 켜지지 않게)
        var holder = new GameObject("_temp");
        holder.SetActive(false);

        int x = 0;
        bool teeFound = false, holeFound = false, groundStyled = false, decoStyled = false;
        Vector3Int teeCell = default, holeCell = default;

        foreach (var prefab in order)
        {
            var inst = Instantiate(prefab, holder.transform);
            var offset = new Vector3Int(x, 0, 0);

            if (inst.Ground != null)
            {
                CopyTiles(inst.Ground, ground, offset);
                if (!groundStyled) { CopyStyle(inst.Ground, ground); groundStyled = true; }
            }

            var srcDeco = FindMap(inst.transform, "Deco");
            if (srcDeco != null)
            {
                CopyTiles(srcDeco, deco, offset);
                if (!decoStyled) { CopyStyle(srcDeco, deco); decoStyled = true; }
            }

            if (!teeFound && inst.CountMarker(SectionInfo.TeeMarker, out var t) > 0)
            { teeCell = t + offset; teeFound = true; }
            if (!holeFound && inst.CountMarker(SectionInfo.HoleMarker, out var h) > 0)
            { holeCell = h + offset; holeFound = true; }

            info.SectionNames.Add(prefab.name);
            x += inst.width;
        }
        Destroy(holder);

        // ── 4. 땅 콜라이더 (하나로 합침) ──
        var rb = ground.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        var tc = ground.gameObject.AddComponent<TilemapCollider2D>();
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
        var cc = ground.gameObject.AddComponent<CompositeCollider2D>();
        cc.geometryType = CompositeCollider2D.GeometryType.Outlines;
        tc.ProcessTilemapChanges();
        cc.GenerateGeometry();

        // ── 5. 위치 정보 ──
        if (!teeFound)
        {
            Debug.LogWarning("[Region] Start 섹션에서 Tee 마커를 못 찾음 → 임시 위치 사용");
            teeCell = new Vector3Int(SectionInfo.SeamSafeColumns, SectionInfo.SeamGroundTop, 0);
        }
        info.TeePosition = ground.GetCellCenterWorld(teeCell);

        Vector2 origin = _root.position;
        info.Bounds = new Rect(origin.x, origin.y, x, SectionInfo.Height);
        info.KillY = origin.y - killMargin;

        // ── 6. 컵 ──
        if (holeFound)
        {
            info.HolePosition = ground.GetCellCenterWorld(holeCell);

            var holeGo = new GameObject("Hole");
            holeGo.transform.SetParent(_root, false);
            holeGo.transform.position = info.HolePosition;
            var box = holeGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.8f, 0.8f);
            var hole = holeGo.AddComponent<HoleTrigger>();
            hole.target = ball;
            hole.maxSpeed = holeMaxSpeed;
            hole.settleTime = holeSettleTime;
            info.Hole = hole;

            if (holeFlagPrefab != null)
                Instantiate(holeFlagPrefab, info.HolePosition, Quaternion.identity, _root);
        }
        else Debug.LogWarning("[Region] Goal 섹션에서 Hole 마커를 못 찾음 → 클리어 불가");

        // ── 7. 양 끝 보이지 않는 벽 ──
        if (addSideWalls)
        {
            var walls = new GameObject("SideWalls");
            walls.transform.SetParent(_root, false);
            float h = SectionInfo.Height * 3f;
            AddWall(walls, new Vector2(-0.5f, SectionInfo.Height * 0.5f), h);
            AddWall(walls, new Vector2(x + 0.5f, SectionInfo.Height * 0.5f), h);
        }

        Current = info;
        Debug.Log($"[Region] {index + 1}번 리전 ({level.name}) seed {seed}: {string.Join(" → ", info.SectionNames)}");
        return info;
    }

    public void Clear()
    {
        if (_root != null) Destroy(_root.gameObject);
        _root = null;
        Current = null;
    }

    // ───────────── helpers ─────────────

    Tilemap CreateMap(string mapName)
    {
        var go = new GameObject(mapName, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(_root, false);
        return go.GetComponent<Tilemap>();
    }

    static Tilemap FindMap(Transform parent, string childName)
    {
        var t = parent.Find(childName);
        return t != null ? t.GetComponent<Tilemap>() : null;
    }

    static void CopyTiles(Tilemap src, Tilemap dst, Vector3Int offset)
    {
        foreach (var p in src.cellBounds.allPositionsWithin)
        {
            var tile = src.GetTile(p);
            if (tile == null) continue;

            var q = p + offset;
            dst.SetTile(q, tile);
            dst.SetTileFlags(q, TileFlags.None);
            dst.SetTransformMatrix(q, src.GetTransformMatrix(p));
            dst.SetColor(q, src.GetColor(p) * src.color); // 타일맵 전체 색(Color)도 반영
        }
    }

    static void CopyStyle(Tilemap src, Tilemap dst)
    {
        dst.gameObject.layer = src.gameObject.layer;
        var s = src.GetComponent<TilemapRenderer>();
        var d = dst.GetComponent<TilemapRenderer>();
        if (s == null || d == null) return;
        d.sortingLayerID = s.sortingLayerID;
        d.sortingOrder = s.sortingOrder;
        d.sharedMaterial = s.sharedMaterial;
    }

    static void AddWall(GameObject parent, Vector2 center, float height)
    {
        var box = parent.AddComponent<BoxCollider2D>();
        box.offset = center;
        box.size = new Vector2(1f, height);
    }

    static SectionInfo PickOne(List<SectionInfo> list, System.Random rng)
    {
        var valid = list.FindAll(s => s != null);
        return valid.Count == 0 ? null : valid[rng.Next(valid.Count)];
    }

    /// <summary>중복 없이 count개. 후보가 모자라면 다시 섞어서 이어 뽑음 (같은 게 연속으로는 안 나오게)</summary>
    static List<SectionInfo> PickUnique(List<SectionInfo> list, int count, System.Random rng)
    {
        var result = new List<SectionInfo>();
        var valid = list.FindAll(s => s != null);
        if (valid.Count == 0 || count <= 0) return result;

        var bag = new List<SectionInfo>();
        while (result.Count < count)
        {
            if (bag.Count == 0)
            {
                bag.AddRange(valid);
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (bag[i], bag[j]) = (bag[j], bag[i]);
                }
                if (result.Count > 0 && bag.Count > 1 && bag[0] == result[^1])
                    (bag[0], bag[^1]) = (bag[^1], bag[0]);
            }
            result.Add(bag[0]);
            bag.RemoveAt(0);
        }
        return result;
    }
}