using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 맵 배경 이미지 이름을 규칙(map번호_이름)에 맞게 한 번에 바꾸는 일회용 도구.
/// AssetDatabase.RenameAsset 을 쓰므로 .meta(GUID)가 유지되어 씬/프리팹의 연결이 끊기지 않는다.
/// 메뉴: 도메네뷸라 > 에셋 정리 > 맵 배경 이름 바꾸기   (다 바꾼 뒤에는 이 파일을 지워도 됨)
/// </summary>
public static class RenameMapBackgrounds
{
    private const string Folder = "Assets/Sprite/";

    // 옛 이름(확장자 포함) → 새 이름(확장자 제외)
    private static readonly (string oldFile, string newName)[] Map =
    {
        ("배경.png",        "map1_classroom"),
        ("배경1.png",       "map2_temp"),
        ("배경2.png",       "map3_temp"),
        ("임시배경1.jpeg",  "map4_temp"),
        ("임시배경2.jpeg",  "map5_temp"),
        ("임시배경3.jpeg",  "map6_temp"),
        ("임시배경4.jpeg",  "map7_temp"),
        ("임시배경5.jpeg",  "map8_temp"),
        ("임시배경6.jpeg",  "map9_temp"),
        ("임시배경7.jpeg",  "map10_temp"),
    };

    [MenuItem("도메네뷸라/에셋 정리/맵 배경 이름 바꾸기")]
    private static void Run()
    {
        var log = new List<string>();
        int ok = 0;

        foreach (var (oldFile, newName) in Map)
        {
            string path = Folder + oldFile;
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                log.Add($"건너뜀(파일 없음): {oldFile}");
                continue;
            }

            string error = AssetDatabase.RenameAsset(path, newName);
            if (string.IsNullOrEmpty(error)) { ok++; log.Add($"{oldFile} → {newName}"); }
            else log.Add($"실패: {oldFile} → {newName} ({error})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[맵 배경 이름 바꾸기]\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("맵 배경 이름 바꾸기", $"{ok}개 이름을 바꿨습니다.\n자세한 내용은 Console 을 확인하세요.", "확인");
    }
}
