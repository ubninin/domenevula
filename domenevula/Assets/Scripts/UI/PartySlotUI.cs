using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 메뉴 상태창의 파티원 한 명 슬롯. 평소엔 SlotBarBg가 화면 왼쪽 바깥에 걸쳐서
/// 얼굴(SlotPortrait)만 보이고, 커서가 이 슬롯을 가리키면 슬롯 전체(PartySlot 자기 자신)를
/// 오른쪽으로 슬라이드시켜 HP/MP 바가 들어갈 영역(SlotBarBg)까지 화면 안으로 들여온다.
///
/// 슬라이드는 코루틴이 아니라 Update에서 매 프레임 목표 위치로 다가가는 방식이다.
/// 상태창이 꺼졌다 켜져도(OnEnable) 항상 원위치(0)에서 시작해서, 다시 열 때마다 등장 애니메이션이 재생된다.
///
/// 붙이는 곳: PartySlot 프리팹 (PartySlot1~3 인스턴스 각각).
/// 움직이는 대상은 이 오브젝트 자신의 RectTransform이다 (SlotBarBg만이 아니라 얼굴 포함 전체).
/// selectedOffsetX 값은 Play 모드에서 눈으로 보면서 SlotBarBg가 화면 안으로 적당히 들어오도록 맞추면 됨.
/// </summary>
public class PartySlotUI : MonoBehaviour
{
    private RectTransform slotBarBg; // 움직일 대상 = 이 슬롯 자신 (이름은 예전 그대로)
    [Tooltip("선택됐을 때 슬롯 전체를 기존 위치에서 오른쪽으로 얼마나 밀지 (anchoredPosition.x 기준)")]
    [SerializeField] private float selectedOffsetX = 700f;
    [Tooltip("슬라이드 속도(Lerp 계수). 클수록 빨리 도착함")]
    [SerializeField] private float slideSpeed = 10f;

    private bool initialized;
    private float restX;
    private float currentOffset; // 원위치 기준 현재 밀린 정도
    private float targetOffset;  // 가야 할 정도 (선택 시 selectedOffsetX, 아니면 0)

    /// <summary>슬라이드로 밀린 정도를 뺀, 원래(평소) 자리의 월드 좌표. 커서 이동 방향 계산용.</summary>
    public Vector2 RestWorldPosition
    {
        get
        {
            Transform p = transform.parent != null ? transform.parent : transform;
            return (Vector2)(transform.position - p.TransformVector(new Vector3(currentOffset, 0f, 0f)));
        }
    }

    private void Init()
    {
        if (initialized) return;
        slotBarBg = transform as RectTransform;
        if (slotBarBg == null)
        {
            Debug.LogWarning($"PartySlotUI({name}): RectTransform이 아닙니다. UI 오브젝트에 붙여주세요.");
            return;
        }
        restX = slotBarBg.anchoredPosition.x;
        initialized = true;
    }

    // 켜질 때마다 항상 원위치에서 시작 → 선택되면 거기서부터 슬라이드 인
    private void OnEnable()
    {
        Init();
        currentOffset = 0f;
        targetOffset = 0f;
        Apply();
    }

    private void OnDisable()
    {
        currentOffset = 0f;
        targetOffset = 0f;
        Apply();
    }

    // ───────────── HP/MP 게이지 (전투 상태창과 같은 모양, SlotBarBg 중앙에 자동 생성) ─────────────
    // 게이지는 SlotBarBg의 자식이라 슬롯이 슬라이드할 때 함께 움직인다.

    [Header("HP/MP 게이지")]
    [Tooltip("이 슬롯이 보여줄 전투 파티원 이름(하율/라임 등, BattleActor.name). 비우면 슬롯 순서대로 기본 파티(BattleDefaults.Party)에서 가져옴")]
    [SerializeField] private string actorName;

    private RectTransform hpFill, mpFill;
    private TextMeshProUGUI nameText, hpText, mpText;
    private static TMP_FontAsset gaugeFont;

