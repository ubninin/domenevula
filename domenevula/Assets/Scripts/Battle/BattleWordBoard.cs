using System;
using System.Collections.Generic;

/// <summary>
/// 칸마다 붙는 단어를 관리한다 (Unity 의존성 없는 순수 C#).
/// - 화면에 떠 있는 단어끼리는 절대 헷갈리지 않게 뽑는다 (HangulWordRules.Conflicts).
/// - 단계(tier)가 오를수록 어려운 단어 목록을 쓴다.
/// - 칸 수가 단어 풀보다 많거나 충돌 없는 조합을 못 찾으면, 마지막 수단으로 "같은 단어만 아니면" 허용하고 경고 콜백을 부른다.
/// </summary>
public class BattleWordBoard
{
    private readonly IList<WordTier> tiers;
    private readonly Random rng;
    private readonly string[] words;
    private int tier;

    public Action<string> Warn;

    public string[] Words => words;
    public int CellCount => words.Length;
    public int Tier => tier;

    public string TierName
    {
        get
        {
            if (tiers == null || tiers.Count == 0) return "-";
            return tiers[Clamp(tier, 0, tiers.Count - 1)].name;
        }
    }

    /// <summary>현재 단계가 단어 고정 모드인지 (칸 단어가 바뀌지 않는다).</summary>
    public bool KeepWords
    {
        get
        {
            if (tiers == null || tiers.Count == 0) return false;
            return tiers[Clamp(tier, 0, tiers.Count - 1)].UsesFixedWords;
        }
    }

    public int TierCount => tiers == null ? 0 : tiers.Count;

    public BattleWordBoard(IList<WordTier> tiers, int cellCount, int seed)
    {
        this.tiers = tiers;
        this.words = new string[cellCount];
        this.rng = new Random(seed);
    }

    public void SetTier(int t)
    {
        if (tiers == null || tiers.Count == 0) { tier = 0; return; }
        tier = Clamp(t, 0, tiers.Count - 1);
    }

    /// <summary>모든 칸의 단어를 새로 뽑는다 (적 턴 시작 시).</summary>
    public void FillAll()
    {
        for (int i = 0; i < words.Length; i++) words[i] = null;

        if (KeepWords) { FillFixed(); return; }

        // 칸 순서를 섞어서 뽑아야 특정 칸만 불리해지지 않음
        var order = new List<int>();
        for (int i = 0; i < words.Length; i++) order.Add(i);
        Shuffle(order);
        foreach (int cell in order) words[cell] = Draw(cell);
    }

    /// <summary>고정 모드: Fixed Words 를 칸 순서대로 놓는다. 비는 칸은 Words 에서 뽑는다.</summary>
    private void FillFixed()
    {
        string[] fx = tiers[Clamp(tier, 0, tiers.Count - 1)].fixedWords;
        var drawLater = new List<int>();
        for (int i = 0; i < words.Length; i++)
        {
            if (i < fx.Length && !string.IsNullOrEmpty(fx[i])) words[i] = fx[i];
            else drawLater.Add(i);
        }
        foreach (int cell in drawLater) words[cell] = Draw(cell);

        for (int a = 0; a < words.Length; a++)
            for (int b = a + 1; b < words.Length; b++)
                if (words[a] == words[b] || HangulWordRules.Conflicts(words[a], words[b]))
                {
                    Warn?.Invoke($"고정 단어 '{words[a]}'(칸 {a})와 '{words[b]}'(칸 {b})는 입력할 때 서로 헷갈릴 수 있습니다.");
                    return;
                }
    }

    /// <summary>한 칸의 단어만 새로 뽑는다 (그 칸 단어를 쳤을 때). 고정 모드에서는 아무것도 하지 않는다.</summary>
    public void Refresh(int cell)
    {
        if (cell < 0 || cell >= words.Length) return;
        if (KeepWords) return;
        words[cell] = Draw(cell);
    }

    public int Match(string typed, bool tolerant)
    {
        return HangulWordRules.MatchIndex(typed, words, tolerant);
    }

    private string Draw(int cell)
    {
        string[] pool = CurrentPool();
        if (pool.Length == 0) return "?";

        // 후보를 섞어서 순서대로 시도
        var candidates = new List<string>(pool);
        Shuffle(candidates);

        foreach (string c in candidates)
            if (!ConflictsWithBoard(c, cell)) return c;

        // 충돌 없는 단어가 없음 → 같은 단어만 피한다 (풀이 너무 작다는 신호)
        Warn?.Invoke($"단어 풀({TierName})이 작아서 충돌 없는 단어를 못 찾았습니다. 단어를 더 추가하세요.");
        foreach (string c in candidates)
        {
            bool same = false;
            for (int i = 0; i < words.Length; i++)
                if (i != cell && words[i] == c) { same = true; break; }
            if (!same) return c;
        }
        return candidates[0];
    }

    private bool ConflictsWithBoard(string candidate, int cell)
    {
        for (int i = 0; i < words.Length; i++)
        {
            if (i == cell || words[i] == null) continue;
            if (HangulWordRules.Conflicts(candidate, words[i])) return true;
        }
        return false;
    }

    private string[] CurrentPool()
    {
        if (tiers == null || tiers.Count == 0) return new string[0];
        var t = tiers[Clamp(tier, 0, tiers.Count - 1)];
        return t.words ?? new string[0];
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
}
