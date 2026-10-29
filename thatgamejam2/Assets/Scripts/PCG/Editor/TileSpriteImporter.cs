using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Art 아래로 처음 들어오는 이미지에 픽셀아트 기본 설정을 한 번 적용.
/// (Sprite, Point 필터, 압축 없음, PPU, 피벗 중앙)
///
/// - "처음 임포트될 때만" 적용 → 이후 인스펙터에서 바꾼 값(예: 애니메이션 시트를 Multiple로 자르기)은 유지됨
/// - 이미 들어와 있는 파일에 다시 적용하고 싶으면: 파일 선택 → 메뉴 Tools > PCG > Apply Pixel Art Settings
/// </summary>
public class TileSpriteImporter : AssetPostprocessor
{
    const string Folder = "Assets/Art";
    public const int PixelsPerUnit = 32; // ← 땅 타일 한 장의 픽셀 수와 같게 (32x32 타일이면 32)

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var ti = (TextureImporter)assetImporter;
        if (!ti.importSettingsMissing) return; // 처음 들어올 때만
        Apply(ti, assetPath.Contains("/Animated/"));
    }

    static void Apply(TextureImporter ti, bool isSheet)
    {
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = isSheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PixelsPerUnit;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteAlignment = (int)SpriteAlignment.Center;
        s.spriteGenerateFallbackPhysicsShape = true;
        ti.SetTextureSettings(s);
    }

    [MenuItem("Tools/PCG/Apply Pixel Art Settings")]
    static void ApplyToSelection()
    {
        int n = 0;
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;
            Apply(ti, path.Contains("/Animated/"));
            ti.SaveAndReimport();
            n++;
        }
        Debug.Log($"[PCG] 픽셀아트 설정 적용: {n}개 (PPU {PixelsPerUnit})");
    }
}