    /// <summary>HP/MP 값을 현재 상태로 갱신한다 (상태창을 열 때마다 호출됨). slotIndex = 비워둔 actorName 대신 쓸 순서.</summary>
    public void Refresh(int slotIndex)
    {
        BuildGauges();
        if (hpFill == null) return;

        List<BattleActor> party = BattleDefaults.Party();
        BattleActor a = null;
        if (!string.IsNullOrEmpty(actorName)) a = party.Find(x => x.name == actorName);
        else if (slotIndex >= 0 && slotIndex < party.Count) a = party[slotIndex];

        if (a == null)
        {
            nameText.text = "";
            hpText.text = "";
            mpText.text = "";
            BattleUIFactory.SetBarRatio(hpFill, 0f);
            BattleUIFactory.SetBarRatio(mpFill, 0f);
            return;
        }

        int hp = BattleLauncher.GetSavedHp(a.name, a.maxHP);
        nameText.text = a.name;
        nameText.color = a.color;
        hpText.text = $"{hp}/{a.maxHP}";
        BattleUIFactory.SetBarRatio(hpFill, a.maxHP > 0 ? (float)hp / a.maxHP : 0f);
        // MP 시스템은 아직 없어서 전투 때와 마찬가지로 가득 찬 상태로 보여준다.
        mpText.text = a.maxMP > 0 ? $"MP {a.maxMP}/{a.maxMP}" : "";
        BattleUIFactory.SetBarRatio(mpFill, a.maxMP > 0 ? 1f : 0f);
    }

    private void BuildGauges()
    {
        if (hpFill != null) return;
        Transform bg = transform.Find("SlotBarBg");
        if (bg == null)
        {
            Debug.LogWarning($"PartySlotUI({name}): SlotBarBg를 찾지 못해 HP/MP 게이지를 만들지 못했습니다.");
            return;
        }

        if (gaugeFont == null)
        {
            TMP_Text any = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
            gaugeFont = any != null ? any.font : TMP_Settings.defaultFontAsset;
        }

        const float w = 400f;
        // 맨 앞(index 0)에 둬서 SlotBarFrame(테두리)이 게이지 위에 그려지게 한다.
        RectTransform box = BattleUIFactory.MakeRect("StatusGauges", bg, Vector2.zero, new Vector2(w, 140f));
        box.SetSiblingIndex(0);

        nameText = BattleUIFactory.MakeText("Name", box, new Vector2(-w * 0.5f + 100f, 52f), new Vector2(200f, 36f),
                                            "", 28f, Color.white, gaugeFont, TextAlignmentOptions.Left);
        hpText = BattleUIFactory.MakeText("HpText", box, new Vector2(w * 0.5f - 80f, 52f), new Vector2(160f, 32f),
                                          "", 24f, Color.white, gaugeFont, TextAlignmentOptions.Right);
        hpFill = BattleUIFactory.MakeBar("HpBar", box, new Vector2(0f, 14f), new Vector2(w, 22f),
                                         new Color(0.05f, 0.12f, 0.06f), new Color(0.35f, 0.9f, 0.4f));
        mpFill = BattleUIFactory.MakeBar("MpBar", box, new Vector2(0f, -22f), new Vector2(w, 18f),
                                         new Color(0.05f, 0.06f, 0.14f), new Color(0.45f, 0.55f, 1f));
        mpText = BattleUIFactory.MakeText("MpText", box, new Vector2(0f, -22f), new Vector2(w, 18f),
                                          "", 14f, Color.white, gaugeFont);
    }

    /// <summary>이 슬롯이 지금 커서에 선택된 상태인지 켜고 끈다.</summary>
    public void SetSelected(bool selected)
    {
        Init();
        targetOffset = selected ? selectedOffsetX : 0f;
    }

    private void Update()
    {
        if (!initialized || Mathf.Approximately(currentOffset, targetOffset)) return;

        currentOffset = Mathf.Lerp(currentOffset, targetOffset, Time.unscaledDeltaTime * slideSpeed);
        if (Mathf.Abs(currentOffset - targetOffset) < 0.5f) currentOffset = targetOffset;
        Apply();
    }

    private void Apply()
    {
        if (!initialized || slotBarBg == null) return;
        Vector2 pos = slotBarBg.anchoredPosition;
        pos.x = restX + currentOffset;
        slotBarBg.anchoredPosition = pos;
    }
}
