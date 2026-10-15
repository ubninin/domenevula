using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MapEvent의 Commands 리스트를 편하게 편집하기 위한 커스텀 인스펙터.
/// 기본 유니티의 "SerializeReference 리스트 우클릭 → Add Managed Reference" 방식이
/// 버전/설정에 따라 안 뜨는 경우가 있어서, 그 대신 눈에 보이는 버튼으로 커맨드를 추가하게 했다.
///
/// 붙이는 곳: Assets/Scripts/Editor/ 폴더 (반드시 "Editor"라는 이름의 폴더 안에 있어야
/// 유니티가 에디터 전용 코드로 인식한다)
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

    private static string ToKorean(Type type)
    {
        return KoreanNames.TryGetValue(type.Name, out string ko) ? ko : type.Name;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // commands를 제외한 나머지 필드는 기본 방식 그대로 그린다.
        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (prop.name == "commands") continue;
            EditorGUILayout.PropertyField(prop, true);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("실행할 내용 (커맨드 목록)", EditorStyles.boldLabel);

        SerializedProperty commandsProp = serializedObject.FindProperty("commands");
        for (int i = 0; i < commandsProp.arraySize; i++)
        {
            SerializedProperty element = commandsProp.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical("box");
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
                break; // 리스트가 바뀌었으니 이번 Draw는 여기서 중단
            }

            EditorGUILayout.EndHorizontal();

            if (element.managedReferenceValue != null)
                EditorGUILayout.PropertyField(element, GUIContent.none, true);
            else
                EditorGUILayout.HelpBox("아래 '커맨드 추가' 버튼으로 타입을 지정하세요.", MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("+ 커맨드 추가", GUILayout.Height(28)))
        {
            ShowAddCommandMenu(commandsProp);
        }

        serializedObject.ApplyModifiedProperties();
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
