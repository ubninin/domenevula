using System;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// 전투 프로토타입 공용 타입. 값은 전부 코드가 아니라 "데이터"로 취급해서
// 인스펙터(BattleManager)에서 바꿀 수 있게 해둔 것들이다.
// ─────────────────────────────────────────────────────────────

/// <summary>탄이 날아오는 쪽. 상하좌우 어디서든 발사 가능.</summary>
public enum AttackSide { Top, Bottom, Left, Right }

/// <summary>
/// 어느 줄(레인)로 쏠지.
/// Top/Bottom에서 오는 탄의 레인 = 열(column), Left/Right에서 오는 탄의 레인 = 행(row).
/// </summary>
public enum LaneSelect
{
    Fixed,      // PatternStep.lane 번 줄
    Random,     // 무작위 한 줄
    AtPlayer,   // 경고가 뜨는 순간 플레이어가 서 있는 줄 (조준탄)
    AllButOne   // 한 줄만 비우고 전부 (벽 탄막)
}

/// <summary>속성. 지금은 탄 색깔만 달라지고, 데미지 배율은 ElementChart에 연결할 자리만 만들어 둠.</summary>
public enum BattleElement { None, Fire, Water, Wind, Earth }

/// <summary>전투 배경 이미지를 화면에 맞추는 방식.</summary>
public enum BackgroundFit
{
    [Tooltip("화면을 빈틈없이 덮는다 (비율 유지, 남는 부분은 잘림)")] Cover,
    [Tooltip("이미지 전체가 보이게 맞춘다 (비율 유지, 남는 곳은 단색 배경)")] Contain,
    [Tooltip("비율 무시하고 화면에 꽉 채워 늘린다")] Stretch,
}

/// <summary>탄에 맞았을 때 누가 피해를 받는지.</summary>
public enum HitTargetMode { RandomAlive, AllParty, FirstAlive }

/// <summary>타이밍 타격 결과.</summary>
public enum TimingGrade { Miss, Normal, Good, Perfect }

public enum SkillKind { Damage, Heal }

public enum ActionKind { Attack, Skill, Guard }

/// <summary>속성 상성 자리. 나중에 속성 시스템을 넣을 때 여기만 채우면 된다.</summary>
public static class ElementChart
{
    public static float Multiplier(BattleElement attack, BattleElement defense)
    {
        return 1f;
    }

    public static Color ColorOf(BattleElement e)
    {
        switch (e)
        {
            case BattleElement.Fire: return new Color(1f, 0.55f, 0.2f);
            case BattleElement.Water: return new Color(0.35f, 0.65f, 1f);
            case BattleElement.Wind: return new Color(0.55f, 0.95f, 0.6f);
            case BattleElement.Earth: return new Color(0.75f, 0.6f, 0.4f);
            default: return new Color(1f, 0.93f, 0.93f);
        }
    }
}

/// <summary>탄막 패턴의 한 발(또는 한 줄 묶음) 정의.</summary>
[Serializable]
public class PatternStep
{
    [Tooltip("패턴이 시작되고 몇 초 뒤에 이 스텝을 시작할지")]
    public float time;
    [Tooltip("어느 쪽에서 날아오는지")]
    public AttackSide side = AttackSide.Top;
    public LaneSelect laneSelect = LaneSelect.Random;
    [Tooltip("LaneSelect가 Fixed일 때만 사용 (0부터 시작)")]
    public int lane;
    [Tooltip("경고가 표시되는 시간(초). 이 시간이 지나면 발사")]
    public float warning = 0.6f;
    [Tooltip("탄 속도 (캔버스 픽셀/초)")]
    public float speed = 420f;
    public int damage = 8;
    public BattleElement element = BattleElement.None;

    public PatternStep() { }

    public PatternStep(float time, AttackSide side, LaneSelect select, float warning, float speed, int damage,
                       BattleElement element = BattleElement.None, int lane = 0)
    {
        this.time = time;
        this.side = side;
        this.laneSelect = select;
        this.warning = warning;
        this.speed = speed;
        this.damage = damage;
        this.element = element;
        this.lane = lane;
    }
}

[Serializable]
public class AttackPattern
{
    public string name;
    public List<PatternStep> steps = new List<PatternStep>();

    public AttackPattern() { }
    public AttackPattern(string name) { this.name = name; }

