using System;
using System.Collections.Generic;

/// <summary>
/// 타자 입력 판정 규칙 (Unity 의존성 없는 순수 C#).
///
/// taja(게임잼)에서 났던 오판 버그를 막기 위한 규칙:
///  1) "정확히 일치"가 항상 최우선이다. (taja는 1번 단어를 먼저 검사해서, 2번 단어를 쳐도 1번으로 처리되곤 했음)
///  2) 한글 IME 때문에 마지막 글자가 덜 조합된 채로 Enter가 눌릴 수 있어서, 두 가지 경우만 너그럽게 봐준다.
///       a) 마지막 글자가 통째로 빠짐        예) 목표 "단어" → 입력 "단"   (2글자 이상 단어만)
///       b) 마지막 글자의 받침만 빠짐        예) 목표 "단어" → 입력 "단어" 는 정확 일치, 목표 "단" → 입력 "다"
///     너그러운 판정은 후보가 딱 하나일 때만 인정한다. 여러 칸이 후보면 아무 칸도 선택하지 않는다.
///  3) 단어판(BattleWordBoard)은 서로 "헷갈릴 수 있는" 단어를 같은 화면에 동시에 올리지 않는다. (Conflicts)
///  4) 한 글자 단어의 빈 문자열 비교 같은 함정이 없다: 입력이 비어 있으면 무조건 불일치.
/// </summary>
public static class HangulWordRules
{
    private const int SyllableBase = 0xAC00;
    private const int SyllableEnd = 0xD7A3;

    public static bool IsSyllable(char c) => c >= SyllableBase && c <= SyllableEnd;

    /// <summary>완성형 음절을 초성/중성/종성 인덱스로 분해. 종성 0 = 받침 없음.</summary>
    public static void Decompose(char c, out int initial, out int medial, out int final)
    {
        int s = c - SyllableBase;
        initial = s / 588;
        medial = (s % 588) / 28;
        final = s % 28;
    }

    /// <summary>
    /// typed가 word와 "마지막 글자의 받침만 빠진" 관계인지.
    /// 예) typed "다", word "단" → true.  typed "단어", word "단어" → false (그건 정확 일치).
    /// </summary>
    public static bool IsIncompleteVariant(string typed, string word)
    {
        if (string.IsNullOrEmpty(typed) || string.IsNullOrEmpty(word)) return false;
        if (typed.Length != word.Length) return false;

        int n = word.Length;
        for (int i = 0; i < n - 1; i++)
            if (typed[i] != word[i]) return false;

        char t = typed[n - 1];
        char w = word[n - 1];
        if (!IsSyllable(t) || !IsSyllable(w)) return false;

        Decompose(t, out int ti, out int tm, out int tf);
        Decompose(w, out int wi, out int wm, out int wf);
        return ti == wi && tm == wm && tf == 0 && wf != 0;
    }

    /// <summary>typed가 word에서 "마지막 글자 하나가 통째로 빠진" 입력인지. 2글자 이상 단어만 해당.</summary>
    public static bool IsDroppedLastChar(string typed, string word)
    {
        if (string.IsNullOrEmpty(typed) || string.IsNullOrEmpty(word)) return false;
        if (word.Length < 2) return false;
        return typed.Length == word.Length - 1 && word.StartsWith(typed, StringComparison.Ordinal);
    }

    /// <summary>
    /// 입력(typed)이 words[] 중 어느 칸 단어인지 찾는다. 못 찾으면 -1.
    /// </summary>
    /// <param name="tolerant">IME 미완성 입력을 너그럽게 볼지 (후보가 하나일 때만)</param>
    public static int MatchIndex(string typed, IList<string> words, bool tolerant)
    {
        if (typed == null || words == null) return -1;
        typed = typed.Trim();
        if (typed.Length == 0) return -1;

        // 1) 정확 일치가 최우선
        for (int i = 0; i < words.Count; i++)
        {
            if (!string.IsNullOrEmpty(words[i]) && string.Equals(words[i], typed, StringComparison.Ordinal))
                return i;
        }

        if (!tolerant) return -1;

        // 2) 너그러운 일치: 후보가 정확히 하나일 때만
        int found = -1;
        for (int i = 0; i < words.Count; i++)
        {
            string w = words[i];
            if (string.IsNullOrEmpty(w)) continue;
            if (IsDroppedLastChar(typed, w) || IsIncompleteVariant(typed, w))
            {
                if (found >= 0) return -1; // 후보가 둘 이상 → 모호하니 무시
                found = i;
            }
        }
        return found;
    }

    /// <summary>
    /// 두 단어를 같은 화면에 동시에 올리면 판정이 헷갈릴 수 있는지.
    /// (같은 단어 / 한쪽이 다른 쪽의 앞부분 / 너그러운 판정이 다른 쪽에도 걸릴 수 있음)
    /// </summary>
    public static bool Conflicts(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return true;
        if (string.Equals(a, b, StringComparison.Ordinal)) return true;
        if (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal)) return true;

        // a의 "마지막 글자 빠진 입력"이 b에도 해당될 수 있는가
        if (a.Length >= 2 && b.StartsWith(a.Substring(0, a.Length - 1), StringComparison.Ordinal)) return true;
        if (b.Length >= 2 && a.StartsWith(b.Substring(0, b.Length - 1), StringComparison.Ordinal)) return true;

        // 받침만 빠진 입력이 다른 쪽과 정확히 같아지는가
        if (IsIncompleteVariant(a, b) || IsIncompleteVariant(b, a)) return true;

        return false;
    }
}
