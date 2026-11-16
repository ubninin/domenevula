using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택한 오브젝트(여러 개 가능)에 말풍선 프리팹(Canvas_SpeechBalloon)을 자식으로 붙여준다.
/// 이미 말풍선 자식이 있으면 건너뛴다.
/// 메뉴 위치: GameObject > 도메네뷸라 > 선택한 오브젝트에 말풍선 붙이기
/// </summary>
public static class AttachSpeechBalloon
{
    private const string PrefabPath = "Assets/Prefabs/Canvas_SpeechBalloon.prefab";

    [MenuItem("GameObject/도메네뷸라/선택한 오브젝트에 말풍선 붙이기", false, 11)]
    private static void Attach()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[말풍선 붙이기] " + PrefabPath + " 를 찾지 못했습니다.");
            return;
        }

        int added = 0, skipped = 0;
        foreach (GameObject target in Selection.gameObjects)
        {
            if (target.GetComponentInChildren<SpeechBalloonController>(true) != null) { skipped++; continue; }

            GameObject balloon = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target.transform);
            balloon.name = "Canvas_SpeechBalloon";
            balloon.SetActive(false);
            Undo.RegisterCreatedObjectUndo(balloon, "말풍선 붙이기");
            added++;
        }
        Debug.Log($"[말풍선 붙이기] {added}개 붙임, {skipped}개는 이미 있어서 건너뜀");
    }

    [MenuItem("GameObject/도메네뷸라/선택한 오브젝트에 말풍선 붙이기", true)]
    private static bool Validate() => Selection.gameObjects.Length > 0;
}