    /// <summary>마지막 스텝이 끝나는 시각 (경고 포함). 패턴 사이 간격 계산용.</summary>
    public float EndTime()
    {
        float end = 0f;
        foreach (PatternStep s in steps)
            end = Mathf.Max(end, s.time + s.warning);
        return end;
    }
}

[Serializable]
public class BattleSkill
{
    public string name = "스킬";
    public SkillKind kind = SkillKind.Damage;
    public int tpCost = 30;
    [Tooltip("Damage: 공격력에 곱하는 배율 / Heal: 회복량(고정)")]
    public float power = 2f;
    [Tooltip("Damage 스킬일 때 타이밍 타격을 하는지")]
    public bool useTiming = true;

    public BattleSkill() { }

    public BattleSkill(string name, SkillKind kind, int tpCost, float power, bool useTiming)
    {
        this.name = name;
        this.kind = kind;
        this.tpCost = tpCost;
        this.power = power;
        this.useTiming = useTiming;
    }
}

[Serializable]
public class BattleActor
{
    public string name = "아군";
    public int maxHP = 80;
    public int attack = 10;
    public Color color = Color.white;
    [Tooltip("상태창 왼쪽에 보이는 얼굴 이미지(Sprite). 비우면 캐릭터 색의 네모가 대신 보인다")]
    public Sprite face;
    [Tooltip("MP 최대치. 지금은 표시만 하는 자리표시용이다 (MP 시스템은 나중에 별도로 만들 예정)")]
    public int maxMP = 30;
    public List<BattleSkill> skills = new List<BattleSkill>();

    [NonSerialized] public int hp;
    [NonSerialized] public int mp;
    [NonSerialized] public bool guarding;

    public bool IsAlive => hp > 0;
}

[Serializable]
public class BattleEnemy
{
    public string name = "적";
    public int maxHP = 160;
    public Color color = new Color(0.8f, 0.3f, 0.4f);
    [TextArea(2, 4)]
    [Tooltip("플레이어 턴이 시작될 때 위쪽 패널에 보이는 적 설명. {enemy} 는 적 이름으로 바뀌고, 줄바꿈은 \\n 또는 <br>. 비우면 아무것도 안 보인다")]
    public string description = "{enemy}이(가) 낯선 기운을 풍기고 있다.";
    public List<AttackPattern> patterns = new List<AttackPattern>();

    [NonSerialized] public int hp;

    public bool IsAlive => hp > 0;
}

/// <summary>전투 전체 수치. 밸런스 조절은 전부 여기서.</summary>
[Serializable]
public class BattleSettings
{
    [Header("칸 배치 (나중에 2x2, 5x3 등으로 자유롭게 변경)")]
    public int columns = 3;
    public int rows = 2;
    [Tooltip("한 칸의 크기(픽셀). 칸이 많아 아레나가 너무 커지면 자동으로 줄어든다")]
    public float cellSize = 140f;
    public float arenaMaxWidth = 900f;
    public float arenaMaxHeight = 520f;

    [Header("이동 (타자로 칸을 지정하면 그 칸을 향해 직선 이동)")]
    [Tooltip("픽셀/초. 낮을수록 이동 중에 맞을 위험이 커진다. 칸 크기 140 기준 380이면 한 칸 이동에 약 0.37초")]
    public float soulSpeed = 380f;
    public float soulHitRadius = 10f;
    public float bulletRadius = 16f;
    [Tooltip("피격 후 무적 시간(초)")]
    public float invincibleSeconds = 0.8f;

    [Header("적 턴")]
    public float enemyTurnSeconds = 10f;
    public float patternGapSeconds = 0.8f;
    [Tooltip("공격 경고 색. 이 색의 투명도(A)는 무시된다. 깜빡임 불투명도는 아래 Min/Max 로 정한다")]
    public Color warningColor = Color.white;
    [Tooltip("경고가 깜빡일 때 가장 옅은 순간의 불투명도 (0 = 투명, 1 = 완전 불투명)")]
    [Range(0f, 1f)] public float warningAlphaMin = 0.22f;
    [Tooltip("경고가 깜빡일 때 가장 진한 순간의 불투명도 (0 = 투명, 1 = 완전 불투명)")]
    [Range(0f, 1f)] public float warningAlphaMax = 0.42f;
    [Tooltip("경고가 1초에 몇 번 깜빡이는지 (옅음→진함→옅음 = 1번). 클수록 빠르게 깜빡임")]
    [Range(0.5f, 20f)] public float warningBlinkPerSecond = 4.5f;
    [Tooltip("모든 탄 속도에 곱하는 배율. 1 = 패턴에 적은 속도 그대로, 낮을수록 느림")]
    [Range(0.2f, 2f)] public float bulletSpeedScale = 0.7f;
    [Tooltip("발사 템포 배율. 1 = 패턴에 적은 간격 그대로, 낮을수록 천천히(간격이 1/배율 배로 늘어남). 경고 시간은 그대로")]
    [Range(0.2f, 2f)] public float patternTempoScale = 0.8f;
    public HitTargetMode hitTarget = HitTargetMode.RandomAlive;
    [Range(0f, 1f)] public float guardDamageRate = 0.5f;

