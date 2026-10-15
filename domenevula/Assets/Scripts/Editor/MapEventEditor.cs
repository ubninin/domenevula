using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MapEvent의 페이지/커맨드 리스트를 편하게 편집하기 위한 커스텀 인스펙터.
/// 페이지마다 조건·모습·트리거·커맨드를 따로 보여주고, 커맨드는 기본 유니티의
/// "SerializeReference 리스트 우클릭 → Add Managed Reference" 대신 눈에 보이는 버튼으로 추가한다.
///
/// 붙이는 곳: Assets/Scripts/Editor/ 폴더
/// </summary>
[CustomEditor(typeof(MapEvent))]
public class MapEventEditor : Editor
{
    private static List<Type> cachedCommandTypes;

    // 커맨드 타입 이름을 한글로 보여주기 위한 표. 새 커맨드 타입을 추가하면 여기에도 한 줄 추가.
    private static readonly Dictionary<string, string> KoreanNames = new Dictionary<string, string>
    {
        { nameof(ShowDialogueCommand), "대사 표시" },
        { nameof(WaitCommand), "대기" },
        { nameof(SetSwitchCommand), "스위치 설정" },
        { nameof(AddVariableCommand), "변수 증감" },
        { nameof(SetSelfSwitchCommand), "셀프 스위치 설정" },
        { nameof(ConditionalBranchCommand), "조건 분기" },
        { nameof(StartBattleCommand), "전투 시작" },
        { nameof(StartQuestCommand), "퀘스트 시작" },
        { nameof(CompleteQuestCommand), "퀘스트 완료" },
        { nameof(TeleportPlayerCommand), "플레이어 이동(순간이동)" },
        { nameof(PlayEffectCommand), "이펙트 재생" },
        { nameof(DeactivateSelfCommand), "이 이벤트 비활성화" },
    };

    private static string ToKorean(Type type) =>
        KoreanNames.TryGetValue(type.Name, out string ko) ? ko : type.Name;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // pages를 제외한 나머지 필드(Interact Range, Prompt Indicator, Sprite Renderer)는 기본 방식 그대로.
        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (prop.name == "pages") continue;
            EditorGUILayout.PropertyField(prop, true);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("페이지 (RPG Maker 이벤트 페이지와 동일한 개념)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "위에서부터 검사해서 조건을 만족하는 '마지막' 페이지가 자동으로 활성화됩니다.\n" +
            "1번 페이지는 조건 없이 기본 모습으로 두는 걸 추천합니다.", MessageType.None);

        SerializedProperty pagesProp = serializedObject.FindProperty("pages");
        for (int p = 0; p < pagesProp.arraySize; p++)
        {
            bool deleted = DrawPage(pagesProp, p);
            if (deleted) break; // 리스트가 바뀌었으니 이번 Draw는 여기서 중단
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("+ 페이지 추가", GUILayout.Height(28)))
        {
            int index = pagesProp.arraySize;
            pagesProp.InsertArrayElementAtIndex(index);
            ResetPageToDefault(pagesProp.GetArrayElementAtIndex(index));
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>페이지 하나를 그린다. true를 돌려주면 이번 프레임에 삭제되어 더 이상 그리면 안 됨.</summary>
    private bool DrawPage(SerializedProperty pagesProp, int index)
    {
        SerializedProperty page = pagesProp.GetArrayElementAtIndex(index);

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"페이지 {index + 1}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("▲", GUILayout.Width(24)) && index > 0)
            pagesProp.MoveArrayElement(index, index - 1);
        if (GUILayout.Button("▼", GUILayout.Width(24)) && index < pagesProp.arraySize - 1)
            pagesProp.MoveArrayElement(index, index + 1);
        bool deletePage = GUILayout.Button("페이지 삭제", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        if (deletePage)
        {
            pagesProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndVertical();
            return true;
        }

        EditorGUILayout.LabelField("조건 (전부 선택 사항)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(page.FindPropertyRelative("requiredSwitch"));
        EditorGUILayout.PropertyField(page.FindPropertyRelative("requiredValue"));
        EditorGUILayout.PropertyField(page.FindPropertyRelative("requiredSelfSwitch"));
        EditorGUILayout.PropertyField(page.FindPropertyRelative("requiredSelfSwitchValue"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("모습 / 동작", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(page.FindPropertyRelative("sprite"));
        EditorGUILayout.PropertyField(page.FindPropertyRelative("trigger"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("실행할 내용 (커맨드 목록)", EditorStyles.miniBoldLabel);
        DrawCommandList(page.FindPropertyRelative("commands"));

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);
        return false;
    }

    private static void ResetPageToDefault(SerializedProperty page)
    {
        page.FindPropertyRelative("requiredSwitch").stringValue = "";
        page.FindPropertyRelative("requiredValue").boolValue = true;
        page.FindPropertyRelative("requiredSelfSwitch").stringValue = "";
        page.FindPropertyRelative("requiredSelfSwitchValue").boolValue = false;
        page.FindPropertyRelative("sprite").objectReferenceValue = null;
        page.FindPropertyRelative("trigger").enumValueIndex = 0;
        page.FindPropertyRelative("commands").ClearArray();
    }

    private void DrawCommandList(SerializedProperty commandsProp)
    {
        for (int i = 0; i < commandsProp.arraySize; i++)
        {
            SerializedProperty element = commandsProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical("helpBox");
            EditorGUILayout.BeginHorizontal();

            string typeName = GetReadableTypeName(element);
            EditorGUILayout.LabelField($"{i}. {typeName}", EditorStyles.boldLabel);

            if (GUILayout.Button("▲", GUILayout.Width(24)) && i > 0)
                commandsProp.MoveArrayElement(i, i - 1);
            if (GUILayout.Button("▼", GUILayout.Width(24)) && i < commandsProp.arraySize - 1)
                commandsProp.MoveArrayElement(i, i + 1);
            if (GUILayout.Button("삭제", GUILayout.Width(50)))
            {
                commandsProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return; // 리스트가 바뀌었으니 이번 Draw는 여기서 중단
            }

            EditorGUILayout.EndHorizontal();

            if (element.managedReferenceValue != null)
                EditorGUILayout.PropertyField(element, GUIContent.none, true);
            else
                EditorGUILayout.HelpBox("아래 '커맨드 추가' 버튼으로 타입을 지정하세요.", MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("+ 커맨드 추가", GUILayout.Height(24)))
            ShowAddCommandMenu(commandsProp);
    }

    private static void ShowAddCommandMenu(SerializedProperty commandsProp)
    {
        GenericMenu menu = new GenericMenu();
        foreach (Type type in GetCommandTypes())
        {
            string label = ToKorean(type);
            menu.AddItem(new GUIContent(label), false, () =>
            {
                commandsProp.serializedObject.Update();
                int index = commandsProp.arraySize;
                commandsProp.InsertArrayElementAtIndex(index);
                SerializedProperty newElement = commandsProp.GetArrayElementAtIndex(index);
                newElement.managedReferenceValue = Activator.CreateInstance(type);
                commandsProp.serializedObject.ApplyModifiedProperties();
            });
        }
        menu.ShowAsContext();
    }

    private static List<Type> GetCommandTypes()
    {
        if (cachedCommandTypes != null) return cachedCommandTypes;

        Type baseType = typeof(EventCommand);
        cachedCommandTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .OrderBy(t => t.Name)
            .ToList();

        return cachedCommandTypes;
    }

    private static string GetReadableTypeName(SerializedProperty element)
    {
        if (element.managedReferenceValue == null) return "(타입 미지정)";
        return ToKorean(element.managedReferenceValue.GetType());
    }
}
