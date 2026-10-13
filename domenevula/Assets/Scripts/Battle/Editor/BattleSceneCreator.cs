using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// 메뉴 "도메네뷸라 > 전투 프로토타입 씬 만들기" 한 번으로 Assets/Scenes/BattlePrototype.unity 를 만든다.
/// 씬에는 카메라, EventSystem, BattleManager(폰트 연결됨)만 들어간다. 나머지 UI/아레나는 실행할 때 코드가 만든다.
/// </summary>
public static class BattleSceneCreator
{
    private const string ScenePath = "Assets/Scenes/BattlePrototype.unity";
    private const string FontPath = "Assets/Fonts/Cafe24Dongdong SDF.asset";

    [MenuItem("도메네뷸라/전투 프로토타입 씬 만들기")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            bool overwrite = EditorUtility.DisplayDialog("전투 프로토타입 씬",
                "Assets/Scenes/BattlePrototype.unity 가 이미 있습니다. 덮어쓸까요?", "덮어쓰기", "취소");
            if (!overwrite) return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.08f);

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<InputSystemUIInputModule>();

        var managerGo = new GameObject("BattleManager");
        var manager = managerGo.AddComponent<BattleManager>();
        manager.fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (manager.fontAsset == null)
            Debug.LogWarning($"[Battle] 폰트 에셋을 찾지 못했습니다 ({FontPath}). BattleManager의 Font Asset에 한글 TMP 폰트를 직접 넣어주세요.");

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Selection.activeGameObject = managerGo;
        Debug.Log("[Battle] 전투 프로토타입 씬을 만들었습니다: " + ScenePath + "  → 재생(▶) 해서 확인하세요.");
    }
}