    [Header("TP / 서서 치기")]
    public int tpMax = 100;
    public int tpPerMove = 6;
    [Tooltip("적 턴 한 번에 이동으로 얻을 수 있는 TP 상한. 0이면 제한 없음")]
    public int maxTpGainPerEnemyTurn = 0;
    [Tooltip("서 있는 칸의 단어를 쳤을 때 적에게 주는 추가 데미지")]
    public int standDamage = 6;

    [Header("타이밍 타격")]
    public float timingSweepsPerSecond = 0.9f;
    public float timingTimeoutSeconds = 3f;
    public float perfectMultiplier = 2f;
    public float goodMultiplier = 1.5f;
    public float normalMultiplier = 1f;
    public float missMultiplier = 0.5f;

    [Header("단어")]
    [Tooltip("마지막 글자가 빠진 입력(예: '하늘' 대신 '하')도 인정할지. 한글 IME 조합 중인 글자는 BattleUI가 따로 읽어서 합치므로 보통은 꺼둔다. 후보가 하나일 때만 인정")]
    public bool allowIncompleteLastChar = false;
    public int startTier = 0;
    [Tooltip("몇 라운드마다 단어 단계가 올라가는지")]
    public int roundsPerTier = 2;

    [Header("타자 입력 피드백 (입력칸 번쩍임)")]
    [Tooltip("단어를 맞게 쳤을 때 입력칸이 번쩍이는 색 (A 값은 무시, 아래 Alpha Max/Min 사용)")]
    public Color inputCorrectColor = new Color(0.1f, 0.5f, 0.2f, 1f);
    [Tooltip("틀리게 쳤을 때 입력칸이 번쩍이는 색 (A 값은 무시)")]
    public Color inputWrongColor = new Color(0.6f, 0.1f, 0.1f, 1f);
    [Tooltip("번쩍임이 시작될 때(가장 진할 때)의 불투명도")]
    [Range(0f, 1f)] public float inputFlashAlphaMax = 0.9f;
    [Tooltip("번쩍임이 끝날 때(가장 옅을 때)의 불투명도. 최대 → 최소로 서서히 옅어진다")]
    [Range(0f, 1f)] public float inputFlashAlphaMin = 0.2f;
    [Tooltip("번쩍임이 지속되는 시간(초)")]
    [Min(0.05f)] public float inputFlashSeconds = 0.25f;
    [Header("칸 수 변경 연출")]
    [Tooltip("라운드가 바뀌어 행/열 수가 달라질 때, 납작한 격자가 새 크기로 늘어나거나 줄어드는 시간(초)")]
    [Min(0.05f)] public float gridMorphSeconds = 0.35f;

    [Header("플레이어(커서) 색")]
    [Tooltip("평소 플레이어(커서)의 색")]
    public Color soulColor = new Color(1f, 0.25f, 0.3f, 1f);
    [Tooltip("지금 서 있는 칸의 단어를 쳐서 적을 공격했을 때 플레이어가 물드는 색")]
    public Color soulAttackColor = new Color(1f, 0.82f, 0.3f, 1f);
    [Tooltip("다른 칸의 단어를 쳐서 이동(TP 획득)했을 때 플레이어가 물드는 색")]
    public Color soulMoveColor = new Color(0.45f, 0.95f, 0.6f, 1f);
    [Tooltip("물들었다가 원래 색으로 돌아오는 데 걸리는 시간(초)")]
    [Min(0.05f)] public float soulTintSeconds = 0.3f;

