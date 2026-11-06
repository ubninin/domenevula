using UnityEditor;
using UnityEngine;

/// <summary>
/// 전투 씬의 BattleManager 인스펙터에서 맞춰둔 전투 설정을 BattleEncounter 에셋으로 내보낸다.
///
/// 쓰는 법: Hierarchy 에서 BattleManager 가 붙은 오브젝트를 선택 → Inspector 에서 BattleManager 컴포넌트 오른쪽 위
///          점 세 개(⋮) 메뉴 (또는 컴포넌트 이름 줄 우클릭)
///   · "전투 설정을 새 Battle Encounter 에셋으로 내보내기"   : 저장 위치/이름을 정하면 새 에셋이 만들어진다 (이름은 enc_ 로 시작 권장)
///   · "전투 설정을 기존 Battle Encounter 에셋에 덮어쓰기"   : Project 창에서 에셋을 선택해둔 상태면 그 에셋에, 아니면 파일 선택창으로 고른 에셋에 덮어쓴다
///
/// 내보내는 항목 (BattleManager → BattleEncounter):
///   Enemy → Enemy,  Battle Background → Background,  Word Tiers,  Round Layouts,  Round Messages,  Round Commands,  Round Lines
/// 내보내지 않는 항목: 파티원·Settings(수치)·폰트·Message·Turn Banner 등은 원래 인카운터가 전투 씬의 BattleManager 에서
///   그대로 가져다 쓰는 값이라 에셋에 없다. Transition(필드↔전투 전환 연출)은 BattleManager 에 없는 값이라
///   새 에셋은 기본값, 덮어쓰기는 기존 에셋의 값을 그대로 유지한다.
/// </summary>
public static class BattleEncounterExporter
{
    private const string DefaultFolder = "Assets/Battle";

    // BattleManager 필드 이름 → BattleEncounter 필드 이름
    private static readonly string[,] Fields =
    {
        { "enemy", "enemy" },
        { "battleBackground", "background" },
        { "wordTiers", "wordTiers" },
        { "roundLayouts", "roundLayouts" },
        { "roundMessages", "roundMessages" },
        { "roundCommands", "roundCommands" },
        { "roundLines", "roundLines" },
    };

    [MenuItem("CONTEXT/BattleManager/전투 설정을 새 Battle Encounter 에셋으로 내보내기")]
    private static void ExportNew(MenuCommand command)
    {
        BattleManager manager = command.context as BattleManager;
        if (manager == null) return;

        if (!AssetDatabase.IsValidFolder(DefaultFolder)) AssetDatabase.CreateFolder("Assets", "Battle");

        string path = EditorUtility.SaveFilePanelInProject("새 Battle Encounter 에셋으로 내보내기",
            "enc_new", "asset", "저장할 위치와 이름을 정하세요 (예: enc_slime_01)", DefaultFolder);
        if (string.IsNullOrEmpty(path)) return;

        // 같은 이름이 이미 있으면 저장창에서 이미 덮어쓰기 확인을 받았으므로 그 에셋의 내용만 교체한다 (다른 곳의 참조가 안 깨지게)
        BattleEncounter enc = AssetDatabase.LoadAssetAtPath<BattleEncounter>(path);
        bool created = enc == null;
        if (created)
        {
            enc = ScriptableObject.CreateInstance<BattleEncounter>();
            AssetDatabase.CreateAsset(enc, path);
        }

        CopyAndSave(manager, enc, created ? "새 에셋 생성" : "기존 에셋 갱신");
    }

    [MenuItem("CONTEXT/BattleManager/전투 설정을 기존 Battle Encounter 에셋에 덮어쓰기")]
    private static void ExportOverwrite(MenuCommand command)
    {
        BattleManager manager = command.context as BattleManager;
        if (manager == null) return;

        BattleEncounter enc = Selection.activeObject as BattleEncounter;
        if (enc == null)
        {
            string abs = EditorUtility.OpenFilePanel("덮어쓸 Battle Encounter 에셋 선택",
                System.IO.Path.GetFullPath(DefaultFolder), "asset");
            if (string.IsNullOrEmpty(abs)) return;

            string rel = FileUtil.GetProjectRelativePath(abs);
            enc = string.IsNullOrEmpty(rel) ? null : AssetDatabase.LoadAssetAtPath<BattleEncounter>(rel);
            if (enc == null)
            {
                EditorUtility.DisplayDialog("내보내기", "프로젝트 안의 Battle Encounter 에셋이 아닙니다.", "확인");
                return;
            }
        }

        if (!EditorUtility.DisplayDialog("덮어쓰기 확인",
                $"'{enc.name}' 의 적 / 단어 단계 / 라운드별 설정이 지금 BattleManager 의 값으로 바뀝니다.\n(Ctrl+Z 로 되돌릴 수 있습니다)",
                "덮어쓰기", "취소"))
            return;

        CopyAndSave(manager, enc, "기존 에셋 덮어쓰기");
    }

