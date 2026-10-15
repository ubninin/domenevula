using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hayul_Walk_0 ~ Hayul_Walk_47 처럼 순서대로 잘라놓은 스프라이트들을,
/// 0~11 Down / 12~23 Left / 24~35 Right / 36~47 Up 순서로 MotionPreset 에셋의
/// Direction Frame Set에 한 번에 채워 넣는 도구.
///
/// 여는 법: 메뉴 Window → 도메네뷸라 → 모션 프리셋 채우기 도구
///
/// 쓰는 법:
/// 1. Target Motion Preset에 채워 넣을 MotionPreset 에셋을 끌어다 놓는다.
/// 2. Project 창에서 Hayul_Walk_0 ~ Hayul_Walk_47 스프라이트를 전부 선택한다
///    (이름 순서는 상관없음 - 파일명 끝 숫자를 기준으로 자동 정렬한다)
/// 3. "선택한 스프라이트 추가" 버튼을 누른다.
/// 4. 방향당 프레임 수(기본 12)를 확인하고 "채우기" 버튼을 누른다.
///    → 추가된 순서(숫자순)대로 앞에서부터 12개씩 Down/Left/Right/Up에 들어간다.
/// </summary>
public class FillMotionPresetWindow : EditorWindow
{
    private MotionPreset target;
    private int framesPerDirection = 12;
    private readonly List<Sprite> sprites = new List<Sprite>();
    private Vector2 scroll;

    [MenuItem("Window/도메네뷸라/모션 프리셋 채우기 도구")]
    private static void Open()
    {
        GetWindow<FillMotionPresetWindow>("모션 프리셋 채우기").Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Project 창에서 순서대로 잘라놓은 스프라이트들(예: Hayul_Walk_0~47)을 전부 선택한 뒤 " +
            "'선택한 스프라이트 추가'를 누르면, 파일명 끝 숫자 순서대로 정렬해서 담아준다.\n" +
            "그 다음 '채우기'를 누르면 앞에서부터 방향당 프레임 수만큼씩 끊어서 " +
            "Down → Left → Right → Up 순서로 MotionPreset에 들어간다.",
            MessageType.Info);

        EditorGUILayout.Space();
        target = (MotionPreset)EditorGUILayout.ObjectField("Target Motion Preset", target, typeof(MotionPreset), false);
        framesPerDirection = EditorGUILayout.IntField("방향당 프레임 수", framesPerDirection);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("선택한 스프라이트 추가 (Project 창에서 선택 후)"))
            AddFromSelection();
        if (GUILayout.Button("목록 비우기", GUILayout.Width(100)))
            sprites.Clear();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"담긴 스프라이트: {sprites.Count}개 " +
            (sprites.Count > 0 ? $"(필요 개수: {framesPerDirection * 4}개)" : ""));

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(200));
        for (int i = 0; i < sprites.Count; i++)
        {
            string dirLabel = framesPerDirection > 0 ? DirLabel(i / Mathf.Max(1, framesPerDirection)) : "?";
            EditorGUILayout.LabelField($"{i:00}  [{dirLabel}]  {sprites[i]?.name}");
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        GUI.enabled = target != null && sprites.Count > 0 && framesPerDirection > 0;
        if (GUILayout.Button("채우기", GUILayout.Height(30)))
            Fill();
        GUI.enabled = true;
    }

    private static string DirLabel(int index) => index switch
    {
        0 => "Down",
        1 => "Left",
        2 => "Right",
        3 => "Up",
        _ => "범위 초과(무시됨)",
    };

    private void AddFromSelection()
    {
        var picked = Selection.objects.OfType<Sprite>().ToList();
        if (picked.Count == 0)
        {
            EditorUtility.DisplayDialog("스프라이트 없음", "Project 창에서 스프라이트를 먼저 선택하세요.", "확인");
            return;
        }

        // 파일명 끝의 숫자를 기준으로 자연스러운 순서로 정렬 (Hayul_Walk_2 가 Hayul_Walk_10보다 앞에 오도록)
        picked.Sort((a, b) => NaturalKey(a.name).CompareTo(NaturalKey(b.name)));

        sprites.Clear();
        sprites.AddRange(picked);
        Repaint();
    }

    private static int NaturalKey(string name)
    {
        Match m = Regex.Match(name, @"(\d+)$");
        return m.Success ? int.Parse(m.Value) : int.MaxValue;
    }

    private void Fill()
    {
        if (target.frames == null) target.frames = new DirectionalFrameSet();

        Sprite[][] buckets = new Sprite[4][];
        for (int d = 0; d < 4; d++)
            buckets[d] = sprites.Skip(d * framesPerDirection).Take(framesPerDirection).ToArray();

        Undo.RecordObject(target, "모션 프리셋 채우기");
        target.frames.down = PadOrKeep(buckets[0], target.frames.down);
        target.frames.left = PadOrKeep(buckets[1], target.frames.left);
        target.frames.right = PadOrKeep(buckets[2], target.frames.right);
        target.frames.up = PadOrKeep(buckets[3], target.frames.up);

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();

        int filled = buckets.Sum(b => b.Length);
        EditorUtility.DisplayDialog("완료", $"{target.name}에 {filled}개 프레임을 채웠습니다.", "확인");
    }

    /// <summary>새로 채울 프레임이 비어있으면(선택한 스프라이트가 모자라서) 기존 값을 그대로 둔다.</summary>
    private static Sprite[] PadOrKeep(Sprite[] newFrames, Sprite[] existing)
    {
        if (newFrames == null || newFrames.Length == 0) return existing ?? Array.Empty<Sprite>();
        return newFrames;
    }
}