    [Header("말풍선 대사 (플레이어 턴 행동 중 / 적 반응)")]
    [Tooltip("Assets/Prefabs/Canvas_SpeechBalloon 프리팹을 끌어다 넣는다. 비우면 말풍선 대사는 나오지 않는다")]
    public GameObject speechBalloonPrefab;
    [Tooltip("글자가 다 나온 뒤 말풍선이 머무는 시간(초)")]
    public float balloonHoldSeconds = 0.8f;
    [Tooltip("적 말풍선 위치(픽셀): 적 그림 중심 기준. X=0 이면 꼬리가 적 중심 바로 위를 가리킨다. Y는 높이(클수록 위)")]
    public Vector2 enemyBalloonPos = new Vector2(0f, 175f);
    [Tooltip("아군 말풍선 위치(픽셀): 아군 얼굴 중심 기준. X=0 이면 꼬리가 얼굴 중심 바로 위를 가리킨다. X를 키우면 왼쪽 아군은 오른쪽으로, 오른쪽 아군은 왼쪽으로(화면 가운데 쪽) 밀린다")]
    public Vector2 partyBalloonPos = new Vector2(0f, 190f);
    [Tooltip("켜면 적 턴 시작 대사 때 아군 말풍선의 꼬리를 숨긴다 (이때는 아군이 화면 가장자리에 있어 꼬리가 얼굴을 못 가리키므로)")]
    public bool hidePartyTailAtEnemyTurnStart = true;

    [Header("칸 표시")]
    [Tooltip("플레이어가 서 있는 칸의 색. 이 색의 A 값이 그대로 불투명도가 된다 (0 = 투명, 255 = 불투명)")]
    public Color currentCellColor = new Color(0.22f, 0.25f, 0.40f, 0.95f);
    [Tooltip("칸 뒤에 깔리는 바탕색. 칸 색의 A 를 낮추면 이 색이 비쳐 보인다. 어둡게 하려면 어두운 색(기본값)으로 둔다")]
    public Color cellBackColor = new Color(0.04f, 0.04f, 0.08f, 1f);

    [Header("상단 상태 패널 (적 HP / 라운드·TP 뒤 바탕)")]
    [Tooltip("두 패널의 바탕색. A 값이 불투명도. 이미지를 넣었다면 이 색이 이미지에 곱해지므로 원본 그대로 쓰려면 흰색(A 255)으로 둔다")]
    public Color hudPanelColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
    [Tooltip("왼쪽 위 적 이름/HP 패널 이미지(Sprite). 비우면 위 색의 단색 바탕. 9-slice(Sprite Editor 의 Border)를 설정하면 늘려도 모서리가 안 깨진다")]
    public Sprite enemyPanelSprite;
    [Tooltip("오른쪽 위 라운드/TP 패널 이미지(Sprite). 비우면 단색 바탕")]
    public Sprite infoPanelSprite;

    [Header("안내 메시지 글자 (\"○○이(가) 나타났다!\" 등)")]
    [Tooltip("메시지 글자에 아웃라인을 쓸지")]
    public bool messageOutline = true;
    [Tooltip("아웃라인 두께(픽셀). 글자 바깥쪽으로만 생긴다. 보통 2 ~ 5")]
    [Range(0f, 12f)] public float messageOutlineThickness = 3f;
    [Tooltip("아웃라인 색")]
    public Color messageOutlineColor = Color.black;

    [Header("안내 텍스트 크기 (메뉴창 안의 안내 문구는 해당 없음)")]
    [Tooltip("안내 메시지(위쪽 패널 / 입력창 위)의 글자 크기. 문구가 길거나 두 줄이면 상자에 맞게 자동으로 줄어든다")]
    public float messageFontSize = 36f;
    [Tooltip("켜면 문구가 길 때 글자가 자동으로 작아진다(그래서 턴마다 크기가 달라 보일 수 있음). 끄면 항상 위 크기로 고정되고, 길면 상자 폭에서 줄이 바뀐다")]
    public bool messageAutoShrink = false;
    [Tooltip("격자 아래 힌트 문구의 글자 크기")]
    public float hintFontSize = 26f;

