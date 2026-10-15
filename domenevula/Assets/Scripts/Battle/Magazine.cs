using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>탄창이 탄을 담고 꺼내는 방식.</summary>
public enum MagazineKind
{
    [InspectorName("스택 (마지막에 넣은 탄이 먼저)")] Stack,
    [InspectorName("큐 (먼저 넣은 탄이 먼저)")] Queue,
    [InspectorName("리스트 (넣을 위치를 자유롭게)")] List,
    [InspectorName("덱 (앞/뒤에 넣을 수 있음)")] Deck,
}

/// <summary>탄 종류 하나. 총알 하나 = 타이밍 커서 하나.</summary>
[Serializable]
public class BulletDef
{
    public string name = "탄";
    public BattleElement element = BattleElement.None;
    [Tooltip("데미지 배율 (공격력 × 이 값 × 타이밍 배율)")]
    public float power = 1f;
    [Tooltip("커서 색. (0,0,0,0) 이면 속성 색을 쓴다")]
    public Color color = new Color(0f, 0f, 0f, 0f);

    public BulletDef() { }
    public BulletDef(string name, BattleElement element, float power = 1f)
    {
        this.name = name; this.element = element; this.power = power;
    }

    public Color CursorColor => color.a > 0.01f ? color : ElementChart.ColorOf(element);
}

/// <summary>가지고 있는 탄 개수 (탄 이름 → 개수) 설정용.</summary>
[Serializable]
public class BulletStock
{
    public string bulletName = "";
    public int count = 1;

    public BulletStock() { }
    public BulletStock(string n, int c) { bulletName = n; count = c; }
}

/// <summary>
/// 탄창. 발사 순서는 항상 index 0 부터 (한쪽 고정).  넣을 때만 종류에 따라 위치가 달라진다.
///   Stack : 항상 맨 앞(index 0)에 쌓임 → 마지막에 넣은 탄이 먼저 나감. 위치 선택 없음
///   Queue : 항상 맨 뒤에 붙음 → 먼저 넣은 탄이 먼저 나감. 위치 선택 없음
///   List  : 0..Count 중 아무 위치
///   Deck  : 맨 앞(0) 또는 맨 뒤(Count)
/// 순수 C# 이라 유니티 없이 테스트할 수 있다.
/// </summary>
public class Magazine
{
    public const int MaxCapacity = 8;

    public MagazineKind Kind { get; private set; }
    public int Capacity { get; private set; }
    private readonly List<BulletDef> bullets = new List<BulletDef>();

    public Magazine(MagazineKind kind, int capacity)
    {
        Kind = kind;
        Capacity = Math.Max(1, Math.Min(MaxCapacity, capacity));
    }

    public int Count => bullets.Count;
    public bool IsFull => bullets.Count >= Capacity;
    public IList<BulletDef> Bullets => bullets;

    /// <summary>넣을 수 있는 위치 목록 (index). 가득 찼으면 빈 목록.</summary>
    public List<int> InsertPositions()
    {
        var r = new List<int>();
        if (IsFull) return r;
        switch (Kind)
        {
            case MagazineKind.Stack: r.Add(0); break;
            case MagazineKind.Queue: r.Add(bullets.Count); break;
            case MagazineKind.Deck: r.Add(0); if (bullets.Count > 0) r.Add(bullets.Count); break;
            default: for (int i = 0; i <= bullets.Count; i++) r.Add(i); break;
        }
        return r;
    }

    /// <summary>position 은 InsertPositions() 에 있는 값이어야 한다.</summary>
    public bool TryInsert(BulletDef bullet, int position)
    {
        if (bullet == null || !InsertPositions().Contains(position)) return false;
        bullets.Insert(position, bullet);
        return true;
    }

    /// <summary>앞에서부터 최대 max 발(0 이하면 전부)을 꺼내 탄창에서 지운다.</summary>
    public List<BulletDef> TakeForFire(int max)
    {
        int n = max > 0 ? Math.Min(max, bullets.Count) : bullets.Count;
        var r = bullets.GetRange(0, n);
        bullets.RemoveRange(0, n);
        return r;
    }

    /// <summary>종류/용량이 바뀌었을 때 새 탄창으로 옮긴다 (넘치는 뒤쪽 탄은 버려지지 않고 반환됨).</summary>
    public Magazine Rebuilt(MagazineKind kind, int capacity, out List<BulletDef> overflow)
    {
        var m = new Magazine(kind, capacity);
        overflow = new List<BulletDef>();
        foreach (BulletDef b in bullets)
        {
            if (m.bullets.Count < m.Capacity) m.bullets.Add(b);
            else overflow.Add(b);
        }
        return m;
    }

