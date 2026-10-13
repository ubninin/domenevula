using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Project 창에서 스프라이트시트(Sprite Mode = Multiple) 텍스처 여러 개를 선택한 다음
/// 메뉴에서 실행하면, 그 안의 모든 조각(sub-sprite)의 Pivot을 한 번에 Bottom으로 바꿔준다.
/// Sprite Editor를 파일마다 일일이 열어서 Apply할 필요 없이 한 번에 처리하기 위한 도구.
///
/// 쓰는 법: Assets/Sprite/Characters 폴더에서 Hayul_Idle, Hayul_Walk 등 원하는 텍스처들을
/// 전부 선택 (Ctrl+클릭 또는 Shift+클릭) → 상단 메뉴 도메네뷸라 → Sprite Pivot을 Bottom으로 일괄 변경
/// </summary>
public static class BatchSetSpritePivot
{
    [MenuItem("도메네뷸라/Sprite Pivot을 Bottom으로 일괄 변경")]
    private static void SetPivotBottomForSelection()
    {
        SetPivotForSelection(new Vector2(0.5f, 0f), SpriteAlignment.Custom);
    }

    private static void SetPivotForSelection(Vector2 pivot, SpriteAlignment alignment)
    {
        Object[] textures = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
        if (textures.Length == 0)
        {
            Debug.LogWarning("도메네뷸라: Project 창에서 텍스처(스프라이트시트)를 먼저 선택하세요.");
            return;
        }

        var factory = new SpriteDataProviderFactories();
        factory.Init();

        int changedFiles = 0;
        int changedSprites = 0;

        foreach (Texture2D texture in textures)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                Debug.LogWarning($"도메네뷸라: {path}는 Sprite Mode가 Multiple이 아니라서 건너뜁니다.");
                continue;
            }

            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            SpriteRect[] spriteRects = dataProvider.GetSpriteRects();
            for (int i = 0; i < spriteRects.Length; i++)
            {
                spriteRects[i].alignment = SpriteAlignment.Custom;
                spriteRects[i].pivot = pivot;
                changedSprites++;
            }

            dataProvider.SetSpriteRects(spriteRects);
            dataProvider.Apply();

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            changedFiles++;
        }

        Debug.Log($"도메네뷸라: 텍스처 {changedFiles}개, 스프라이트 {changedSprites}개의 Pivot을 Bottom으로 변경했습니다.");
    }
}