    [Header("턴 전환 시 UI 이동 (플레이어 턴 ↔ 적 턴)")]
    [Tooltip("턴 배너가 끝난 뒤 UI들이 선형으로 이동하는 시간(초)")]
    public float layoutTransitionSeconds = 0.3f;
    [Tooltip("UI 이동이 모두 끝난 뒤 적 공격이 시작되기까지의 텀(초)")]
    public float enemyAttackDelaySeconds = 0.5f;
    [Tooltip("플레이어 턴에 칸 판이 세로로 납작해졌을 때의 높이 비율 (1 = 원래 높이). 이때 칸 안의 글자는 숨겨진다")]
    [Range(0.1f, 1f)] public float flatGridScaleY = 0.38f;
    [Tooltip("행동 메뉴창이 화면 옆에서 미끄러져 들어오는 시간(초)")]
    public float menuSlideSeconds = 0.25f;

    [Header("상태창 (상태이상 칸 / MP / Enter 버튼)")]
    [Tooltip("상태이상(속성) 칸 한 개의 크기(픽셀). 적/아군 모두 같은 크기")]
    public float statusIconPx = 64f;
    [Tooltip("상태이상 칸 개수 (적/아군 공통). 빈 네모가 이 개수만큼 항상 보이고, 상태가 생기면 안에 아이콘이 뜬다")]
    [Range(1, 8)] public int statusSlotCount = 5;
    [Tooltip("빈 상태이상 칸의 색 (A 값이 불투명도)")]
    public Color statusSlotColor = new Color(0f, 0f, 0f, 0.45f);
    [Tooltip("빈 상태이상 칸 이미지(Sprite). 비우면 위 색의 단색 네모")]
    public Sprite statusSlotSprite;
    [Tooltip("방어 상태 아이콘 이미지(Sprite). 비우면 색 네모 + '방' 글자")]
    public Sprite guardIcon;
    [Tooltip("방어 상태 칸의 색 (이미지를 넣었다면 이미지에 곱해진다)")]
    public Color guardIconColor = new Color(0.4f, 0.65f, 1f, 1f);
    [Tooltip("MP 바 색 (MP 시스템은 나중에. 지금은 가득 찬 상태로만 보인다)")]
    public Color mpBarColor = new Color(0.45f, 0.55f, 1f, 1f);
    [Tooltip("타자 입력창 옆 Enter 버튼 이미지(Sprite). 비우면 단색 네모")]
    public Sprite enterButtonSprite;
    [Tooltip("Enter 버튼 색 (이미지를 넣었다면 이미지에 곱해진다)")]
    public Color enterButtonColor = new Color(0.25f, 0.3f, 0.5f, 1f);
}

/// <summary>기본 데이터. 인스펙터에서 전부 덮어쓸 수 있다.</summary>
public static class BattleDefaults
{
    public static List<WordTier> WordTiers()
    {
        return new List<WordTier>
        {
            new WordTier("자음", Split("ㄱ ㄴ ㄷ ㄹ ㅁ ㅂ ㅅ ㅇ ㅈ ㅊ ㅋ ㅌ ㅍ ㅎ ㄲ ㄸ ㅃ ㅆ ㅉ")),
            new WordTier("자음·모음", Split("ㄱ ㄴ ㄷ ㄹ ㅁ ㅂ ㅅ ㅇ ㅈ ㅊ ㅋ ㅌ ㅍ ㅎ ㅏ ㅑ ㅓ ㅕ ㅗ ㅛ ㅜ ㅠ ㅡ ㅣ")),
            new WordTier("한 글자", Split("가 나 다 라 마 바 사 아 자 차 카 타 파 하 거 너 더 러 머 버 서 어 저 처 커 터 퍼 허 고 노 도 로 모 보 소 오 조 초 코 토 포 호 구 누 두 루 무 부 수 우 주 추 쿠 투 푸 후")),
            new WordTier("두 글자", Split("공격 방어 회복 하늘 바다 구름 별빛 달빛 노을 바람 거울 시계 열쇠 기억 약속 마음 노래 소리 얼음 불꽃 번개 폭풍 햇살 나무 꽃잎 하루 어제 내일 우주 은하 행성 신호 연결 안녕 친구 사랑 여름 겨울 가을 새벽 정원 다리 계단 지도 편지 나침반")),
            new WordTier("긴 단어", Split("도서관 놀이터 발자국 그림자 무지개 눈사람 별자리 비밀번호 지하철 자동차 컴퓨터 키보드 모니터 스피커 시간표 사냥꾼 마법사 탐험가 우체국 편의점 운동장 한밤중 이야기 고양이 강아지 도시락 전화기 손전등 우산꽂이 가로등 신호등 놀이공원 도메네뷸라 네뷸라시티 에덴의씨앗")),
        };
    }

