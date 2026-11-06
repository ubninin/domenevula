using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// 두벌식 한글 자판의 "키 위치 → 자모" 대응표. (타자연습의 자리연습용)
/// IME를 거치지 않고 눌린 키를 직접 읽기 때문에, 한/영 상태나 조합 중 글자와 상관없이
/// R 키 = ㄱ, K 키 = ㅏ 처럼 한 번 누르면 바로 해당 자모로 취급한다.
///   · Shift + 키 = 쌍자음 / ㅒ ㅖ  (ㄲ ㄸ ㅃ ㅆ ㅉ ㅒ ㅖ)
///   · 한국어 두벌식 자판 기준이다.
/// </summary>
public static class HangulKeyboard
{
    private struct Entry
    {
        public Key key;
        public string normal;
        public string shifted; // null이면 Shift를 눌러도 같은 글자
        public Entry(Key key, string normal, string shifted = null)
        {
            this.key = key;
            this.normal = normal;
            this.shifted = shifted;
        }
    }

    private static readonly Entry[] Table =
    {
        // 자음
        new Entry(Key.R, "ㄱ", "ㄲ"), new Entry(Key.S, "ㄴ"), new Entry(Key.E, "ㄷ", "ㄸ"), new Entry(Key.F, "ㄹ"),
        new Entry(Key.A, "ㅁ"), new Entry(Key.Q, "ㅂ", "ㅃ"), new Entry(Key.T, "ㅅ", "ㅆ"), new Entry(Key.D, "ㅇ"),
        new Entry(Key.W, "ㅈ", "ㅉ"), new Entry(Key.C, "ㅊ"), new Entry(Key.Z, "ㅋ"), new Entry(Key.X, "ㅌ"),
        new Entry(Key.V, "ㅍ"), new Entry(Key.G, "ㅎ"),
        // 모음
        new Entry(Key.K, "ㅏ"), new Entry(Key.I, "ㅑ"), new Entry(Key.J, "ㅓ"), new Entry(Key.U, "ㅕ"),
        new Entry(Key.H, "ㅗ"), new Entry(Key.Y, "ㅛ"), new Entry(Key.N, "ㅜ"), new Entry(Key.B, "ㅠ"),
        new Entry(Key.M, "ㅡ"), new Entry(Key.L, "ㅣ"), new Entry(Key.O, "ㅐ", "ㅒ"), new Entry(Key.P, "ㅔ", "ㅖ"),
    };

    private static HashSet<string> typable;

    // 한 프레임(~16ms)에 자모 키 두 개 이상을 거의 동시에 누르면 전부 wasPressedThisFrame이 true가 되는데,
    // 예전에는 Table에서 먼저 매칭되는 것 하나만 반환하고 나머지는 그냥 버렸다(연타 중 입력 누락).
    // 이번 프레임에 눌린 자모를 전부 모아뒀다가 한 번에 하나씩, 다음 호출들에서 순서대로 내보낸다.
    private static readonly Queue<string> pending = new Queue<string>();

    /// <summary>이 낱자를 키 하나로 칠 수 있는지 (단어 목록 검증용).</summary>
    public static bool IsTypable(string jamo)
    {
        if (typable == null)
        {
            typable = new HashSet<string>();
            foreach (Entry e in Table)
            {
                typable.Add(e.normal);
                if (e.shifted != null) typable.Add(e.shifted);
            }
        }
        return !string.IsNullOrEmpty(jamo) && typable.Contains(jamo);
    }

    /// <summary>
    /// 이번 프레임에 새로 눌린 자모 키가 있으면 그 자모를, 없으면 null.
    /// 직전 프레임에 여러 키를 거의 동시에 눌러서 아직 못 내보낸 자모가 쌓여있으면 그것부터 하나씩 내보낸다.
    /// </summary>
    public static string ReadJamoPressedThisFrame()
    {
        if (pending.Count > 0) return pending.Dequeue();

        Keyboard kb = Keyboard.current;
        if (kb == null) return null;

        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        foreach (Entry e in Table)
        {
            if (kb[e.key].wasPressedThisFrame)
                pending.Enqueue((shift && e.shifted != null) ? e.shifted : e.normal);
        }

        return pending.Count > 0 ? pending.Dequeue() : null;
    }
}
