using UnityEditor;
using UnityEngine;

/// <summary>
/// 메뉴 GameObject > 도메네뷸라 > 씬 이동 오브젝트(Move): "Move" 오브젝트(Trigger 박스 콜라이더 + SceneMover)를 만든다.
/// 또한 Play 를 누를 때 Assets/Scenes 의 씬이 Build Settings 에 없으면 자동으로 추가한다 (씬 이동에 필요).
/// </summary>
[InitializeOnLoad]
public static class CreateSceneMover
{
    static CreateSceneMover()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) EnsureScenesInBuildSettings();
        };
    }

    [MenuItem("GameObject/도메네뷸라/씬 이동 오브젝트 (Move)", false, 12)]
    private static void Create(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("Move");
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

        SceneView sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            Vector3 p = sv.pivot;
            p.z = 0f;
            go.transform.position = p;
        }

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 1f);
        go.AddComponent<SceneMover>();

        Undo.RegisterCreatedObjectUndo(go, "씬 이동 오브젝트 생성");
        Selection.activeGameObject = go;
    }

    private static void EnsureScenesInBuildSettings()
    {
        MapSystemSetup.EnsureCoreFirstInBuildSettings();
    }
}