    public static List<BattleActor> Party()
    {
        return new List<BattleActor>
        {
            new BattleActor
            {
                name = "하율", maxHP = 80, attack = 12, color = new Color(1f, 0.6f, 0.7f),
                skills = new List<BattleSkill> { new BattleSkill("강타", SkillKind.Damage, 30, 2.2f, true) }
            },
            new BattleActor
            {
                name = "라임", maxHP = 70, attack = 9, color = new Color(0.6f, 1f, 0.5f),
                skills = new List<BattleSkill> { new BattleSkill("치유", SkillKind.Heal, 40, 35f, false) }
            },
        };
    }

    public static List<RoundLines> DefaultRoundLines()
    {
        return new List<RoundLines>
        {
            new RoundLines
            {
                fromRound = 1,
                party = new List<ActorLine>
                {
                    new ActorLine { actorName = "하율", turnStart = "좋아, 이제 내 차례야!", enemyTurnStart = "조심해!", waiting = "어떻게 할까?", attack = "내 공격을 받아라!", skill = "{skill}!", guard = "버텨 볼게!" },
                    new ActorLine { actorName = "라임", turnStart = "힘내 보자!", enemyTurnStart = "피해야 해!", waiting = "음… 뭘 하지?", attack = "에잇!", skill = "{skill}~!", guard = "조심해야지." },
                },
                enemyReaction = "으윽, 너무 강하잖아!",
                enemyAtPlayerTurnStart = "",
                enemyAtEnemyTurnStart = "이번엔 내 차례다!",
            },
        };
    }

    public static BattleEnemy Enemy()
    {
        return new BattleEnemy { name = "시험용 적", maxHP = 160, patterns = Patterns() };
    }

    public static List<AttackPattern> Patterns()
    {
        var list = new List<AttackPattern>();

        // 위에서 비처럼
        var rain = new AttackPattern("비");
        for (int i = 0; i < 6; i++)
            rain.steps.Add(new PatternStep(i * 0.8f, AttackSide.Top, LaneSelect.Random, 0.6f, 420f, 8));
        list.Add(rain);

        // 좌우에서 번갈아
        var sides = new AttackPattern("양옆");
        for (int i = 0; i < 6; i++)
            sides.steps.Add(new PatternStep(i * 0.9f, i % 2 == 0 ? AttackSide.Left : AttackSide.Right,
                                            LaneSelect.Random, 0.6f, 460f, 8, BattleElement.Wind));
        list.Add(sides);

        // 조준탄: 플레이어가 서 있는 줄을 노린다
        var aimed = new AttackPattern("조준");
        aimed.steps.Add(new PatternStep(0f, AttackSide.Top, LaneSelect.AtPlayer, 0.9f, 500f, 10, BattleElement.Fire));
        aimed.steps.Add(new PatternStep(1.0f, AttackSide.Left, LaneSelect.AtPlayer, 0.9f, 500f, 10, BattleElement.Fire));
        aimed.steps.Add(new PatternStep(2.0f, AttackSide.Bottom, LaneSelect.AtPlayer, 0.9f, 500f, 10, BattleElement.Fire));
        aimed.steps.Add(new PatternStep(3.0f, AttackSide.Right, LaneSelect.AtPlayer, 0.9f, 500f, 10, BattleElement.Fire));
        list.Add(aimed);

        // 벽: 한 줄만 비우고 전부 쏜다
        var wall = new AttackPattern("벽");
        wall.steps.Add(new PatternStep(0f, AttackSide.Bottom, LaneSelect.AllButOne, 1.0f, 380f, 10, BattleElement.Water));
        wall.steps.Add(new PatternStep(2.2f, AttackSide.Right, LaneSelect.AllButOne, 1.0f, 380f, 10, BattleElement.Water));
        list.Add(wall);

        return list;
    }

    private static string[] Split(string spaced)
    {
        return spaced.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }
}

