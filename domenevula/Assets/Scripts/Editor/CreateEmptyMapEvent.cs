using UnityEditor;
using UnityEngine;

/// <summary>
/// 빈 MapEvent 오브젝트를 한 번에 만들어주는 메뉴.
/// NPC_Example에 쓰던 세팅(스프라이트, Box Collider 2D Is Trigger, Rigidbody2D Kinematic,
/// Sorting Layer "Characters")을 기본값으로 깔아두고, 애니메이터와 커맨드는 비워둔다.
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

        go.AddComponent<MapEvent>();

        Undo.RegisterCreatedObjectUndo(go, "빈 이벤트 오브젝트 생성");
        Selection.activeGameObject = go;
    }

    private static Sprite cachedPlaceholderSprite;

    /// <summary>
    /// 유니티 버전/렌더 파이프라인에 따라 내장 "Sprites/Square.png"를 못 찾는 경우가 있어서
    /// (URP 프로젝트 등), 1x1 흰 텍스처로 직접 정사각형 스프라이트를 만들어 쓴다.
    /// </summary>
    private static Sprite GetPlaceholderSquareSprite()
    {
        if (cachedPlaceholderSprite != null) return cachedPlaceholderSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.hideFlags = HideFlags.DontSave;

        cachedPlaceholderSprite = Sprite.Create(
            tex,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f),
            1f // Pixels Per Unit = 1 → localScale로 크기를 조절해 쓰면 됨
        );
        cachedPlaceholderSprite.hideFlags = HideFlags.DontSave;
        return cachedPlaceholderSprite;
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
