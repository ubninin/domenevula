using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 빈 MapEvent 오브젝트를 한 번에 만들어주는 메뉴.
/// NPC_Example에 쓰던 세팅(스프라이트, Box Collider 2D Is Trigger, Rigidbody2D Kinematic,
/// Sorting Layer "Characters", DepthScale)을 기본값으로 깔아두고, 애니메이터와 커맨드는 비워둔다.
/// 이후 Sprite만 원하는 캐릭터 그림으로 바꾸고, MapEvent의 "+ 커맨드 추가"로 내용을 채우면 됨.
///
/// 붙이는 곳: Assets/Scripts/Editor/
/// 메뉴 위치: GameObject > 도메네뷸라 > 빈 이벤트 오브젝트
///          (Hierarchy 창에서 우클릭해도 같은 메뉴가 뜸)
/// </summary>
public static class CreateEmptyMapEvent
{
    [MenuItem("GameObject/도메네뷸라/빈 이벤트 오브젝트", false, 10)]
    private static void CreateEmptyEvent(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("NewMapEvent");

        // 부모가 선택되어 있으면 그 밑에 생성 (Unity 기본 동작과 맞춤)
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

        // Scene 뷰 중앙 근처에 배치
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            Vector3 pivot = sceneView.pivot;
            pivot.z = 0f;
            go.transform.position = pivot;
        }

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetPlaceholderSquareSprite();
        sr.sortingLayerName = SortingLayerExists("Characters") ? "Characters" : sr.sortingLayerName;
        sr.sortingOrder = 1;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 1f);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;

        go.AddComponent<DepthScale>();
        go.AddComponent<CharacterSpriteAnimator>();
        go.AddComponent<MapEvent>(); // MapEvent.Reset()이 같은 오브젝트의 CharacterSpriteAnimator를 자동으로 찾아 연결해준다

        Undo.RegisterCreatedObjectUndo(go, "빈 이벤트 오브젝트 생성");
        Selection.activeGameObject = go;
    }

    private const string PlaceholderPath = "Assets/Sprite/_placeholder_square.png";

    /// <summary>
    /// 유니티 버전/렌더 파이프라인에 따라 내장 "Sprites/Square.png"를 못 찾는 경우가 있어서
    /// (URP 프로젝트 등), 1x1 흰 텍스처로 직접 정사각형 스프라이트를 만들어 쓴다.
    ///
    /// 메모리상 임시 객체(HideFlags.DontSave)로 만들면, 그 스프라이트가 물린 SpriteRenderer를
    /// 씬에 저장한 뒤 인스펙터가 다시 그릴 때 "영속 안 되는 객체를 영속 취급" 에러
    /// (Assertion failed ... kDontSaveInEditor ... InspectorWindow:RedrawFromNative)가 나서,
    /// 대신 실제 에셋 파일로 한 번만 저장해 영구적으로 쓴다.
    /// </summary>
    private static Sprite GetPlaceholderSquareSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderPath);
        if (existing != null) return existing;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string dir = Path.GetDirectoryName(PlaceholderPath);
        if (!AssetDatabase.IsValidFolder(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(PlaceholderPath, png);
        AssetDatabase.ImportAsset(PlaceholderPath);

        TextureImporter importer = AssetImporter.GetAtPath(PlaceholderPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1f; // localScale로 크기를 조절해 쓰면 됨
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderPath);
    }

    private static bool SortingLayerExists(string name)
    {
        foreach (var sl in UnityEngine.SortingLayer.layers)
        {
            if (sl.name == name) return true;
        }
        return false;
    }
}