/// <summary>한 아군의 말풍선 대사. {actor} {skill} {enemy} 를 쓸 수 있다. 비우면 그 행동엔 대사 없음.</summary>
[Serializable]
public class ActorLine
{
    [Tooltip("아군 이름 (Party 의 Name 과 같아야 한다)")] public string actorName = "";
    [TextArea(1, 3)] [Tooltip("플레이어 턴 시작 대사: UI 이동이 끝난 뒤, 행동 메뉴가 열리기 전에 잠깐 뜬다 (다른 아군/적 대사와 동시에)")] public string turnStart = "";
    [TextArea(1, 3)] [Tooltip("적 턴 시작 대사: UI 이동이 끝난 뒤, 적 공격이 시작되기 전에 잠깐 뜬다 (적 대사와 동시에)")] public string enemyTurnStart = "";
    [TextArea(1, 3)] [Tooltip("이 아군이 행동을 고르는 중(메뉴가 열려 있는 동안) 계속 떠 있는 대사")] public string waiting = "";
    [TextArea(1, 3)] [Tooltip("공격 선택 시 대사")] public string attack = "";
    [TextArea(1, 3)] [Tooltip("스킬 사용 시 대사 ({skill} = 스킬 이름)")] public string skill = "";
    [TextArea(1, 3)] [Tooltip("방어 선택 시 대사")] public string guard = "";
}

/// <summary>
/// "이 라운드부터 이 말풍선 대사들을 쓴다". 가장 큰 fromRound(현재 라운드 이하)가 적용된다.
/// 라운드가 바뀔 때마다 새 항목을 추가해서 대사를 바꾼다.
/// </summary>
[Serializable]
public class RoundLines
{
    [Tooltip("이 라운드부터 적용 (1부터). 다음 항목이 나올 때까지 계속 쓰인다")] public int fromRound = 1;
    [Tooltip("플레이어 턴 행동 중 아군 대사")] public List<ActorLine> party = new List<ActorLine>();
    [TextArea(1, 3)] [Tooltip("플레이어 턴이 끝난 뒤 적이 하는 반응 대사 (적 턴 배너 직전). {enemy} 사용 가능")]
    public string enemyReaction = "";
    [TextArea(1, 3)] [Tooltip("플레이어 턴 시작 때 적도 함께 하는 대사 (아군의 Turn Start 대사와 동시에 뜨고, 그동안 적 이름/HP 바가 숨겨진다)")]
    public string enemyAtPlayerTurnStart = "";
    [TextArea(1, 3)] [Tooltip("적 턴 시작 때 적이 하는 대사 (아군의 Enemy Turn Start 대사와 동시에 뜬다)")]
    public string enemyAtEnemyTurnStart = "";
}

/// <summary>"이 라운드부터 이 플레이어 턴 안내 문구를 쓴다". 가장 큰 fromRound(현재 라운드 이하)가 적용된다.</summary>
[Serializable]
public class RoundMessage
{
    [Tooltip("이 라운드부터 적용 (1부터). 다음 항목이 나올 때까지 계속 쓰인다")] public int fromRound = 1;
    [TextArea(2, 4)]
    [Tooltip("{enemy} 는 적 이름. 줄바꿈은 \\n 또는 <br>. 비우면 아무것도 안 보인다")]
    public string text = "";
}

/// <summary>"이 라운드부터 이 칸 배치를 쓴다". 가장 큰 fromRound(현재 라운드 이하)가 적용된다.</summary>
[Serializable]
public class RoundLayout
{
    [Tooltip("이 라운드부터 적용 (1부터)")] public int fromRound = 1;
    public int columns = 3;
    public int rows = 2;

    public RoundLayout() { }
    public RoundLayout(int fromRound, int columns, int rows)
    {
        this.fromRound = fromRound; this.columns = columns; this.rows = rows;
    }
}

/// <summary>턴 전환 연출 하나(플레이어 턴 또는 적 턴)의 내용. 이미지를 넣으면 글자 대신 이미지가 나온다.</summary>
[Serializable]
public class TurnBannerEntry
{
    [Tooltip("이미지가 비어 있을 때 보여줄 글자")] public string text = "";
    [Tooltip("글자 색 + 위아래 띠 선 색")] public Color color = Color.white;
    [Tooltip("나중에 만들 연출 이미지(Sprite). 넣으면 글자 대신 이 이미지가 나온다")] public Sprite image;

    public TurnBannerEntry() { }
    public TurnBannerEntry(string text, Color color) { this.text = text; this.color = color; }
}

