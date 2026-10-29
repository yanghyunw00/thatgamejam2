using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// 경사 스프라이트의 충돌 모양(Custom Physics Shape)을 파일 이름 보고 자동으로 매끈한 직선으로 설정.
/// 사용: Project 창에서 경사 스프라이트들 선택 → 메뉴 Tools > PCG > Apply Slope Physics Shapes
/// 진짜 아트로 교체한 뒤에도 같은 이름 규칙이면 이 메뉴 한 번이면 끝.
///
/// 이름 규칙 (이름 어딘가에 포함되면 됨):
///   Slope45_Up, Slope45_Down,
///   Slope12_Up_Low, Slope12_Up_High, Slope12_Down_High, Slope12_Down_Low
/// 그 외 타일(Ground 등)은 사각형으로 설정됨.
/// </summary>
public static class SlopePhysicsShapes
{
    // 0~1 정규화 좌표, (0,0) = 왼쪽 아래
    static readonly (string key, Vector2[] pts)[] Shapes =
    {
        ("Slope45_Up",        new[] { V(0,0), V(1,0), V(1,1) }),
        ("Slope45_Down",      new[] { V(0,0), V(1,0), V(0,1) }),
        ("Slope12_Up_Low",    new[] { V(0,0), V(1,0), V(1,.5f) }),
        ("Slope12_Up_High",   new[] { V(0,0), V(1,0), V(1,1), V(0,.5f) }),
        ("Slope12_Down_High", new[] { V(0,0), V(1,0), V(1,.5f), V(0,1) }),
        ("Slope12_Down_Low",  new[] { V(0,0), V(1,0), V(0,.5f) }),
    };
    static readonly Vector2[] Square = { V(0, 0), V(1, 0), V(1, 1), V(0, 1) };
    static Vector2 V(float x, float y) => new Vector2(x, y);

    [MenuItem("Tools/PCG/Apply Slope Physics Shapes")]
    static void Apply()
    {
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        int count = 0;

        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            Vector2[] norm = Square;
            foreach (var (key, pts) in Shapes)
                if (file.Contains(key)) { norm = pts; break; }

            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();
            var physics = dp.GetDataProvider<ISpritePhysicsOutlineDataProvider>();

            foreach (var rect in dp.GetSpriteRects())
            {
                // 아웃라인 좌표는 스프라이트 rect 중심 기준 픽셀 단위
                var size = rect.rect.size;
                var outline = new Vector2[norm.Length];
                for (int i = 0; i < norm.Length; i++)
                    outline[i] = new Vector2((norm[i].x - 0.5f) * size.x, (norm[i].y - 0.5f) * size.y);

                physics.SetOutlines(rect.spriteID, new List<Vector2[]> { outline });
            }

            dp.Apply();
            importer.SaveAndReimport();
            count++;
        }

        Debug.Log($"[PCG] Physics Shape 적용: {count}개");
    }

    [MenuItem("Tools/PCG/Apply Slope Physics Shapes", true)]
    static bool CanApply() => Selection.objects.Length > 0;
}