    /// <summary>SerializedProperty 를 종류별로 재귀 복사한다 (배열 크기 → 각 칸, 클래스/구조체 → 각 필드, 나머지는 값).</summary>
    private static void CopyProp(SerializedProperty s, SerializedProperty d)
    {
        if (s.propertyType == SerializedPropertyType.Generic)
        {
            if (s.isArray)
            {
                d.arraySize = s.arraySize;
                for (int i = 0; i < s.arraySize; i++)
                    CopyProp(s.GetArrayElementAtIndex(i), d.GetArrayElementAtIndex(i));
                return;
            }

            SerializedProperty sc = s.Copy();
            SerializedProperty dc = d.Copy();
            SerializedProperty end = s.GetEndProperty();
            if (!sc.NextVisible(true) || !dc.NextVisible(true)) return;
            do
            {
                if (SerializedProperty.EqualContents(sc, end)) break;
                CopyProp(sc, dc);
            }
            while (sc.NextVisible(false) && dc.NextVisible(false));
            return;
        }

        switch (s.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.Character:
                d.longValue = s.longValue; break;
            case SerializedPropertyType.Boolean: d.boolValue = s.boolValue; break;
            case SerializedPropertyType.Float: d.floatValue = s.floatValue; break;
            case SerializedPropertyType.String: d.stringValue = s.stringValue; break;
            case SerializedPropertyType.Color: d.colorValue = s.colorValue; break;
            case SerializedPropertyType.ObjectReference: d.objectReferenceValue = s.objectReferenceValue; break;
            case SerializedPropertyType.Enum: d.intValue = s.intValue; break; // enumValueIndex 는 값이 번호와 다른 enum 에서 범위 오류가 나서 원래 숫자 값을 그대로 복사
            case SerializedPropertyType.Vector2: d.vector2Value = s.vector2Value; break;
            case SerializedPropertyType.Vector3: d.vector3Value = s.vector3Value; break;
            case SerializedPropertyType.Vector4: d.vector4Value = s.vector4Value; break;
            case SerializedPropertyType.Vector2Int: d.vector2IntValue = s.vector2IntValue; break;
            case SerializedPropertyType.Vector3Int: d.vector3IntValue = s.vector3IntValue; break;
            case SerializedPropertyType.Rect: d.rectValue = s.rectValue; break;
            case SerializedPropertyType.RectInt: d.rectIntValue = s.rectIntValue; break;
            case SerializedPropertyType.Bounds: d.boundsValue = s.boundsValue; break;
            case SerializedPropertyType.Quaternion: d.quaternionValue = s.quaternionValue; break;
            case SerializedPropertyType.AnimationCurve: d.animationCurveValue = s.animationCurveValue; break;
            case SerializedPropertyType.ArraySize: break; // arraySize 는 위에서 처리됨
            default:
                Debug.LogWarning($"[전투 내보내기] 복사하지 못한 항목 (종류 {s.propertyType}): {s.propertyPath}");
                break;
        }
    }

    private static void CopyAndSave(BattleManager manager, BattleEncounter enc, string label)
    {
        Undo.RecordObject(enc, "전투 설정 내보내기");

        var src = new SerializedObject(manager);
        var dst = new SerializedObject(enc);
        src.UpdateIfRequiredOrScript();
        dst.UpdateIfRequiredOrScript();

        int copied = 0;
        for (int i = 0; i < Fields.GetLength(0); i++)
        {
            SerializedProperty s = src.FindProperty(Fields[i, 0]);
            SerializedProperty d = dst.FindProperty(Fields[i, 1]);
            if (s == null || d == null)
            {
                Debug.LogWarning($"[전투 내보내기] 필드를 찾지 못해 건너뜀: {Fields[i, 0]} → {Fields[i, 1]}");
                continue;
            }
            CopyProp(s, d); // 리스트/중첩 클래스/스프라이트 참조까지 한 칸씩 직접 복사
            copied++;
        }
        dst.ApplyModifiedProperties();

        EditorUtility.SetDirty(enc);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(enc); // Project 창에서 만든 에셋을 깜빡여 보여줌 (선택은 그대로 둔다)

        Debug.Log($"[전투 내보내기] {label}: {AssetDatabase.GetAssetPath(enc)} ({copied}개 항목 복사: 적, 배경, 단어 단계, 라운드별 칸 배치/안내 문구/행동/대사)", enc);
    }
}