/// <summary>"플레이어 턴!" / "적 턴!" 전환 연출 설정. 전부 인스펙터에서 바꿀 수 있다.</summary>
[Serializable]
public class TurnBannerSettings
{
    [Tooltip("끄면 연출 없이 바로 넘어간다")] public bool enabled = true;
    public TurnBannerEntry playerTurn = new TurnBannerEntry("플레이어 턴!", new Color(0.45f, 0.8f, 1f));
    public TurnBannerEntry enemyTurn = new TurnBannerEntry("적 턴!", new Color(1f, 0.4f, 0.4f));

    [Header("시간 (초)")]
    [Min(0f)] public float slideInSeconds = 0.25f;
    [Min(0f)] public float holdSeconds = 0.7f;
    [Min(0f)] public float slideOutSeconds = 0.25f;

    [Header("모양")]
    [Tooltip("뒤에 깔리는 띠의 높이")] public float bandHeight = 200f;
    [Tooltip("뒤에 깔리는 띠의 색 (A = 어둡기)")] public Color bandColor = new Color(0f, 0f, 0f, 0.75f);
    [Tooltip("글자 크기")] public float fontSize = 96f;
    [Tooltip("이미지를 넣었을 때 표시 크기 (비율은 유지)")] public Vector2 imageSize = new Vector2(640f, 180f);
}

/// <summary>
/// 화면 위쪽 안내 문구 모음. 인스펙터에서 자유롭게 바꾼다.
/// 문구 안에 아래 {낱말}을 쓰면 상황에 맞게 바뀐다 (쓸 수 있는 것은 문구마다 툴팁에 적혀 있다):
///   {enemy} 적 이름   {actor} 행동하는 파티원   {skill} 스킬 이름   {action} 행동 이름
///   {target} 대상   {amount} 회복량   {damage} 피해량   {grade} 타이밍 등급(예: "Perfect! ", 없으면 빈칸)
/// </summary>
[Serializable]
public class BattleMessages
{
    [Tooltip("전투 시작.  {enemy}")]
    public string encounter = "{enemy}이(가) 나타났다!";
    [Tooltip("적 턴 시작 (단어를 치는 단계).  {enemy}")]
    public string enemyTurnWord = "{enemy}의 공격!  단어를 쳐서 피하세요  금색 = 서 있는 칸 = 추가 공격";
    [Tooltip("적 턴 시작 (키 하나만 누르는 자리연습 단계).  {enemy}")]
    public string enemyTurnKey = "{enemy}의 공격!  키를 눌러 피하세요 (Enter 필요 없음)  금색 = 서 있는 칸 = 추가 공격";
    [Tooltip("파티원이 방어를 골랐을 때.  {actor}")]
    public string guard = "{actor}은(는) 방어 자세를 취했다.";
    [Tooltip("TP 가 모자라 스킬을 못 쓸 때")]
    public string notEnoughTp = "TP가 부족하다!";
    [Tooltip("스킬을 쓸 때.  {actor} {skill}")]
    public string skillUse = "{actor}의 {skill}!";
    [Tooltip("공격 등 일반 행동을 할 때.  {actor} {action}")]
    public string actionUse = "{actor}의 {action}!";
    [Tooltip("회복 스킬 결과.  {target} {amount}")]
    public string heal = "{target}의 HP가 {amount} 회복되었다.";
    [Tooltip("적에게 피해를 줬을 때.  {grade} {enemy} {damage}")]
    public string damageDealt = "{grade}{enemy}에게 {damage}의 데미지!";
    [Tooltip("승리했을 때.  {enemy}")]
    public string victory = "{enemy}을(를) 쓰러뜨렸다!";
    [Tooltip("패배했을 때")]
    public string defeat = "파티가 전멸했다...";
    [Tooltip("고를 수 없는 메뉴 항목을 골랐을 때")]
    public string notAvailable = "지금은 고를 수 없습니다.";

    public static string Fill(string template, string enemy = "", string actor = "", string skill = "", string action = "",
                              string target = "", string grade = "", int amount = 0, int damage = 0)
    {
        if (string.IsNullOrEmpty(template)) return "";
        return template.Replace("{enemy}", enemy).Replace("{actor}", actor).Replace("{skill}", skill)
                       .Replace("{action}", action).Replace("{target}", target).Replace("{grade}", grade)
                       .Replace("{amount}", amount.ToString()).Replace("{damage}", damage.ToString());
    }
}