    /// <summary>"화염 → 물 → 바람  (3/6)"  (왼쪽이 먼저 발사)</summary>
    public string Describe()
    {
        if (bullets.Count == 0) return $"비어 있음 (0/{Capacity})";
        var names = new List<string>();
        foreach (BulletDef b in bullets) names.Add(b.name);
        return $"{string.Join(" → ", names)} ({bullets.Count}/{Capacity})";
    }

    public static string KindLabel(MagazineKind k)
    {
        switch (k)
        {
            case MagazineKind.Stack: return "스택";
            case MagazineKind.Queue: return "큐";
            case MagazineKind.Deck: return "덱";
            default: return "리스트";
        }
    }

    /// <summary>InsertPositions 의 한 항목을 메뉴에 보여줄 글로.</summary>
    public string PositionLabel(int position)
    {
        switch (Kind)
        {
            case MagazineKind.Stack: return "맨 위에 쌓기 (먼저 발사)";
            case MagazineKind.Queue: return "맨 뒤에 넣기";
            case MagazineKind.Deck: return position == 0 ? "앞에 넣기 (먼저 발사)" : "뒤에 넣기 (나중 발사)";
            default:
                if (position == 0) return "1번째 (맨 앞)";
                if (position == bullets.Count) return $"{position + 1}번째 (맨 뒤)";
                return $"{position + 1}번째";
        }
    }
}

/// <summary>
/// 전투와 전투 사이에 유지되는 탄 재고/탄창. (게임을 껐다 켜면 사라진다 — 저장 기능은 아직 없음)
/// 탄창 종류는 맵 씬에서만 바꾼다: BattleInventory.SetMagazineKind("라임", MagazineKind.Deck, 6)
/// 탄 얻기: BattleInventory.AddBullets("화염탄", 3)
/// </summary>
public static class BattleInventory
{
    private static readonly Dictionary<string, int> stock = new Dictionary<string, int>();
    private static readonly Dictionary<string, Magazine> magazines = new Dictionary<string, Magazine>();
    private static readonly Dictionary<string, KeyValuePair<MagazineKind, int>> kindOverride =
        new Dictionary<string, KeyValuePair<MagazineKind, int>>();
    private static bool stockInitialized;

    public static void Reset()
    {
        stock.Clear(); magazines.Clear(); kindOverride.Clear(); stockInitialized = false;
    }

    /// <summary>처음 한 번만 설정(Starting Bullets)으로 재고를 채운다.</summary>
    public static void InitStock(IList<BulletStock> starting)
    {
        if (stockInitialized) return;
        stockInitialized = true;
        if (starting == null) return;
        foreach (BulletStock s in starting)
            if (s != null && !string.IsNullOrEmpty(s.bulletName) && s.count > 0) AddBullets(s.bulletName, s.count);
    }

    public static int GetCount(string bulletName)
    {
        int c; return stock.TryGetValue(bulletName, out c) ? c : 0;
    }

    public static void AddBullets(string bulletName, int count)
    {
        stock[bulletName] = Math.Max(0, GetCount(bulletName) + count);
    }

    public static bool TryConsume(string bulletName)
    {
        int c = GetCount(bulletName);
        if (c <= 0) return false;
        stock[bulletName] = c - 1;
        return true;
    }

    /// <summary>맵 씬에서 탄창 종류/용량을 바꾼다. 안에 든 탄은 가능한 만큼 유지되고 넘치는 탄은 재고로 돌아간다.</summary>
    public static void SetMagazineKind(string actorName, MagazineKind kind, int capacity)
    {
        kindOverride[actorName] = new KeyValuePair<MagazineKind, int>(kind, capacity);
    }

    /// <summary>해당 캐릭터의 탄창 (없으면 설정값으로 만든다).</summary>
    public static Magazine GetMagazine(string actorName, MagazineKind defKind, int defCapacity)
    {
        MagazineKind kind = defKind; int cap = defCapacity;
        KeyValuePair<MagazineKind, int> o;
        if (kindOverride.TryGetValue(actorName, out o)) { kind = o.Key; cap = o.Value; }
        cap = Math.Max(1, Math.Min(Magazine.MaxCapacity, cap));

        Magazine m;
        if (!magazines.TryGetValue(actorName, out m))
        {
            m = new Magazine(kind, cap);
            magazines[actorName] = m;
        }
        else if (m.Kind != kind || m.Capacity != cap)
        {
            List<BulletDef> overflow;
            m = m.Rebuilt(kind, cap, out overflow);
            foreach (BulletDef b in overflow) AddBullets(b.name, 1);
            magazines[actorName] = m;
        }
        return m;
    }
}
