using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Core 씬 + 맵 씬 구조로 옮기기 위한 에디터 도구 모음.
///
///  1) 도메네뷸라 > 맵 시스템 > 현재 씬을 Core + 맵으로 분리
///     지금 열려 있는 씬(예: SampleScene)에서 플레이어/파티/카메라/UI/대사 시스템 등 "항상 있어야 하는 것"을
///     새 Core 씬(Assets/Scenes/Core.unity)으로 옮기고, 나머지(배경, 가구, NPC, 이벤트)는 이 씬(맵)에 남긴다.
///  2) GameObject > 도메네뷸라 > 도착 지점 (Spawn Point) / 맵 정보 (MapInfo)
/// </summary>
public static class MapSystemSetup
{
    private const string CorePath = "Assets/Scenes/Core.unity";

    // 이 이름의 최상위 오브젝트는 Core 로 옮긴다. (맵마다 달라지는 것들은 제외)
    private static readonly string[] CoreRootNames =
    {
        "MainCamera", "Main Camera", "GlobalLight2D", "Global Light 2D", "EventSystem",
        "Canvas_Screen", "DialogueManager", "Players", "PartyManager", "QuestManager", "ShootModeController",
    };

    [MenuItem("도메네뷸라/맵 시스템/현재 씬을 Core + 맵으로 분리")]
    private static void SplitCurrentScene()
    {
        Scene map = SceneManager.GetActiveScene();

        if (string.IsNullOrEmpty(map.path))
        {
            EditorUtility.DisplayDialog("맵 분리", "먼저 이 씬을 파일로 저장해 주세요.", "확인");
            return;
        }
        if (map.name == MapManager.CoreSceneName)
        {
            EditorUtility.DisplayDialog("맵 분리", "지금 활성 씬이 Core 입니다. 분리할 맵 씬(예: SampleScene)을 열어 주세요.", "확인");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CorePath) != null)
        {
            EditorUtility.DisplayDialog("맵 분리", "이미 " + CorePath + " 가 있습니다. 새 맵은 '맵 정보'와 '도착 지점'만 추가하면 됩니다.", "확인");
            return;
        }
        if (!EditorUtility.DisplayDialog("맵 분리",
                "'" + map.name + "' 에서 플레이어/파티/카메라/UI/대사 시스템 등을 새 Core 씬으로 옮깁니다.\n" +
                "(Git 커밋이나 백업을 먼저 해두는 걸 권장합니다.)\n계속할까요?", "분리", "취소"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var roots = new List<GameObject>();
        foreach (GameObject go in map.GetRootGameObjects())
            if (Array.IndexOf(CoreRootNames, go.name) >= 0) roots.Add(go);

        if (roots.Count == 0)
        {
            EditorUtility.DisplayDialog("맵 분리", "Core 로 옮길 오브젝트(MainCamera, Players, Canvas_Screen 등)를 찾지 못했습니다.", "확인");
            return;
        }

        // 카메라가 참조하던 배경(맵 쪽 오브젝트)은 씬을 건너 참조할 수 없으므로, MapInfo 로 넘기고 카메라 쪽 참조는 비운다.
        SpriteRenderer background = null;
        foreach (GameObject root in roots)
        {
            CameraFollow cf = root.GetComponentInChildren<CameraFollow>(true);
            if (cf == null) continue;
            var so = new SerializedObject(cf);
            SerializedProperty p = so.FindProperty("backgroundBounds");
            if (p != null)
            {
                background = p.objectReferenceValue as SpriteRenderer;
                p.objectReferenceValue = null;
                so.ApplyModifiedProperties();
            }
        }
        if (background == null)
        {
            GameObject bgObj = GameObject.Find("Background");
            if (bgObj != null) background = bgObj.GetComponentInChildren<SpriteRenderer>();
        }

        Vector3 playerPos = Vector3.zero;
        PlayerMovement2D mover = UnityEngine.Object.FindFirstObjectByType<PlayerMovement2D>();
        if (mover != null) playerPos = mover.transform.position;

        // Core 씬 만들기
        Scene core = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        foreach (GameObject root in roots) SceneManager.MoveGameObjectToScene(root, core);

        var mmObj = new GameObject("MapManager");
        SceneManager.MoveGameObjectToScene(mmObj, core);
        var mm = mmObj.AddComponent<MapManager>();
        var mmSo = new SerializedObject(mm);
        mmSo.FindProperty("startMapName").stringValue = map.name;
        SerializedProperty startMap = mmSo.FindProperty("startMap");
        if (startMap != null) startMap.objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(map.path);
        mmSo.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(core, CorePath);

        // 맵 씬 쪽: MapInfo + 기본 도착 지점
        SceneManager.SetActiveScene(map);
        var infoObj = new GameObject("MapInfo");
        var info = infoObj.AddComponent<MapInfo>();
        info.background = background;
        info.displayName = map.name;

        var spawnObj = new GameObject("SpawnDefault");
        spawnObj.transform.position = playerPos;
        var sp = spawnObj.AddComponent<MapSpawnPoint>();
        sp.id = "Default";
        sp.isDefault = true;

        EditorSceneManager.MarkSceneDirty(map);
        EditorSceneManager.SaveScene(map);

        EnsureCoreFirstInBuildSettings();

        EditorUtility.DisplayDialog("맵 분리 완료",
            "Core 씬을 만들었습니다 (" + CorePath + ").\n\n" +
            "• 맵 씬 '" + map.name + "' 에는 MapInfo, SpawnDefault 가 추가됐습니다.\n" +
            "• 이제 맵 씬만 열고 Play 해도 Core 가 자동으로 같이 로드됩니다.\n" +
            "• Console 에 'cross scene reference' 경고가 있으면 알려 주세요.", "확인");
    }

    [MenuItem("GameObject/도메네뷸라/도착 지점 (Spawn Point)", false, 13)]
    private static void CreateSpawnPoint(MenuCommand cmd)
    {
        var go = new GameObject("SpawnPoint");
        GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
        PlaceInSceneView(go);
        var sp = go.AddComponent<MapSpawnPoint>();
        sp.id = "From";
        Undo.RegisterCreatedObjectUndo(go, "도착 지점 생성");
        Selection.activeGameObject = go;
    }

    [MenuItem("GameObject/도메네뷸라/맵 정보 (MapInfo)", false, 14)]
    private static void CreateMapInfo(MenuCommand cmd)
    {
        var go = new GameObject("MapInfo");
        GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
        var info = go.AddComponent<MapInfo>();
        GameObject bg = GameObject.Find("Background");
        if (bg != null) info.background = bg.GetComponentInChildren<SpriteRenderer>();
        Undo.RegisterCreatedObjectUndo(go, "맵 정보 생성");
        Selection.activeGameObject = go;
    }

    private static void PlaceInSceneView(GameObject go)
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null) return;
        Vector3 p = sv.pivot;
        p.z = 0f;
        go.transform.position = p;
    }

    /// <summary>Build Settings 의 Scene List 맨 앞을 Core 로 맞추고, Assets/Scenes 의 다른 씬도 등록한다.</summary>
    public static void EnsureCoreFirstInBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!list.Exists(s => s.path == path)) list.Add(new EditorBuildSettingsScene(path, true));
        }

        int coreIndex = list.FindIndex(s => s.path == CorePath);
        if (coreIndex > 0)
        {
            EditorBuildSettingsScene core = list[coreIndex];
            list.RemoveAt(coreIndex);
            list.Insert(0, core);
        }
        EditorBuildSettings.scenes = list.ToArray();
    }
}
