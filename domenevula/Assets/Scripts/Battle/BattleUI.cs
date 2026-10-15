using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 전투 화면의 UI 전부: 상태창(HP/TP), 커맨드 메뉴, 타이밍 바, 타자 입력창, 메시지, 결과창.
/// Canvas_Screen(Screen Space Overlay, 기준 1600x900, Match 0.5)을 코드로 만든다.
/// 입력은 프로젝트의 다른 스크립트처럼 Keyboard.current 를 직접 읽는다 (메뉴/타이밍).
/// 타자 입력만 TMP_InputField (taja와 같은 방식, 한글 IME 지원).
/// </summary>
public class BattleUI : MonoBehaviour
{
    private const int MaxMenuItems = 9;
    private const float TimingBarWidth = 700f;

    /// <summary>타자 입력창에서 Enter를 눌렀을 때 (입력한 문자열).</summary>
    public event Action<string> WordSubmitted;

    public RectTransform CanvasRect { get; private set; }
    /// <summary>아레나를 넣을 자리. 이 아래에 BattleArena 오브젝트를 만든다 (UI 겹침 순서가 맞게).</summary>
    public RectTransform ArenaSlot { get; private set; }
    public Vector2 ArenaCenter => ArenaPosE;
    /// <summary>적 그림의 현재(이동 중이면 그 순간의) 위치.</summary>
    public Vector2 EnemyPosition => enemyCur;

    // ── 화면 배치(스케치 기준, 화면 중앙이 (0,0)): 0 = 플레이어 턴, 1 = 적 턴. 전환 때 이 둘 사이를 선형으로 오간다 ──
    private static readonly Vector2 EnemyPosP = new Vector2(0f, 182f), EnemyPosE = new Vector2(0f, 265f);
    private static readonly Vector2 EnemySize = new Vector2(220f, 125f);
    private static readonly Vector2 ArenaPosP = new Vector2(0f, -22f), ArenaPosE = new Vector2(0f, -12f);
    // 적 이름/HP 패널(위쪽 가운데): 플레이어 턴엔 안내 메시지 줄이 있어서 높고, 적 턴엔 낮다
    private const float EnemyPanelW = 710f, EnemyPanelHP = 120f, EnemyPanelHE = 65f;
    private const float EnemyRowYP = 362f, EnemyRowYE = 418f;
    private static readonly Vector2 MessagePosP = new Vector2(0f, 420f);
    private const float MessageXE = 0f, MessageYEWithInput = -330f, MessageYENoInput = -402f;
    private static readonly Vector2 HintPos = new Vector2(0f, -150f);
    private static readonly Vector2 TypingPos = new Vector2(-67f, -402f);
    private static readonly Vector2 EnterPos = new Vector2(211f, -402f);
    private static readonly Vector2 SlotSize = new Vector2(335f, 130f);
    private const float SlotY = -385f;
    private const float SlotXP = 175f, SlotCenterP = 0f; // 플레이어 턴: 가운데에 붙는다 (중심 -10 기준 ±175)
    private const float SlotXE = 465f;                      // 적 턴: 양쪽 가장자리
    private const float FaceOffsetX = 245f, FaceOffsetY = 30f, FaceSize = 170f; // 얼굴은 상태창 바깥쪽에 따로
    private const float MenuBottomY = -345f;
    private const float EnemyStatusX0 = -745f, EnemyStatusY = 412f; // 적 상태이상 칸: 왼쪽 위, 항상 같은 자리
    private Vector2 enemyCur = EnemyPosP;
    private float layoutT;
    private BattleArena arenaRef;
    private RectTransform messageRoot;
    private GameObject enterButton;
    private bool typingOn;
    private float messageYE = MessageYENoInput;
    private float messageShakeX;
    private RectTransform enemyPanelRt;
    private RectTransform[] enemyRowItems;
    private float[] enemyRowX;
    private Image[] enemySlotIcons;
    private RectTransform faceLayer;

    /// <summary>Choose()의 결과. 취소하면 -1.</summary>
    public int LastChoice { get; private set; }
    /// <summary>RunTiming()의 결과.</summary>
    public TimingGrade LastGrade { get; private set; }

    private TMP_FontAsset font;
    private BattleSettings settings;

    // 상태창
    private class PartySlotUI
    {
        public Image bg;
        public TextMeshProUGUI nameText, hpText, statusText;
        public RectTransform hpFill;
        public RectTransform root;
        public Vector2 basePos;
        public Color baseColor;
        public Coroutine shake;
        public Image face;
        public RectTransform mpFill;
        public TextMeshProUGUI mpText;
        public Image[] slotBgs;    // 상태이상 칸(빈 네모)
        public Image[] slotIcons;  // 칸 안에 뜨는 아이콘 (상태가 있을 때만 켜짐)
        public float halfWidth;
        public float faceSide;
        // 얼굴 이미지 상태
        public BattleActor actor;
        public Sprite defaultSprite;
        public Sprite shownSprite;
        public Color shownTint = new Color(-1f, -1f, -1f, -1f); // 첫 프레임에 반드시 적용되도록
        public bool dead, lowHp, guarding;
        public int holdState = -1, flashState = -1, ailmentState = -1;
        public float flashUntil;
    }
    private static readonly Color CursorColor = new Color(1f, 0.88f, 0.35f, 1f);
    private int activeSlot = -1;

    // 선택 중인 상태창을 따라다니는 노란 테두리 커서 (부드럽게 이동)
    private RectTransform partyFrame;
    private readonly Image[] partyFrameEdges = new Image[4];
    private Vector2 partyFrameCur;
    private float partyFrameAlpha;
    private bool partyFrameShown;

    // 명령 메뉴: 선택 중인 상태창 위에 뜨고, 위치/높이/커서가 부드럽게 움직인다
    private const float MenuWidth = 300f;
    private const float MenuRowHeight = 56f;
    private const float MenuTopPad = 10f;
    private const float MenuHeaderHeight = 44f;
    private const float MenuHintHeight = 30f; // 헤더 바로 아래 안내 문구 줄
    private RectTransform menuRect, menuCursorRt;
    private TextMeshProUGUI menuHint;
    private int menuCount = 3, menuCursorIndex;
    private float menuX, menuBottom, menuH, menuScale = 1f, menuBarY;
    private bool menuPosInit;
    private readonly List<PartySlotUI> slots = new List<PartySlotUI>();
    private TextMeshProUGUI enemyNameText, enemyHpText, roundText, tpText, messageText, hintText;
    private RectTransform enemyHpFill, tpFill, enemyBody;
    // 적 이미지 상태
    private Image enemyImage;
    private BattleEnemy enemyData;
    private Sprite enemyShownSprite;
    private Color enemyShownTint = new Color(-1f, -1f, -1f, -1f);
    private bool enemyDead, enemyLowHp;
    private int enemyHoldState = -1, enemyFlashState = -1, enemyAilmentState = -1;
    private float enemyFlashUntil;

    // 커맨드 메뉴
    private GameObject menuPanel;
    private TextMeshProUGUI menuHeader;
    private readonly TextMeshProUGUI[] menuItems = new TextMeshProUGUI[MaxMenuItems];

    // 타이밍
    private GameObject timingPanel;
    private RectTransform timingMarker;
    private TextMeshProUGUI timingResultText;
    private TextMeshProUGUI timingTitleText;

    // 타자 입력
    private GameObject typingRoot;
    private RectTransform typingRect;
    private TMP_InputField inputField;
    private Image inputBg;
    private TextMeshProUGUI placeholderText;
    private bool instantMode; // true면 Enter 없이 자음/모음 키 하나로 바로 입력

    // 결과
    private GameObject resultPanel;
    private TextMeshProUGUI resultTitle;

    // ───────────────────────── 만들기 ─────────────────────────

    public void Build(TMP_FontAsset fontAsset, BattleSettings battleSettings,
                      IList<BattleActor> party, BattleEnemy enemy)
    {
        font = fontAsset;
        settings = battleSettings;

        var canvasGo = new GameObject("Canvas_Screen", typeof(RectTransform), typeof(Canvas),
                                      typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = battleSettings.canvasSortingOrder; // 필드 씬의 UI 위에 덮이도록
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        CanvasRect = (RectTransform)canvasGo.transform;

        Image backdrop = BattleUIFactory.MakeImage("Backdrop", CanvasRect, Vector2.zero, Vector2.zero,
                                                   new Color(0.04f, 0.04f, 0.08f, 1f));
        BattleUIFactory.Stretch(backdrop.rectTransform);

        bgImage = BattleUIFactory.MakeImage("BackgroundImage", CanvasRect, Vector2.zero, Vector2.zero, Color.white);
        BattleUIFactory.Stretch(bgImage.rectTransform);
        bgFitter = bgImage.gameObject.AddComponent<AspectRatioFitter>();
        bgFitter.enabled = false;
        bgImage.gameObject.SetActive(false);

        BuildEnemyArea(enemy);
        ArenaSlot = BattleUIFactory.MakeRect("ArenaSlot", CanvasRect, Vector2.zero, Vector2.zero);
        BattleUIFactory.Stretch(ArenaSlot);
        BuildTopRight();
        BuildPartySlots(party);
        BuildMessageAndHint();
        BuildTypingField();
        BuildMenu();
        BuildFaces(party); // 메뉴/선택 테두리보다 위에 그려지도록 그 뒤에
        balloonLayer = BattleUIFactory.MakeRect("BalloonLayer", CanvasRect, Vector2.zero, Vector2.zero);
        BattleUIFactory.Stretch(balloonLayer); // 말풍선은 얼굴/메뉴보다도 위
        BuildTiming();
        BuildMagazineView();
        BuildResult();
        BuildTurnBanner(); // 맨 위에 그려지도록 마지막에
    }

    /// <summary>HP/TP 게이지 뒤에 깔리는 바탕 패널. 이미지(sprite)를 넣으면 이미지로, 아니면 단색으로.</summary>
    private Image MakeHudPanel(string name, Vector2 pos, Vector2 size, Sprite sprite)
    {
        Image panel = BattleUIFactory.MakeImage(name, CanvasRect, pos, size, settings.hudPanelColor);
        if (sprite != null)
        {
            panel.sprite = sprite;
            panel.type = Image.Type.Sliced;
        }
        return panel;
    }

    private void BuildEnemyArea(BattleEnemy enemy)
    {
        // 위쪽 가운데 패널: 플레이어 턴엔 [안내 메시지 / 이름+HP], 적 턴엔 [이름+HP]만
        Image panel = MakeHudPanel("EnemyPanel", new Vector2(0f, 450f - EnemyPanelHP * 0.5f), new Vector2(EnemyPanelW, EnemyPanelHP), settings.enemyPanelSprite);
        enemyPanelRt = panel.rectTransform;

        var body = BattleUIFactory.MakeImage("EnemyBody", CanvasRect, EnemyPosP, EnemySize, enemy.color);
        enemyBody = body.rectTransform;
        enemyImage = body;
        enemyData = enemy;
        enemyBody.sizeDelta = settings.enemyImageSize;
        body.preserveAspect = true;

        enemyNameText = BattleUIFactory.MakeText("EnemyName", CanvasRect, new Vector2(-255f, EnemyRowYP), new Vector2(170f, 40f),
                                                 enemy.name, 30f, Color.white, font, TextAlignmentOptions.Left);
        enemyHpFill = BattleUIFactory.MakeBar("EnemyHpBar", CanvasRect, new Vector2(70f, EnemyRowYP), new Vector2(460f, 24f),
                                              new Color(0.15f, 0.05f, 0.08f), new Color(0.9f, 0.25f, 0.35f));
        enemyHpText = BattleUIFactory.MakeText("EnemyHpText", CanvasRect, new Vector2(70f, EnemyRowYP), new Vector2(460f, 24f),
                                               "", 18f, Color.white, font);
        enemyRowItems = new[] { enemyNameText.rectTransform, (RectTransform)enemyHpFill.transform.parent, enemyHpText.rectTransform };
        enemyRowX = new[] { -255f, 70f, 70f };

        // 적 상태이상 칸 (왼쪽 위, 위치 고정). 빈 네모가 항상 보이고, 상태가 생기면 SetEnemyStatus 로 아이콘을 띄운다
        float isz = Mathf.Max(8f, settings.statusIconPx);
        int cnt = Mathf.Clamp(settings.statusSlotCount, 1, 8);
        enemySlotIcons = new Image[cnt];
        for (int k = 0; k < cnt; k++)
        {
            Vector2 p = new Vector2(EnemyStatusX0 + k * (isz + 10f), EnemyStatusY);
            enemySlotIcons[k] = MakeStatusSlot("EnemyStatus" + (k + 1), CanvasRect, p, isz, out _);
        }
    }

    /// <summary>빈 네모(칸) 하나와, 그 안에 뜰 아이콘(처음엔 꺼짐)을 만든다.</summary>
    private Image MakeStatusSlot(string name, Transform parent, Vector2 pos, float size, out Image slotBg)
    {
        slotBg = BattleUIFactory.MakeImage(name, parent, pos, new Vector2(size, size), settings.statusSlotColor, settings.statusSlotSprite);
        if (settings.statusSlotSprite != null) slotBg.type = Image.Type.Sliced;
        Image icon = BattleUIFactory.MakeImage("Icon", slotBg.transform, Vector2.zero, new Vector2(size - 8f, size - 8f), Color.white);
        icon.preserveAspect = true;
        icon.gameObject.SetActive(false);
        return icon;
    }

    /// <summary>적 상태이상 칸 하나에 아이콘을 띄우거나(shown) 비운다 (index 0부터). sprite 가 없으면 color 색 네모.</summary>
    public void SetEnemyStatus(int index, bool shown, Sprite sprite = null, Color? color = null)
    {
        if (enemySlotIcons == null || index < 0 || index >= enemySlotIcons.Length) return;
        Image img = enemySlotIcons[index];
        img.gameObject.SetActive(shown);
        img.sprite = sprite;
        img.color = color ?? Color.white;
    }

    private void BuildTopRight()
    {
        // TP 바 (오른쪽 위). 크기/위치는 스케치 기준
        MakeHudPanel("InfoPanel", new Vector2(578f, 397f), new Vector2(365f, 85f), settings.infoPanelSprite);

        BattleUIFactory.MakeText("TpLabel", CanvasRect, new Vector2(432f, 397f), new Vector2(60f, 30f),
                                 "TP", 26f, new Color(0.5f, 0.9f, 1f), font);
        tpFill = BattleUIFactory.MakeBar("TpBar", CanvasRect, new Vector2(612f, 397f), new Vector2(270f, 26f),
                                         new Color(0.04f, 0.1f, 0.14f), new Color(0.3f, 0.85f, 1f));
        tpText = BattleUIFactory.MakeText("TpText", CanvasRect, new Vector2(612f, 397f), new Vector2(270f, 26f),
                                          "", 18f, Color.white, font);
        roundText = BattleUIFactory.MakeText("RoundText", CanvasRect, new Vector2(660f, 335f), new Vector2(200f, 30f),
                                             "", 22f, new Color(0.85f, 0.9f, 1f), font, TextAlignmentOptions.Right);
    }

    private void BuildPartySlots(IList<BattleActor> party)
    {
        int n = Mathf.Min(party.Count, 2);
        if (party.Count > 2) Debug.LogWarning("[Battle] 스케치 UI는 파티 2명용입니다. 3번째 이후 멤버는 상태창이 표시되지 않습니다.");
        float w = SlotSize.x - 35f;           // 안쪽 바 길이
        float isz = Mathf.Max(8f, settings.statusIconPx);
        for (int i = 0; i < n; i++)
        {
            BattleActor a = party[i];
            float side = i == 0 ? -1f : 1f;
            Vector2 pos = new Vector2(SlotCenterP + side * SlotXP, SlotY);
            Transform parent = BattleUIFactory.MakeRect($"PartySlot{i + 1}", CanvasRect, pos, SlotSize);
            var slot = new PartySlotUI();
            slot.root = (RectTransform)parent;
            slot.basePos = pos;
            slot.halfWidth = SlotSize.x * 0.5f;
            slot.baseColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            slot.bg = parent.gameObject.AddComponent<Image>();
            slot.bg.color = slot.baseColor;
            slot.bg.raycastTarget = false;

            slot.faceSide = side;

            slot.nameText = BattleUIFactory.MakeText("Name", parent, new Vector2(-w * 0.5f + 70f, 30f), new Vector2(140f, 32f),
                                                     a.name, 26f, a.color, font, TextAlignmentOptions.Left);
            slot.hpText = BattleUIFactory.MakeText("HpText", parent, new Vector2(w * 0.5f - 75f, 30f), new Vector2(150f, 30f),
                                                   "", 22f, Color.white, font, TextAlignmentOptions.Right);
            slot.hpFill = BattleUIFactory.MakeBar("HpBar", parent, new Vector2(0f, 2f), new Vector2(w, 18f),
                                                  new Color(0.05f, 0.12f, 0.06f), new Color(0.35f, 0.9f, 0.4f));
            slot.mpFill = BattleUIFactory.MakeBar("MpBar", parent, new Vector2(0f, -24f), new Vector2(w, 14f),
                                                  new Color(0.05f, 0.06f, 0.14f), settings.mpBarColor);
            slot.mpText = BattleUIFactory.MakeText("MpText", parent, new Vector2(0f, -24f), new Vector2(w, 14f),
                                                   "", 12f, Color.white, font);
            slot.statusText = BattleUIFactory.MakeText("Status", parent, new Vector2(w * 0.5f - 60f, -49f), new Vector2(120f, 18f),
                                                       "", 15f, new Color(1f, 0.9f, 0.5f), font, TextAlignmentOptions.Right);

            // 상태이상(속성) 칸: 상태창 위쪽에 빈 네모가 항상 보이고, 상태가 생기면 그 안에 아이콘이 뜬다. 크기는 적/아군 동일
            int cnt = Mathf.Clamp(settings.statusSlotCount, 1, 8);
            float step = Mathf.Min(isz + 8f, cnt > 1 ? (SlotSize.x - isz) / (cnt - 1) : isz);
            slot.slotBgs = new Image[cnt];
            slot.slotIcons = new Image[cnt];
            for (int k = 0; k < cnt; k++)
            {
                Vector2 ip = new Vector2(-slot.halfWidth + isz * 0.5f + k * step, SlotSize.y * 0.5f + isz * 0.5f + 8f);
                slot.slotIcons[k] = MakeStatusSlot("StatusSlot" + (k + 1), parent, ip, isz, out slot.slotBgs[k]);
            }
            // 슬롯 0 = 방어. 이미지가 없으면 색 네모 + '방'
            Image g = slot.slotIcons[0];
            g.color = settings.guardIconColor;
            if (settings.guardIcon != null) g.sprite = settings.guardIcon;
            else
            {
                TextMeshProUGUI mark = BattleUIFactory.MakeText("Mark", g.transform, Vector2.zero, new Vector2(isz, isz),
                                                                "방", isz * 0.5f, Color.white, font);
                mark.raycastTarget = false;
            }
            slots.Add(slot);
        }

        // 선택 커서(테두리): 슬롯들 위에 그려지는 얇은 4변. 선택이 바뀌면 이 프레임이 미끄러져 이동한다.
        partyFrame = BattleUIFactory.MakeRect("PartySelectFrame", CanvasRect, Vector2.zero, SlotSize + new Vector2(12f, 12f));
        const float th = 4f;
        for (int e = 0; e < 4; e++)
        {
            Image edge = BattleUIFactory.MakeImage("Edge" + e, partyFrame, Vector2.zero, Vector2.zero, CursorColor);
            RectTransform rt = edge.rectTransform;
            switch (e)
            {
                case 0: rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(0f, th); break;
                case 1: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f); rt.sizeDelta = new Vector2(0f, th); break;
                case 2: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f); rt.sizeDelta = new Vector2(th, 0f); break;
                default: rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 0.5f); rt.sizeDelta = new Vector2(th, 0f); break;
            }
            rt.anchoredPosition = Vector2.zero;
            partyFrameEdges[e] = edge;
        }
        partyFrame.gameObject.SetActive(false);
    }

    private void BuildMessageAndHint()
    {
        // 메시지와 그 아웃라인 복사본을 한 묶음(MessageRoot)에 넣어 함께 이동시킨다
        messageRoot = BattleUIFactory.MakeRect("MessageRoot", CanvasRect, MessagePosP, new Vector2(590f, 56f));
        messageText = BattleUIFactory.MakeText("Message", messageRoot, Vector2.zero, new Vector2(590f, 56f),
                                               "", settings.messageFontSize, Color.white, font);
        messageText.enableAutoSizing = settings.messageAutoShrink; // 기본 꺼짐: 모든 턴에서 같은 크기
        messageText.fontSizeMin = Mathf.Min(16f, settings.messageFontSize);
        messageText.fontSizeMax = settings.messageFontSize;
        hintText = BattleUIFactory.MakeText("Hint", CanvasRect, HintPos, new Vector2(1100f, 40f),
                                            "", settings.hintFontSize, new Color(0.7f, 0.75f, 0.85f), font);

        BuildMessageOutline();
    }

    private void BuildTypingField()
    {
        typingRect = BattleUIFactory.MakeRect("TypingInput", CanvasRect, TypingPos, new Vector2(420f, 70f));
        typingRoot = typingRect.gameObject;
        typingRoot.SetActive(false); // 구성이 끝나기 전에 OnEnable이 돌지 않도록

        inputBg = typingRoot.AddComponent<Image>();
        inputBg.color = new Color(0f, 0f, 0f, 0.8f);

        RectTransform area = BattleUIFactory.MakeRect("TextArea", typingRect, Vector2.zero, Vector2.zero);
        BattleUIFactory.Stretch(area, 8f);
        area.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = BattleUIFactory.MakeText("Placeholder", area, Vector2.zero, Vector2.zero,
                                                               "단어를 입력하고 Enter", 28f, new Color(1f, 1f, 1f, 0.35f), font,
                                                               TextAlignmentOptions.Left);
        BattleUIFactory.Stretch(placeholder.rectTransform);
        placeholderText = placeholder;
        TextMeshProUGUI text = BattleUIFactory.MakeText("Text", area, Vector2.zero, Vector2.zero,
                                                        "", 32f, Color.white, font, TextAlignmentOptions.Left);
        BattleUIFactory.Stretch(text.rectTransform);

        inputField = typingRoot.AddComponent<TMP_InputField>();
        inputField.textViewport = area;
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.targetGraphic = inputBg;
        if (font != null) inputField.fontAsset = font;
        inputField.pointSize = 32f;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.richText = false;
        inputField.onFocusSelectAll = false;
        inputField.restoreOriginalTextOnEscape = false;
        inputField.onSubmit.AddListener(OnFieldSubmit);

        // Enter 버튼: 눌러도 Enter를 친 것과 같다. 입력창이 사라지면 같이 사라진다
        Image btn = BattleUIFactory.MakeImage("EnterButton", CanvasRect, EnterPos, new Vector2(130f, 70f), settings.enterButtonColor);
        if (settings.enterButtonSprite != null) { btn.sprite = settings.enterButtonSprite; btn.type = Image.Type.Sliced; }
        btn.raycastTarget = true;
        TextMeshProUGUI btnText = BattleUIFactory.MakeText("Label", btn.transform, Vector2.zero, new Vector2(130f, 70f),
                                                           "Enter", 28f, Color.white, font);
        btnText.raycastTarget = false;
        Button b = btn.gameObject.AddComponent<Button>();
        b.targetGraphic = btn;
        b.onClick.AddListener(OnEnterButton);
        enterButton = btn.gameObject;
        enterButton.SetActive(false);
    }

    private void OnEnterButton()
    {
        if (!typingRoot.activeInHierarchy || instantMode) return;
        OnFieldSubmit(inputField.text);
    }

    /// <summary>
    /// 얼굴 이미지는 상태창 바깥쪽에 따로 있고, 메뉴창과 선택 테두리보다 위에 보여야 해서
    /// 상태창의 자식이 아니라 별도 레이어에 만든 뒤 매 프레임 상태창 위치를 따라가게 한다.
    /// </summary>
    private void BuildFaces(IList<BattleActor> party)
    {
        faceLayer = BattleUIFactory.MakeRect("FaceLayer", CanvasRect, Vector2.zero, Vector2.zero);
        BattleUIFactory.Stretch(faceLayer);
        for (int i = 0; i < slots.Count; i++)
        {
            BattleActor a = party[i];
            PartySlotUI slot = slots[i];
            slot.face = BattleUIFactory.MakeImage("Face" + (i + 1), faceLayer, Vector2.zero, new Vector2(FaceSize, FaceSize),
                                                  a.color, BattleUIFactory.CircleSprite);
            slot.face.preserveAspect = true;
            slot.actor = a;
            slot.defaultSprite = slot.face.sprite; // 이미지가 없을 때 보이는 캐릭터 색 동그라미
            slot.shownSprite = slot.face.sprite;
        }
        UpdateFaces();
    }

    private void UpdateFaces()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            PartySlotUI s = slots[i];
            if (s.face == null) continue;
            s.face.rectTransform.anchoredPosition = s.root.anchoredPosition + new Vector2(s.faceSide * FaceOffsetX, FaceOffsetY);
            ApplyPartyFaceSprite(s, i);
        }
        ApplyEnemySprite();
    }

    // ───────────────────────── 상태별 얼굴 / 적 이미지 ─────────────────────────

    private static bool Has(int state) { return state >= 0; }

    private readonly List<FaceState> faceBuf = new List<FaceState>();

    private Color TintOf(FaceState st)
    {
        return settings.faceTintEnabled && settings.faceTints != null ? settings.faceTints.Get(st) : Color.white;
    }

    /// <summary>우선순위: 피격 > 행동 중(공격/스킬/방어) > 쓰러짐 > 상태이상/속성 > 방어 중 > HP 낮음 > 행동 선택 중 > 대기</summary>
    private void ApplyPartyFaceSprite(PartySlotUI s, int index)
    {
        if (s.actor == null) return;
        FaceSet f = s.actor.faces ?? new FaceSet();

        // 지금 해당하는 상태를 우선순위 순으로 모은다
        faceBuf.Clear();
        if (Has(s.flashState) && Time.time < s.flashUntil) faceBuf.Add((FaceState)s.flashState);
        if (Has(s.holdState)) faceBuf.Add((FaceState)s.holdState);
        if (s.dead) faceBuf.Add(FaceState.Dead);
        if (Has(s.ailmentState)) faceBuf.Add((FaceState)s.ailmentState);
        if (s.guarding) faceBuf.Add(FaceState.Guard);
        if (s.lowHp) faceBuf.Add(FaceState.LowHp);
        if (activeSlot == index) faceBuf.Add(FaceState.Selecting);

        Sprite pick = null;
        foreach (FaceState st in faceBuf) { pick = f.Get(st); if (pick != null) break; }
        if (pick == null) pick = f.idle != null ? f.idle : s.actor.face;
        Color tint = TintOf(faceBuf.Count > 0 ? faceBuf[0] : FaceState.Idle); // 색조는 가장 우선순위가 높은 상태 기준

        if (pick == s.shownSprite && tint == s.shownTint) return;
        s.shownSprite = pick; s.shownTint = tint;
        if (pick != null) { s.face.sprite = pick; s.face.color = tint; }
        else { s.face.sprite = s.defaultSprite; s.face.color = s.actor.color * tint; }
    }

    /// <summary>우선순위: 피격 > 공격(적 턴) > 쓰러짐 > 상태이상/속성 > HP 낮음 > 대기</summary>
    private void ApplyEnemySprite()
    {
        if (enemyImage == null || enemyData == null) return;
        EnemyFaceSet f = enemyData.faces ?? new EnemyFaceSet();

        faceBuf.Clear();
        if (Has(enemyFlashState) && Time.time < enemyFlashUntil) faceBuf.Add((FaceState)enemyFlashState);
        if (Has(enemyHoldState)) faceBuf.Add((FaceState)enemyHoldState);
        if (enemyDead) faceBuf.Add(FaceState.Dead);
        if (Has(enemyAilmentState)) faceBuf.Add((FaceState)enemyAilmentState);
        if (enemyLowHp) faceBuf.Add(FaceState.LowHp);

        Sprite pick = null;
        foreach (FaceState st in faceBuf) { pick = f.Get(st); if (pick != null) break; }
        if (pick == null) pick = f.idle;
        Color tint = TintOf(faceBuf.Count > 0 ? faceBuf[0] : FaceState.Idle);

        if (pick == enemyShownSprite && tint == enemyShownTint) return;
        enemyShownSprite = pick; enemyShownTint = tint;
        if (pick != null) { enemyImage.sprite = pick; enemyImage.color = tint; }
        else { enemyImage.sprite = null; enemyImage.color = enemyData.color * tint; }
    }

    /// <summary>아군 얼굴을 잠깐(seconds 초) 해당 상태로 바꾼다 (피격 등).</summary>
    public void FlashPartyFace(int index, FaceState state, float seconds)
    {
        if (index < 0 || index >= slots.Count) return;
        slots[index].flashState = (int)state;
        slots[index].flashUntil = Time.time + Mathf.Max(0f, seconds);
    }

    /// <summary>아군 얼굴을 ClearPartyFace 를 부르기 전까지 해당 상태로 유지한다 (행동 중 공격/스킬/방어).</summary>
    public void HoldPartyFace(int index, FaceState state)
    {
        if (index >= 0 && index < slots.Count) slots[index].holdState = (int)state;
    }

    public void ClearPartyFace(int index)
    {
        if (index >= 0 && index < slots.Count) slots[index].holdState = -1;
    }

    /// <summary>상태이상/속성 이미지를 켜고 끈다 (state 는 Status 또는 Element). 해당 시스템이 생기면 여기에 연결한다.</summary>
    public void SetPartyAilmentFace(int index, FaceState state, bool on)
    {
        if (index >= 0 && index < slots.Count) slots[index].ailmentState = on ? (int)state : -1;
    }

    public void FlashEnemyFace(FaceState state, float seconds)
    {
        enemyFlashState = (int)state;
        enemyFlashUntil = Time.time + Mathf.Max(0f, seconds);
    }

    public void HoldEnemyFace(FaceState state) { enemyHoldState = (int)state; }
    public void ClearEnemyFace() { enemyHoldState = -1; }
    public void SetEnemyAilmentFace(FaceState state, bool on) { enemyAilmentState = on ? (int)state : -1; }

    /// <summary>전투를 처음부터 다시 시작할 때 남은 상태를 전부 지운다.</summary>
    public void ResetFaces()
    {
        foreach (PartySlotUI s in slots) { s.holdState = -1; s.flashState = -1; s.ailmentState = -1; }
        enemyHoldState = -1; enemyFlashState = -1; enemyAilmentState = -1;
    }

    private void BuildMenu()
    {
        float h0 = MenuHeight(3);
        var panel = BattleUIFactory.MakeImage("CommandMenu", CanvasRect, new Vector2(-620f, -30f), new Vector2(MenuWidth, h0),
                                              new Color(0.08f, 0.1f, 0.2f, 0.96f));
        menuPanel = panel.gameObject;
        menuRect = panel.rectTransform;

        // 선택 커서(막대): 항목들 뒤에 깔리고, 선택이 바뀌면 위아래로 미끄러진다
        menuCursorRt = BattleUIFactory.MakeRect("Cursor", menuRect, Vector2.zero, new Vector2(MenuWidth - 20f, MenuRowHeight - 4f));
        AnchorTop(menuCursorRt, MenuRowCenter(0));
        Image bar = BattleUIFactory.MakeImage("Bar", menuCursorRt, Vector2.zero, Vector2.zero, new Color(1f, 0.88f, 0.35f, 0.2f));
        BattleUIFactory.Stretch(bar.rectTransform);
        Image accent = BattleUIFactory.MakeImage("Accent", menuCursorRt, Vector2.zero, Vector2.zero, CursorColor);
        RectTransform ar = accent.rectTransform;
        ar.anchorMin = new Vector2(0f, 0f); ar.anchorMax = new Vector2(0f, 1f); ar.pivot = new Vector2(0f, 0.5f);
        ar.sizeDelta = new Vector2(5f, 0f); ar.anchoredPosition = Vector2.zero;

        menuHeader = BattleUIFactory.MakeText("Header", menuRect, Vector2.zero, new Vector2(MenuWidth - 20f, MenuHeaderHeight),
                                              "", 26f, CursorColor, font);
        AnchorTop(menuHeader.rectTransform, MenuTopPad + MenuHeaderHeight * 0.5f);
        for (int i = 0; i < MaxMenuItems; i++)
        {
            menuItems[i] = BattleUIFactory.MakeText($"Item{i + 1}", menuRect, Vector2.zero,
                                                    new Vector2(MenuWidth - 50f, MenuRowHeight - 4f), "", 30f, Color.white, font,
                                                    TextAlignmentOptions.Left);
            AnchorTop(menuItems[i].rectTransform, MenuRowCenter(i));
        }
        menuHint = BattleUIFactory.MakeText("Hint", menuRect, Vector2.zero, new Vector2(MenuWidth - 20f, MenuHintHeight - 4f),
                                            "", 17f, new Color(0.7f, 0.75f, 0.85f), font);
        AnchorTop(menuHint.rectTransform, MenuTopPad + MenuHeaderHeight + MenuHintHeight * 0.5f);
        menuPanel.SetActive(false);
    }

    // ───────────────────────── 말풍선 대사 (Canvas_SpeechBalloon 프리팹 재사용) ─────────────────────────

    private RectTransform balloonLayer;
    private readonly Dictionary<string, SpeechBalloonController> balloons = new Dictionary<string, SpeechBalloonController>();
    private bool balloonWarned;

    /// <summary>한 번에 띄울 말풍선 하나 (아군 또는 적).</summary>
    public struct BalloonSpec
    {
        public bool enemy;
        public int partyIndex;
        public string text;
        public bool hideTail;
        public static BalloonSpec Enemy(string text) { return new BalloonSpec { enemy = true, partyIndex = -1, text = text }; }
        public static BalloonSpec Party(int index, string text, bool hideTail = false) { return new BalloonSpec { enemy = false, partyIndex = index, text = text, hideTail = hideTail }; }
    }

    private SpeechBalloonController GetPartyBalloon(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        PartySlotUI s = slots[index];
        Vector2 pos = s.face != null ? s.face.rectTransform.anchoredPosition : s.root.anchoredPosition;
        Vector2 off = settings.partyBalloonPos;
        if (index != 0) off.x = -off.x; // 오른쪽 아군은 화면 가운데 쪽(왼쪽)으로
        return GetBalloon("Party" + index, pos, off);
    }

    /// <summary>말풍선 꼬리(SpeechBalloonTail)를 켜고 끈다.</summary>
    private static void SetTailVisible(SpeechBalloonController b, bool visible)
    {
        Transform tail = b.transform.Find("SpeechBalloonRoot/SpeechBalloonTail");
        if (tail != null) tail.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 여러 말풍선을 동시에 띄운다. 모두 글자가 다 나온 뒤 잠깐 머물다 함께 사라질 때까지 대기.
    /// 적 말풍선이 포함되면 그동안 적 이름/HP 바와 안내 메시지가 숨겨진다.
    /// </summary>
    public IEnumerator BalloonGroup(IList<BalloonSpec> specs)
    {
        var active = new List<SpeechBalloonController>();
        bool hasEnemy = false;
        foreach (BalloonSpec sp in specs)
        {
            if (string.IsNullOrWhiteSpace(sp.text)) continue;
            SpeechBalloonController b = sp.enemy ? GetBalloon("Enemy", enemyCur, settings.enemyBalloonPos) : GetPartyBalloon(sp.partyIndex);
            if (b == null) continue;
            b.Show(new DialogueLine { text = sp.text.Replace("\\n", "\n").Replace("<br>", "\n") });
            SetTailVisible(b, !sp.hideTail);
            active.Add(b);
            if (sp.enemy) hasEnemy = true;
        }
        if (active.Count == 0) yield break;

        if (hasEnemy) SetEnemyInfoVisible(false);
        yield return null; // 이 단계로 넘어오게 한 키 입력이 바로 스킵으로 처리되지 않도록
        while (true)
        {
            bool revealing = false;
            foreach (SpeechBalloonController b in active) if (b.IsRevealing) revealing = true;
            if (!revealing) break;
            if (PressedConfirm() || PressedClick()) foreach (SpeechBalloonController b in active) b.SkipReveal();
            yield return null;
        }
        yield return new WaitForSeconds(Mathf.Max(0f, settings.balloonHoldSeconds));
        foreach (SpeechBalloonController b in active) b.Hide();
        if (hasEnemy) SetEnemyInfoVisible(true);
    }

    /// <summary>
    /// 말풍선을 목록 순서대로 한 번에 하나씩 띄운다 (맵의 대사 시스템처럼).
    /// 글자가 나오는 중에 진행 키를 누르면 끝까지 보여주고, 다 나온 뒤 한 번 더 누르면 다음 줄로 넘어간다.
    /// 적이 말하는 줄에서는 적 이름/HP 바와 안내 메시지가 숨겨진다.
    /// </summary>
    public IEnumerator BalloonSequence(IList<BalloonSpec> specs)
    {
        foreach (BalloonSpec sp in specs)
        {
            if (string.IsNullOrWhiteSpace(sp.text)) continue;
            SpeechBalloonController b = sp.enemy ? GetBalloon("Enemy", enemyCur, settings.enemyBalloonPos) : GetPartyBalloon(sp.partyIndex);
            if (b == null) continue;
            b.Show(new DialogueLine { text = sp.text.Replace("\\n", "\n").Replace("<br>", "\n") });
            SetTailVisible(b, !sp.hideTail);
            if (sp.enemy) SetEnemyInfoVisible(false);

            yield return null; // 이 줄로 넘어오게 한 키 입력이 바로 스킵으로 처리되지 않도록
            while (b.IsRevealing)
            {
                if (PressedConfirm() || PressedClick()) b.SkipReveal();
                yield return null;
            }
            yield return null;
            while (!(PressedConfirm() || PressedClick())) yield return null;

            b.Hide();
            if (sp.enemy) SetEnemyInfoVisible(true);
        }
    }

    /// <summary>아군 한 명이 말풍선으로 대사를 한다.</summary>
    public IEnumerator PartyBalloon(int index, string text)
    {
        yield return BalloonGroup(new[] { BalloonSpec.Party(index, text) });
    }

    /// <summary>적이 말풍선으로 대사를 한다. 말풍선이 떠 있는 동안에만 적 이름/HP 바와 안내 메시지가 숨겨진다.</summary>
    public IEnumerator EnemyBalloon(string text)
    {
        yield return BalloonGroup(new[] { BalloonSpec.Enemy(text) });
    }

    /// <summary>위쪽 적 패널(이름, HP 바)과 그에 붙은 안내 메시지를 켜고 끈다.</summary>
    private void SetEnemyInfoVisible(bool visible)
    {
        if (enemyPanelRt != null) enemyPanelRt.gameObject.SetActive(visible);
        if (enemyRowItems != null) foreach (RectTransform r in enemyRowItems) r.gameObject.SetActive(visible);
        // 안내 메시지는 플레이어 턴 배치(위쪽 적 패널에 붙어 있을 때)에서만 같이 숨긴다. 적 턴에는 아래쪽에 계속 보인다.
        if (messageRoot != null) messageRoot.gameObject.SetActive(visible || layoutT >= 0.5f);
    }

    private SpeechBalloonController waitBalloon;

    /// <summary>행동을 고르는 동안 아군 말풍선을 띄워 둔다 (기다리지 않고 바로 돌아온다). HideWaitBalloon 으로 끈다.</summary>
    public void ShowWaitBalloon(int index, string text)
    {
        HideWaitBalloon();
        if (string.IsNullOrWhiteSpace(text)) return;
        SpeechBalloonController b = GetPartyBalloon(index);
        if (b == null) return;
        b.Show(new DialogueLine { text = text.Replace("\\n", "\n").Replace("<br>", "\n") });
        waitBalloon = b;
    }

    public void HideWaitBalloon()
    {
        if (waitBalloon != null) waitBalloon.Hide();
        waitBalloon = null;
    }

    private SpeechBalloonController GetBalloon(string key, Vector2 anchorPos, Vector2 bodyOffset)
    {
        if (settings.speechBalloonPrefab == null)
        {
            if (!balloonWarned)
            {
                balloonWarned = true;
                Debug.LogWarning("[Battle] 말풍선 대사가 있지만 Settings 의 Speech Balloon Prefab 이 비어 있어 건너뜁니다.");
            }
            return null;
        }

        SpeechBalloonController b;
        if (!balloons.TryGetValue(key, out b) || b == null)
        {
            GameObject go = Instantiate(settings.speechBalloonPrefab, balloonLayer, false);
            go.name = "Balloon_" + key;
            // 원래는 NPC에 붙는 World Space 캔버스(1/90 배율)라서, 화면 캔버스 안에서는 픽셀 1:1이 되도록 맞춘다
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1600f, 900f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            GraphicRaycaster gr = go.GetComponent<GraphicRaycaster>();
            if (gr != null) gr.enabled = false; // 말풍선이 마우스 입력을 가로채지 않도록
            b = go.GetComponentInChildren<SpeechBalloonController>(true);
            if (b == null)
            {
                Debug.LogWarning("[Battle] Speech Balloon Prefab 에서 SpeechBalloonController 를 찾지 못했습니다.");
                Destroy(go);
                return null;
            }
            balloons[key] = b;
        }

        // 프리팹 전체를 말하는 쪽 위치에 두고, 말풍선 몸체(SpeechBalloonRoot)는 거기서 offset 만큼 떨어뜨린다
        // 적 턴 배치에서는 아군이 화면 가장자리에 있어서 말풍선이 화면 밖으로 나갈 수 있으니 안쪽으로 당긴다
        const float halfBody = 140f;
        float bodyX = Mathf.Clamp(anchorPos.x + bodyOffset.x, -800f + halfBody, 800f - halfBody);
        bodyOffset.x = bodyX - anchorPos.x;

        RectTransform canvasRt = (RectTransform)b.transform;
        canvasRt.anchoredPosition3D = new Vector3(anchorPos.x, anchorPos.y, 0f);
        Transform body = canvasRt.Find("SpeechBalloonRoot");
        if (body != null) ((RectTransform)body).anchoredPosition = bodyOffset;
        SetTailVisible(b, true); // 기본은 꼬리 표시 (필요한 곳에서만 숨김)
        return b;
    }

    // ───────────────────────── 플레이어 턴 ↔ 적 턴 배치 전환 ─────────────────────────

    public void BindArena(BattleArena arena) { arenaRef = arena; ApplyLayout(layoutT); }

    /// <summary>애니메이션 없이 바로 배치한다 (false = 플레이어 턴, true = 적 턴).</summary>
    public void SetLayoutInstant(bool enemyTurn)
    {
        layoutT = enemyTurn ? 1f : 0f;
        ApplyLayout(layoutT);
    }

    /// <summary>
    /// UI들을 다른 턴의 배치로 선형 이동시킨다. 끝날 때까지 기다리는 코루틴.
    /// 적이 위로/아래로 이동, 칸 판이 납작해지거나 펼쳐짐(글자는 펼쳐지는 중반 이후에 나타남),
    /// 안내 메시지가 위↔아래, 상태창이 아래↔양옆으로 이동한다.
    /// </summary>
    public IEnumerator PlayLayoutTransition(bool toEnemyTurn, float seconds)
    {
        float target = toEnemyTurn ? 1f : 0f;
        if (Mathf.Approximately(layoutT, target)) yield break;

        float dur = Mathf.Max(0.01f, seconds);
        while (!Mathf.Approximately(layoutT, target))
        {
            layoutT = Mathf.MoveTowards(layoutT, target, Time.deltaTime / dur);
            ApplyLayout(layoutT);
            yield return null;
        }
        ApplyLayout(target);
    }

    private void ApplyLayout(float t)
    {
        enemyCur = Vector2.Lerp(EnemyPosP, EnemyPosE, t);
        if (enemyBody != null) enemyBody.anchoredPosition = enemyCur;

        // 적 이름/HP 패널: 위쪽 가장자리는 고정, 아래가 줄어든다 (안내 메시지 줄이 사라지므로)
        if (enemyPanelRt != null)
        {
            float h = Mathf.Lerp(EnemyPanelHP, EnemyPanelHE, t);
            enemyPanelRt.sizeDelta = new Vector2(EnemyPanelW, h);
            enemyPanelRt.anchoredPosition = new Vector2(0f, 450f - h * 0.5f);
            float rowY = Mathf.Lerp(EnemyRowYP, EnemyRowYE, t);
            for (int i = 0; i < enemyRowItems.Length; i++) enemyRowItems[i].anchoredPosition = new Vector2(enemyRowX[i], rowY);
        }

        UpdateMessagePosition();

        for (int i = 0; i < slots.Count; i++)
        {
            float side = i == 0 ? -1f : 1f;
            PartySlotUI s = slots[i];
            float x = Mathf.Lerp(SlotCenterP + side * SlotXP, side * SlotXE, t);
            s.basePos = new Vector2(x, SlotY);
            if (s.shake == null && s.root != null) s.root.anchoredPosition = s.basePos;
        }

        if (arenaRef != null)
        {
            float scaleY = Mathf.Lerp(settings.flatGridScaleY, 1f, t);
            float detail = Mathf.Clamp01((t - 0.5f) * 2f); // 글자·플레이어: 전환 후반에 나타나고, 돌아갈 땐 전반에 사라진다
            arenaRef.SetPose(Vector2.Lerp(ArenaPosP, ArenaPosE, t), scaleY, detail);
        }
    }

    /// <summary>
    /// 안내 메시지 위치: 플레이어 턴엔 위쪽 패널 안, 적 턴엔 아래쪽.
    /// 입력창이 보일 땐 그 위, 입력창이 없으면(숨겨졌으면) 입력창 자리로 내려온다.
    /// </summary>
    private void UpdateMessagePosition()
    {
        if (messageRoot == null) return;
        float x = Mathf.Lerp(MessagePosP.x, MessageXE, layoutT) + messageShakeX;
        float y = Mathf.Lerp(MessagePosP.y, messageYE, layoutT);
        messageRoot.anchoredPosition = new Vector2(x, y);
    }

    // ───────────────────────── 배경 이미지 ─────────────────────────

    private Image bgImage;
    private AspectRatioFitter bgFitter;

    /// <summary>전투 배경 이미지를 설정한다. sprite 가 null 이면 이미지 없이 단색 배경만 보인다.</summary>
    public void SetBackground(Sprite sprite, BackgroundFit fit, Color tint)
    {
        if (bgImage == null) return;
        if (sprite == null) { bgImage.gameObject.SetActive(false); return; }

        bgImage.gameObject.SetActive(true);
        bgImage.sprite = sprite;
        bgImage.color = tint;
        bgImage.preserveAspect = false;

        RectTransform rt = bgImage.rectTransform;
        if (fit == BackgroundFit.Stretch)
        {
            bgFitter.enabled = false;
            BattleUIFactory.Stretch(rt);
            return;
        }

        // Cover/Contain: 부모(화면) 크기에 맞춰 비율을 유지하며 덮거나 담는다
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        bgFitter.aspectMode = fit == BackgroundFit.Cover ? AspectRatioFitter.AspectMode.EnvelopeParent
                                                         : AspectRatioFitter.AspectMode.FitInParent;
        bgFitter.aspectRatio = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 16f / 9f;
        bgFitter.enabled = true;
    }

    // ───────────────────────── 턴 전환 연출 ─────────────────────────

    private RectTransform bannerRoot, bannerContent, bannerAccentTop, bannerAccentBottom;
    private Image bannerBand, bannerImage, bannerTop, bannerBottom;
    private TextMeshProUGUI bannerText;
    private CanvasGroup bannerGroup;

    private void BuildTurnBanner()
    {
        bannerRoot = BattleUIFactory.MakeRect("TurnBanner", CanvasRect, Vector2.zero, new Vector2(1700f, 200f));
        bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;

        bannerBand = BattleUIFactory.MakeImage("Band", bannerRoot, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.75f));
        BattleUIFactory.Stretch(bannerBand.rectTransform);

        bannerTop = BattleUIFactory.MakeImage("LineTop", bannerRoot, Vector2.zero, Vector2.zero, Color.white);
        RectTransform tr = bannerTop.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 5f); tr.anchoredPosition = Vector2.zero;
        bannerBottom = BattleUIFactory.MakeImage("LineBottom", bannerRoot, Vector2.zero, Vector2.zero, Color.white);
        RectTransform br = bannerBottom.rectTransform;
        br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 0f); br.pivot = new Vector2(0.5f, 0f);
        br.sizeDelta = new Vector2(0f, 5f); br.anchoredPosition = Vector2.zero;

        bannerContent = BattleUIFactory.MakeRect("Content", bannerRoot, Vector2.zero, Vector2.zero);
        bannerText = BattleUIFactory.MakeText("Text", bannerContent, Vector2.zero, new Vector2(1400f, 200f), "", 96f, Color.white, font);
        bannerImage = BattleUIFactory.MakeImage("Image", bannerContent, Vector2.zero, new Vector2(640f, 180f), Color.white);
        bannerImage.preserveAspect = true;

        bannerRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// "플레이어 턴!" / "적 턴!" 연출. 글자(또는 이미지)가 왼쪽에서 들어와 잠깐 머물다 오른쪽으로 빠진다.
    /// 끝날 때까지 기다리는 코루틴이라 이 동안은 게임이 진행되지 않는다.
    /// </summary>
    public IEnumerator ShowTurnBanner(TurnBannerSettings s, TurnBannerEntry e)
    {
        if (s == null || e == null || !s.enabled) yield break;

        bannerRoot.sizeDelta = new Vector2(1700f, Mathf.Max(20f, s.bandHeight));
        bannerBand.color = s.bandColor;
        bannerTop.color = e.color;
        bannerBottom.color = e.color;

        bool useImage = e.image != null;
        bannerImage.gameObject.SetActive(useImage);
        bannerText.gameObject.SetActive(!useImage);
        if (useImage)
        {
            bannerImage.sprite = e.image;
            bannerImage.rectTransform.sizeDelta = s.imageSize;
        }
        else
        {
            bannerText.text = e.text;
            bannerText.color = e.color;
            bannerText.fontSize = s.fontSize;
        }

        const float offX = 900f;   // 화면 밖에서 들어오고 나가는 거리
        const float drift = 40f;   // 머무는 동안 천천히 흐르는 거리
        bannerRoot.gameObject.SetActive(true);

        float tIn = Mathf.Max(0.01f, s.slideInSeconds);
        for (float t = 0f; t < tIn; t += Time.deltaTime)
        {
            float p = t / tIn;
            float ease = 1f - (1f - p) * (1f - p) * (1f - p);       // 빠르게 들어와 부드럽게 멈춤
            bannerContent.anchoredPosition = new Vector2(Mathf.Lerp(-offX, 0f, ease), 0f);
            bannerGroup.alpha = p;
            yield return null;
        }
        bannerGroup.alpha = 1f;

        float hold = Mathf.Max(0f, s.holdSeconds);
        for (float t = 0f; t < hold; t += Time.deltaTime)
        {
            bannerContent.anchoredPosition = new Vector2(Mathf.Lerp(0f, drift, t / hold), 0f);
            yield return null;
        }

        float tOut = Mathf.Max(0.01f, s.slideOutSeconds);
        for (float t = 0f; t < tOut; t += Time.deltaTime)
        {
            float p = t / tOut;
            float ease = p * p * p;                                   // 천천히 출발해 빠르게 빠짐
            bannerContent.anchoredPosition = new Vector2(Mathf.Lerp(drift, offX, ease), 0f);
            bannerGroup.alpha = 1f - p;
            yield return null;
        }

        bannerRoot.gameObject.SetActive(false);
    }

    private const float MenuFixedHeight = 650f; // 스케치처럼 항목 수와 상관없이 큰 창
    private static float MenuHeight(int n) { return MenuFixedHeight; }
    private static float MenuRowCenter(int i) { return MenuTopPad + MenuHeaderHeight + MenuHintHeight + MenuRowHeight * (i + 0.5f); }

    /// <summary>위쪽 중앙 기준으로 배치 (패널 높이가 변해도 위에서부터의 위치는 유지된다). y 는 위에서 내려온 거리.</summary>
    private static void AnchorTop(RectTransform rt, float yFromTop)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -yFromTop);
    }


    // ───────────────────────── 탄창 그림 (장전 연출) ─────────────────────────

    private GameObject magPanel;
    private RectTransform magRoot;
    private TextMeshProUGUI magLabel;
    private readonly List<GameObject> magObjs = new List<GameObject>(); // 칸·총알·번호·화살표 전부 (다시 그릴 때 한꺼번에 지움)

    /// <summary>PickMagazinePosition 의 결과 (취소 = -1)</summary>
    public int LastMagPick { get; private set; }

    private void BuildMagazineView()
    {
        Vector2 pos = ArenaCenter + settings.magazineOffset;
        Image frame = BattleUIFactory.MakeImage("MagazinePanel", CanvasRect, pos, new Vector2(900f, 380f),
                                                settings.magazineFrameColor, settings.magazineFrameSprite);
        magPanel = frame.gameObject;
        magRoot = frame.rectTransform;
        magLabel = BattleUIFactory.MakeText("MagazineLabel", magRoot, new Vector2(0f, 150f), new Vector2(860f, 36f),
                                            "", 24f, Color.white, font);
        magPanel.SetActive(false);
    }

    private static bool MagVertical(Magazine m) { return m.Kind == MagazineKind.Stack; }   // 스택만 세로로 쌓는다
    private Vector2 MagSlotSize(Magazine m) { return MagVertical(m) ? settings.magazineStackSlotSize : settings.magazineSlotSize; }
    private Vector2 MagBulletSize(Magazine m) { return MagVertical(m) ? settings.magazineStackBulletSize : settings.magazineBulletSize; }
    private const float MagHSlotY = -35f, MagVX = -40f;

    private float MagSlotX(int i, int cap)
    {
        float step = settings.magazineSlotSize.x + settings.magazineSlotGap;
        return (i - (cap - 1) * 0.5f) * step;
    }

    /// <summary>i번째 "칸(슬롯)"의 중심 좌표 — i는 배열 index가 아니라 화면상 슬롯 위치(0 = 맨 아래 칸).
    /// 가로 배치는 발사 순서(배열 index)를 그대로 슬롯 위치로 쓰지만, 세로(스택)는 그렇지 않다 —
    /// 호출하는 쪽(ShowMagazine)에서 발사 순서와 화면 슬롯 위치를 따로 계산해서 넘긴다.</summary>
    private Vector2 MagSlotPos(int i, Magazine m)
    {
        if (!MagVertical(m)) return new Vector2(MagSlotX(i, m.Capacity), MagHSlotY);
        float step = settings.magazineStackSlotSize.y + settings.magazineSlotGap;
        return new Vector2(MagVX, (i - (m.Capacity - 1) * 0.5f) * step - 12f);
    }

    private T MagTrack<T>(T img) where T : Component { magObjs.Add(img.gameObject); return img; }

    private Image MakeMagBullet(Magazine m, BulletDef b, Vector2 pos)
    {
        Color c = settings.bulletTintByElement || settings.bulletSprite == null ? b.CursorColor : Color.white;
        return MagTrack(BattleUIFactory.MakeImage("Bullet", magRoot, pos, MagBulletSize(m), c, settings.bulletSprite));
    }

    private void SetMagLabel(Magazine mag)
    {
        magLabel.text = $"{Magazine.KindLabel(mag.Kind)}  {mag.Count}/{mag.Capacity}";
    }

    /// <summary>선(막대) 하나. angle 은 반시계 방향 도.</summary>
    private Image MagLine(Vector2 center, float length, float angle)
    {
        Image l = MagTrack(BattleUIFactory.MakeImage("Arrow", magRoot, center, new Vector2(length, 6f), settings.magazineArrowColor));
        l.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        return l;
    }

    /// <summary>화살표 (그림 글자 없이 막대로 만든다). tip = 화살촉 끝, dirAngle = 가리키는 방향(도, 0=오른쪽 90=위 180=왼쪽).</summary>
    private void MagArrow(Vector2 tip, float length, float dirAngle)
    {
        float r = dirAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        MagLine(tip - dir * (length * 0.5f), length, dirAngle);          // 몸통
        const float head = 30f, spread = 35f;
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            float a = dirAngle + 180f + sgn * spread;                    // 촉 끝에서 뒤로 벌어지는 두 선
            float ar = a * Mathf.Deg2Rad;
            Vector2 c = tip + new Vector2(Mathf.Cos(ar), Mathf.Sin(ar)) * (head * 0.5f);
            MagLine(c, head, a);
        }
    }

    /// <summary>탄창 그림을 현재 내용 그대로 보여준다 (칸 + 총알 + 발사 순서 번호 + 발사 방향 화살표).</summary>
    public void ShowMagazine(Magazine mag)
    {
        if (!settings.magazineVisual || mag == null) return;
        ClearMagazineView();
        int cap = mag.Capacity;
        bool vert = MagVertical(mag);
        Vector2 ss = MagSlotSize(mag);
        float gap = settings.magazineSlotGap;

        float w, h;
        if (!vert) { w = cap * ss.x + (cap - 1) * gap + 80f; h = ss.y + 330f; }
        else { w = ss.x + 300f; h = cap * (ss.y + gap) + 144f; }
        magRoot.sizeDelta = new Vector2(Mathf.Max(w, 460f), h);
        magLabel.rectTransform.anchoredPosition = new Vector2(0f, h * 0.5f - 26f);

        for (int i = 0; i < cap; i++)
            MagTrack(BattleUIFactory.MakeImage("Slot" + (i + 1), magRoot, MagSlotPos(i, mag), ss,
                                               settings.magazineSlotColor, settings.magazineSlotSprite));
        for (int i = 0; i < mag.Count; i++)
        {
            // 스택은 항상 index 0(배열 맨 앞)에 새 탄이 꽂히므로, 배열 index를 그대로 슬롯 번호로 쓰면
            // 넣을 때마다 "가장 오래된 탄"의 자리가 위아래로 밀려 보인다. 실제로 쌓아올린 것처럼 보이려면
            // "나이"(오래된 탄일수록 아래) 기준 슬롯을 써야 한다: index 0(가장 최근 = 가장 먼저 발사)이 지금
            // 채워진 칸 중 가장 위, index count-1(가장 오래됨)이 항상 맨 아래에 고정된다.
            int slot = vert ? (mag.Count - 1 - i) : i;
            Vector2 p = MagSlotPos(slot, mag);
            MakeMagBullet(mag, mag.Bullets[i], p);
            // 발사 순서 번호 (1 = 가장 먼저)
            Vector2 np = vert ? new Vector2(p.x - ss.x * 0.5f - 26f, p.y) : new Vector2(p.x, p.y - ss.y * 0.5f - 18f);
            MagTrack(BattleUIFactory.MakeText("Order" + (i + 1), magRoot, np, new Vector2(44f, 30f),
                                              (i + 1).ToString(), 24f, settings.magazineArrowColor, font));
        }

        // 발사 방향 화살표
        if (!vert)
        {
            float left = MagSlotX(0, cap) - ss.x * 0.5f, right = MagSlotX(cap - 1, cap) + ss.x * 0.5f;
            float y = MagHSlotY - ss.y * 0.5f - 58f;
            MagArrow(new Vector2(left, y), right - left, 180f);                // 왼쪽을 향함 = 왼쪽부터 발사
            MagTrack(BattleUIFactory.MakeText("FireLabel", magRoot, new Vector2((left + right) * 0.5f, y - 26f), new Vector2(300f, 30f),
                                              "발사 방향 (1번부터)", 22f, settings.magazineArrowColor, font));
        }
        else
        {
            float x = MagVX + ss.x * 0.5f + 60f;
            // 0번 칸(먼저 발사)이 이제 맨 아래이므로, 화살표는 0번 칸 쪽(아래)에서 마지막 칸 쪽(위)으로 그린다.
            Vector2 bottomSlot = MagSlotPos(0, mag), topSlot = MagSlotPos(cap - 1, mag);
            float yTop = topSlot.y + ss.y * 0.5f, yBot = bottomSlot.y - ss.y * 0.5f;
            MagArrow(new Vector2(x, yTop), yTop - yBot, 90f);                  // 위를 향함 = 아래(0번)부터 쌓여 위로 발사
            MagTrack(BattleUIFactory.MakeText("FireLabel", magRoot, new Vector2(x + 4f, yTop + 24f), new Vector2(160f, 30f),
                                              "발사", 22f, settings.magazineArrowColor, font));
        }

        MagTrack(BattleUIFactory.MakeText("OrderGuide", magRoot, new Vector2(0f, -h * 0.5f + 18f), new Vector2(560f, 26f),
                                          "숫자가 작을수록 먼저 발사됩니다", 18f, new Color(0.8f, 0.82f, 0.9f), font));
        SetMagLabel(mag);
        magPanel.SetActive(true);
    }

    public void HideMagazine()
    {
        if (magPanel == null) return;
        ClearMagazineView();
        magPanel.SetActive(false);
    }

    private void ClearMagazineView()
    {
        foreach (GameObject g in magObjs) if (g != null) Destroy(g);
        magObjs.Clear();
    }

    /// <summary>
    /// 탄창 그림 위에서 ←→(A/D)로 넣을 위치를 고른다. Z/Enter/클릭 = 결정, X/우클릭 = 취소.
    /// positions 는 Magazine.InsertPositions() 의 값. 결과는 LastMagPick (취소 -1).
    /// </summary>
    public IEnumerator PickMagazinePosition(Magazine mag, BulletDef bullet, IList<int> positions)
    {
        LastMagPick = -1;
        if (positions.Count == 0) yield break;
        if (!settings.magazineVisual || magPanel == null || !magPanel.activeSelf) { LastMagPick = positions[0]; yield break; }

        Vector2 ss = MagSlotSize(mag);
        float step = ss.x + settings.magazineSlotGap;
        Image marker = MagTrack(BattleUIFactory.MakeImage("InsertMarker", magRoot, Vector2.zero, new Vector2(8f, ss.y + 24f),
                                                          settings.magazineArrowColor));
        Image ghost = MakeMagBullet(mag, bullet, Vector2.zero);
        Color gc = ghost.color; gc.a = 0.6f; ghost.color = gc;
        magLabel.text = "←→ 위치 선택    Z 결정    X 취소";

        int ci = 0;
        bool first = true;
        while (true)
        {
            // 마커: 넣을 위치(= 그 칸의 왼쪽 경계). 넣을 총알은 그 위에 반투명으로 보인다
            float x = MagSlotX(positions[ci], mag.Capacity) - step * 0.5f;
            marker.rectTransform.anchoredPosition = new Vector2(x, MagHSlotY);
            ghost.rectTransform.anchoredPosition = new Vector2(x, MagHSlotY + ss.y * 0.5f + MagBulletSize(mag).y * 0.5f + 14f);

            yield return null;
            if (first) { first = false; continue; }

            Mouse mouse = Mouse.current;
            if (PressedLeft()) ci = Mathf.Max(0, ci - 1);
            else if (PressedRight()) ci = Mathf.Min(positions.Count - 1, ci + 1);
            else if (PressedConfirm() || PressedClick()) { LastMagPick = positions[ci]; break; }
            else if (PressedCancel() || (mouse != null && mouse.rightButton.wasPressedThisFrame)) break;
        }

        magObjs.Remove(marker.gameObject); Destroy(marker.gameObject);
        magObjs.Remove(ghost.gameObject); Destroy(ghost.gameObject);
        SetMagLabel(mag);
    }

    private const string TimingTitleDefault = "초록 구간에서 Z / Enter / Space / 클릭으로 멈추세요!";

    private void BuildTiming()
    {
        var panel = BattleUIFactory.MakeImage("TimingPanel", CanvasRect, ArenaCenter, new Vector2(840f, 220f),
                                              new Color(0f, 0f, 0f, 0.88f));
        timingPanel = panel.gameObject;
        timingTitleText = BattleUIFactory.MakeText("Title", panel.transform, new Vector2(0f, 75f), new Vector2(800f, 40f),
                                 TimingTitleDefault, 26f, Color.white, font);

        BattleUIFactory.MakeImage("Bar", panel.transform, Vector2.zero, new Vector2(TimingBarWidth, 36f),
                                  new Color(0.2f, 0.2f, 0.25f));
        BattleUIFactory.MakeImage("ZoneNormal", panel.transform, Vector2.zero, new Vector2(TimingBarWidth * 0.70f, 36f),
                                  new Color(0.35f, 0.4f, 0.55f));
        BattleUIFactory.MakeImage("ZoneGood", panel.transform, Vector2.zero, new Vector2(TimingBarWidth * 0.35f, 36f),
                                  new Color(0.3f, 0.7f, 0.4f));
        BattleUIFactory.MakeImage("ZonePerfect", panel.transform, Vector2.zero, new Vector2(TimingBarWidth * 0.10f, 36f),
                                  new Color(1f, 0.9f, 0.3f));
        timingMarker = BattleUIFactory.MakeImage("Marker", panel.transform, Vector2.zero, new Vector2(8f, 64f),
                                                 Color.white).rectTransform;

        timingResultText = BattleUIFactory.MakeText("Result", panel.transform, new Vector2(0f, -70f), new Vector2(800f, 50f),
                                                    "", 36f, Color.white, font);
        timingPanel.SetActive(false);
    }

    private void BuildResult()
    {
        var panel = BattleUIFactory.MakeImage("ResultPanel", CanvasRect, Vector2.zero, Vector2.zero,
                                              new Color(0f, 0f, 0f, 0.78f));
        BattleUIFactory.Stretch(panel.rectTransform);
        resultPanel = panel.gameObject;
        resultTitle = BattleUIFactory.MakeText("Title", panel.transform, new Vector2(0f, 50f), new Vector2(1200f, 140f),
                                               "", 96f, Color.white, font);
        BattleUIFactory.MakeText("Sub", panel.transform, new Vector2(0f, -60f), new Vector2(1200f, 60f),
                                 "Z / Enter : 다시 시작", 34f, new Color(0.8f, 0.85f, 1f), font);
        resultPanel.SetActive(false);
    }

    // ───────────────────────── 상태 갱신 ─────────────────────────

    public void RefreshStatus(IList<BattleActor> party, BattleEnemy enemy, int tp, int tpMax, string roundLabel)
    {
        for (int i = 0; i < slots.Count && i < party.Count; i++)
        {
            BattleActor a = party[i];
            PartySlotUI ui = slots[i];
            ui.hpText.text = $"{a.hp}/{a.maxHP}";
            BattleUIFactory.SetBarRatio(ui.hpFill, a.maxHP > 0 ? (float)a.hp / a.maxHP : 0f);
            ui.statusText.text = !a.IsAlive ? "전투불능" : "";
            ui.dead = !a.IsAlive;
            ui.guarding = a.IsAlive && a.guarding;
            ui.lowHp = a.IsAlive && a.maxHP > 0 && a.hp <= a.maxHP * settings.lowHpRatio;
            if (ui.slotIcons != null) ui.slotIcons[0].gameObject.SetActive(a.IsAlive && a.guarding);
            if (ui.mpFill != null) BattleUIFactory.SetBarRatio(ui.mpFill, a.maxMP > 0 ? (float)a.mp / a.maxMP : 0f);
            if (ui.mpText != null) ui.mpText.text = a.maxMP > 0 ? $"MP {a.mp}/{a.maxMP}" : "";
            ui.baseColor = a.IsAlive ? new Color(0.1f, 0.1f, 0.15f, 0.95f) : new Color(0.15f, 0.05f, 0.05f, 0.95f);
            if (ui.shake == null) ui.bg.color = ui.baseColor;
            ui.nameText.color = a.IsAlive ? a.color : new Color(0.5f, 0.5f, 0.5f);
        }

        enemyNameText.text = enemy.name;
        enemyHpText.text = $"{enemy.hp}/{enemy.maxHP}";
        enemyDead = enemy.hp <= 0;
        enemyLowHp = enemy.hp > 0 && enemy.maxHP > 0 && enemy.hp <= enemy.maxHP * settings.lowHpRatio;
        BattleUIFactory.SetBarRatio(enemyHpFill, enemy.maxHP > 0 ? (float)enemy.hp / enemy.maxHP : 0f);

        tpText.text = $"{tp}/{tpMax}";
        BattleUIFactory.SetBarRatio(tpFill, tpMax > 0 ? (float)tp / tpMax : 0f);
        roundText.text = roundLabel;
    }

    public void Say(string text)
    {
        // 줄바꿈: 문구에 \n 또는 <br> 을 쓰면 줄이 바뀐다
        text = (text ?? "").Replace("\\n", "\n").Replace("<br>", "\n");
        messageText.text = text;
        string plain = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", ""); // 아웃라인 복사본은 태그 없이
        foreach (TextMeshProUGUI c in messageOutlineCopies) c.text = plain;
    }

    // 안내 메시지 아웃라인: TMP 내장 아웃라인은 글자 안쪽을 깎아 먹어서 보기 나쁘다.
    // 대신 같은 글자를 outline 색으로 여러 방향(16방향)으로 살짝 밀어 뒤에 깔고 그 위에 원본을 그린다 → 바깥쪽에만 생긴다.
    private readonly List<TextMeshProUGUI> messageOutlineCopies = new List<TextMeshProUGUI>();
    private const int OutlineDirections = 16;

    private void BuildMessageOutline()
    {
        if (!settings.messageOutline || settings.messageOutlineThickness <= 0f) return;

        Color col = settings.messageOutlineColor;
        col.a = 1f;
        RectTransform baseRt = messageText.rectTransform;
        int sibling = baseRt.GetSiblingIndex();
        for (int i = 0; i < OutlineDirections; i++)
        {
            float ang = i * Mathf.PI * 2f / OutlineDirections;
            Vector2 off = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * settings.messageOutlineThickness;
            TextMeshProUGUI copy = BattleUIFactory.MakeText("MessageOutline" + i, messageRoot, baseRt.anchoredPosition + off,
                                                            baseRt.sizeDelta, "", messageText.fontSize, col, font);
            copy.richText = false;
            copy.enableAutoSizing = settings.messageAutoShrink;
            copy.fontSizeMin = Mathf.Min(16f, settings.messageFontSize);
            copy.fontSizeMax = settings.messageFontSize;
            copy.rectTransform.SetSiblingIndex(sibling); // 원본 글자 바로 뒤(아래)에 깐다
            messageOutlineCopies.Add(copy);
        }
    }

    public IEnumerator SayFor(string text, float seconds)
    {
        Say(text);
        yield return new WaitForSeconds(seconds);
    }

    /// <summary>고를 수 없는 메뉴 항목을 눌렀을 때 문구 (BattleMessages.notAvailable).</summary>
    public string NotAvailableText = "지금은 고를 수 없습니다.";

    public void SetHint(string text) { hintText.text = text; }

    /// <summary>행동을 고르는 중인 파티원의 상태창에 노란 테두리(커서)를 표시한다. -1 이면 모두 끈다.</summary>
    public void SetActiveSlot(int index)
    {
        activeSlot = (index >= 0 && index < slots.Count) ? index : -1;
    }

    public void ShakeEnemy()
    {
        FlashEnemyFace(FaceState.Hit, settings.faceHitSeconds); // 피격 이미지
        StartCoroutine(ShakeRoutine());
    }

    /// <summary>피해를 입은 파티원의 상태창을 살짝 흔들고 붉게 번쩍인다.</summary>
    public void ShakePartySlot(int index)
    {
        if (index < 0 || index >= slots.Count) return;
        PartySlotUI s = slots[index];
        if (s.shake != null) StopCoroutine(s.shake);
        s.shake = StartCoroutine(SlotShakeRoutine(s));
    }

    private IEnumerator SlotShakeRoutine(PartySlotUI s)
    {
        const float duration = 0.35f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / duration);          // 점점 약해짐
            s.root.anchoredPosition = s.basePos + new Vector2(Mathf.Sin(t * 90f) * 9f * k, 0f);
            s.bg.color = Color.Lerp(s.baseColor, new Color(0.65f, 0.12f, 0.12f, 0.95f), k);
            yield return null;
        }
        s.root.anchoredPosition = s.basePos;
        s.bg.color = s.baseColor;
        s.shake = null;
    }

    public void FloatAtEnemy(string text, Color color)
    {
        BattleFloatText.Spawn(CanvasRect, EnemyPosition + new Vector2(0f, 40f), text, color, font, 44f);
    }

    private IEnumerator ShakeRoutine()
    {
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            enemyBody.anchoredPosition = EnemyPosition + new Vector2(Mathf.Sin(t * 70f) * 12f, 0f);
            yield return null;
        }
        enemyBody.anchoredPosition = EnemyPosition;
    }

    // ───────────────────────── 입력 헬퍼 ─────────────────────────

    private static bool Pressed(params Key[] keys)
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        foreach (Key k in keys)
            if (kb[k].wasPressedThisFrame) return true;
        return false;
    }

    private static bool PressedConfirm() { return Pressed(Key.Z, Key.Enter, Key.NumpadEnter, Key.Space); }
    private static bool PressedClick() { return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame; }
    private static bool PressedCancel() { return Pressed(Key.X, Key.Escape, Key.Backspace); }
    private static bool PressedUp() { return Pressed(Key.UpArrow, Key.W); }
    private static bool PressedDown() { return Pressed(Key.DownArrow, Key.S); }
    private static bool PressedLeft() { return Pressed(Key.LeftArrow, Key.A); }
    private static bool PressedRight() { return Pressed(Key.RightArrow, Key.D); }

    // ───────────────────────── 커맨드 메뉴 (RPG 만들기 MV식) ─────────────────────────

    /// <summary>
    /// 목록에서 하나를 고른다. ↑↓ 이동, Z/Enter/Space 결정, X/Esc 취소.
    /// 결과는 LastChoice (취소 -1). enabled[i]가 false인 항목은 회색이며 고를 수 없다.
    /// </summary>
    public IEnumerator Choose(string header, IList<string> labels, IList<bool> enabled, bool canCancel, int startIndex = 0)
    {
        LastChoice = -1;
        int n = Mathf.Min(labels.Count, MaxMenuItems);
        if (n == 0) yield break;

        OpenMenu(n);
        menuHeader.text = header;
        SetHint("");
        menuHint.enableAutoSizing = true;
        menuHint.fontSizeMin = 11f;
        menuHint.fontSizeMax = 17f;
        menuHint.text = "↑↓·마우스 선택  Z·클릭 결정" + (canCancel ? "  X·우클릭 취소" : "");
        lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        int cursor = Mathf.Clamp(startIndex, 0, n - 1);
        while (cursor < n - 1 && !enabled[cursor]) cursor++;
        DrawMenu(labels, enabled, n, cursor, true);

        while (true)
        {
            yield return null; // 이 메뉴를 연 키 입력이 같은 프레임에 또 처리되지 않도록 먼저 한 프레임 넘김

            // 마우스: 움직이면 가리키는 항목으로 커서 이동, 좌클릭 = 결정, 우클릭 = 취소
            Mouse mouse = Mouse.current;
            int hover = MenuRowAtMouse(n);
            bool mouseMoved = false;
            if (mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                mouseMoved = mp != lastMouse;
                lastMouse = mp;
            }
            bool leftClick = mouse != null && mouse.leftButton.wasPressedThisFrame && hover >= 0;
            bool rightClick = mouse != null && mouse.rightButton.wasPressedThisFrame;

            if (mouseMoved && hover >= 0 && hover != cursor && enabled[hover])
            {
                cursor = hover;
                DrawMenu(labels, enabled, n, cursor);
            }

            if (leftClick)
            {
                if (enabled[hover]) { cursor = hover; LastChoice = cursor; break; }
                Say(NotAvailableText);
            }
            else if (rightClick && canCancel)
            {
                LastChoice = -1;
                break;
            }
            else if (PressedUp())
            {
                cursor = StepCursor(cursor, -1, enabled, n);
                DrawMenu(labels, enabled, n, cursor);
            }
            else if (PressedDown())
            {
                cursor = StepCursor(cursor, +1, enabled, n);
                DrawMenu(labels, enabled, n, cursor);
            }
            else if (PressedConfirm())
            {
                if (enabled[cursor]) { LastChoice = cursor; break; }
                Say(NotAvailableText);
            }
            else if (canCancel && PressedCancel())
            {
                LastChoice = -1;
                break;
            }
        }

        menuPanel.SetActive(false);
        menuClosedAt = Time.unscaledTime;
    }

    private Vector2 lastMouse;

    /// <summary>마우스가 가리키는 메뉴 항목 번호 (메뉴 밖이거나 빈 칸이면 -1).</summary>
    private int MenuRowAtMouse(int n)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || menuRect == null || !menuPanel.activeInHierarchy) return -1;

        Vector2 local;
        // 오버레이 캔버스라 카메라는 null. 메뉴의 현재(애니메이션 중인) 위치/크기를 그대로 반영한다
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(menuRect, mouse.position.ReadValue(), null, out local))
            return -1;
        if (Mathf.Abs(local.x) > MenuWidth * 0.5f) return -1;

        float yFromTop = menuRect.sizeDelta.y * 0.5f - local.y;
        float rowsTop = MenuTopPad + MenuHeaderHeight + MenuHintHeight;
        if (yFromTop < rowsTop) return -1;
        int row = (int)Mathf.Floor((yFromTop - rowsTop) / MenuRowHeight);
        return (row >= 0 && row < n) ? row : -1;
    }

    private static int StepCursor(int cursor, int dir, IList<bool> enabled, int n)
    {
        for (int i = 0; i < n; i++)
        {
            cursor = (cursor + dir + n) % n;
            if (enabled[cursor]) return cursor;
        }
        return cursor;
    }

    private void DrawMenu(IList<string> labels, IList<bool> enabled, int n, int cursor, bool snapCursor = false)
    {
        for (int i = 0; i < MaxMenuItems; i++)
        {
            if (i >= n) { menuItems[i].text = ""; continue; }
            bool on = enabled[i];
            menuItems[i].text = labels[i];
            menuItems[i].color = !on ? new Color(0.5f, 0.5f, 0.55f)
                               : (i == cursor ? CursorColor : Color.white);
        }
        menuCursorIndex = cursor;
        if (snapCursor) menuBarY = -MenuRowCenter(cursor);
    }

    private float menuClosedAt = -10f;
    private int menuSide;

    /// <summary>메뉴를 연다. 화면 옆(캐릭터 쪽)에서 미끄러져 들어온다. 스킬 메뉴처럼 바로 이어서 열릴 때는 제자리 유지.</summary>
    private void OpenMenu(int n)
    {
        menuCount = n;
        int side = MenuSideOf(activeSlot);
        menuPanel.SetActive(true);
        bool keep = menuPosInit && menuSide == side && Time.unscaledTime - menuClosedAt < 0.2f;
        menuSide = side;
        Vector2 t = MenuTarget();
        menuBottom = t.y;
        menuH = MenuHeight(n);
        if (!keep) menuX = side * (800f + MenuWidth * 0.5f + 20f); // 화면 밖에서 시작
        else menuX = t.x;
        menuPosInit = true;
        menuScale = 1f;
        ApplyMenuTransform();
    }

    private static int MenuSideOf(int slot) { return slot == 0 ? -1 : 1; }

    /// <summary>메뉴가 있어야 할 위치(x = 중심, y = 아래 가장자리). 선택 중인 캐릭터 쪽 화면 옆.</summary>
    private Vector2 MenuTarget()
    {
        float x = MenuSideOf(activeSlot) * (800f - MenuWidth * 0.5f - 20f);
        return new Vector2(x, MenuBottomY);
    }

    private void ApplyMenuTransform()
    {
        menuRect.sizeDelta = new Vector2(MenuWidth, menuH);
        menuRect.anchoredPosition = new Vector2(menuX, menuBottom + menuH * 0.5f);
        menuRect.localScale = Vector3.one * menuScale;
        menuCursorRt.anchoredPosition = new Vector2(0f, menuBarY);
    }

    private static float Smooth(float cur, float target, float k, float dt)
    {
        float v = Mathf.Lerp(cur, target, 1f - Mathf.Exp(-k * dt));
        return Mathf.Abs(v - target) < 0.05f ? target : v;
    }

    /// <summary>매 프레임: 선택 테두리와 명령 메뉴를 목표 위치로 부드럽게 보낸다.</summary>
    private void UpdateCursorAnimations(float dt)
    {
        // 상태창 선택 테두리
        if (partyFrame != null)
        {
            if (activeSlot >= 0 && activeSlot < slots.Count)
            {
                Vector2 target = slots[activeSlot].basePos;
                if (!partyFrameShown) { partyFrameCur = target; partyFrameShown = true; } // 처음 나타날 땐 제자리에서 페이드인
                partyFrameCur = new Vector2(Smooth(partyFrameCur.x, target.x, 16f, dt), Smooth(partyFrameCur.y, target.y, 16f, dt));
                partyFrameAlpha = Smooth(partyFrameAlpha, 1f, 14f, dt);
            }
            else
            {
                partyFrameShown = false;
                partyFrameAlpha = Smooth(partyFrameAlpha, 0f, 14f, dt);
            }

            bool visible = partyFrameAlpha > 0.01f;
            if (partyFrame.gameObject.activeSelf != visible) partyFrame.gameObject.SetActive(visible);
            if (visible)
            {
                partyFrame.anchoredPosition = partyFrameCur;
                Color c = CursorColor;
                c.a = partyFrameAlpha * (0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.time * 5f)));
                for (int e = 0; e < 4; e++) partyFrameEdges[e].color = c;
            }
        }

        // 명령 메뉴
        if (menuPanel != null && menuPanel.activeSelf)
        {
            Vector2 t = MenuTarget();
            // 가로 이동은 선형(일정한 속도)
            float slideSpeed = (800f + MenuWidth) / Mathf.Max(0.05f, settings.menuSlideSeconds);
            menuX = Mathf.MoveTowards(menuX, t.x, slideSpeed * dt);
            menuBottom = t.y;
            menuH = Smooth(menuH, MenuHeight(menuCount), 18f, dt);
            menuScale = 1f;
            menuBarY = Smooth(menuBarY, -MenuRowCenter(menuCursorIndex), 24f, dt);
            ApplyMenuTransform();
        }
    }

    // ───────────────────────── 타이밍 타격 (언더테일식) ─────────────────────────

    /// <summary>
    /// 막대 위를 왕복하는 표시를 Z/Enter/Space로 멈춘다. 가운데에 가까울수록 높은 등급.
    /// 결과는 LastGrade. 시간 안에 못 누르면 Miss.
    /// </summary>
    public IEnumerator RunTiming(float sweepsPerSecond, float timeoutSeconds)
    {
        timingPanel.SetActive(true);
        timingMarker.gameObject.SetActive(true); // 탄창 사격(RunTimingStream)이 꺼놨을 수 있으니 기본 커서를 다시 켠다
        timingTitleText.text = TimingTitleDefault;
        timingResultText.text = "";
        SetHint("");

        float half = TimingBarWidth * 0.5f;
        float phase = 0f;
        float elapsed = 0f;
        float distance = 1f; // 0 = 정중앙, 1 = 가장자리
        bool stopped = false;
        timingMarker.anchoredPosition = new Vector2(-half, 0f);

        while (true)
        {
            yield return null;
            float dt = Time.deltaTime;
            phase += sweepsPerSecond * dt;
            elapsed += dt;

            float x = (Mathf.PingPong(phase, 1f) - 0.5f) * TimingBarWidth;
            timingMarker.anchoredPosition = new Vector2(x, 0f);

            if (PressedConfirm() || PressedClick())
            {
                distance = Mathf.Abs(x) / half;
                stopped = true;
                break;
            }
            if (elapsed >= timeoutSeconds) break;
        }

        if (!stopped) LastGrade = TimingGrade.Miss;
        else if (distance <= 0.10f) LastGrade = TimingGrade.Perfect;
        else if (distance <= 0.35f) LastGrade = TimingGrade.Good;
        else if (distance <= 0.70f) LastGrade = TimingGrade.Normal;
        else LastGrade = TimingGrade.Miss;

        timingResultText.text = GradeLabel(LastGrade);
        timingResultText.color = GradeColor(LastGrade);
        yield return new WaitForSeconds(0.6f);
        timingPanel.SetActive(false);
    }


    /// <summary>
    /// 탄창 사격: 탄 하나당 커서 하나가 왼쪽에서 줄 서서 연달아 나와 막대를 가로지른다.
    /// 누를 때마다 가장 앞(오른쪽)에 있는 커서가 판정되고, 끝까지 가도록 못 누른 커서는 Miss.
    /// onResolve(번호, 등급) 은 커서 하나가 판정될 때마다 불리며, true 를 돌려주면 남은 커서를 접고 끝낸다 (적이 쓰러졌을 때).
    /// </summary>
    public IEnumerator RunTimingStream(IList<BulletDef> bullets, float crossSeconds, float interval, System.Func<int, TimingGrade, bool> onResolve)
    {
        int n = bullets.Count;
        timingPanel.SetActive(true);
        timingMarker.gameObject.SetActive(false); // 탄창 사격 때는 탄마다 생기는 색깔 커서만 쓰고, 기본 흰색 커서는 숨긴다
        timingResultText.text = "";
        SetHint("");
        var names = new List<string>();
        foreach (BulletDef b in bullets) names.Add(b.name);
        timingTitleText.text = string.Join(" → ", names);

        float half = TimingBarWidth * 0.5f;
        crossSeconds = Mathf.Max(0.2f, crossSeconds);
        interval = Mathf.Max(0.05f, interval);

        var cursors = new RectTransform[n];
        for (int i = 0; i < n; i++)
        {
            Image img = BattleUIFactory.MakeImage("BulletCursor" + (i + 1), timingPanel.transform, new Vector2(-half, 0f),
                                                  new Vector2(8f, 64f), bullets[i].CursorColor);
            img.raycastTarget = false;
            cursors[i] = img.rectTransform;
            cursors[i].gameObject.SetActive(false);
        }

        int next = 0;      // 아직 판정 안 된 가장 앞 커서
        float t = 0f;
        bool abort = false;
        while (next < n && !abort)
        {
            yield return null;
            t += Time.deltaTime;

            for (int i = next; i < n; i++)
            {
                float age = t - i * interval;
                if (age < 0f) break;
                cursors[i].gameObject.SetActive(true);
                cursors[i].anchoredPosition = new Vector2(-half + age / crossSeconds * TimingBarWidth, 0f);
            }

            bool pressed = PressedConfirm() || PressedClick();
            float frontAge = t - next * interval;
            bool passed = frontAge >= crossSeconds;
            if (!pressed && !passed) continue;

            TimingGrade g = TimingGrade.Miss;
            if (pressed && frontAge >= 0f && !passed)
            {
                float d = Mathf.Abs(-half + frontAge / crossSeconds * TimingBarWidth) / half;
                if (d <= 0.10f) g = TimingGrade.Perfect;
                else if (d <= 0.35f) g = TimingGrade.Good;
                else if (d <= 0.70f) g = TimingGrade.Normal;
            }
            else if (pressed && frontAge < 0f) continue; // 아직 나오지 않은 커서 — 입력 무시

            cursors[next].gameObject.SetActive(false);
            timingResultText.text = GradeLabel(g);
            timingResultText.color = GradeColor(g);
            if (onResolve != null && onResolve(next, g)) abort = true;
            next++;
        }

        yield return new WaitForSeconds(0.6f);
        for (int i = 0; i < n; i++) if (cursors[i] != null) Destroy(cursors[i].gameObject);
        timingPanel.SetActive(false);
    }

    public static string GradeLabel(TimingGrade g)
    {
        switch (g)
        {
            case TimingGrade.Perfect: return "완벽!";
            case TimingGrade.Good: return "좋아!";
            case TimingGrade.Normal: return "보통";
            default: return "빗나감...";
        }
    }

    public static Color GradeColor(TimingGrade g)
    {
        switch (g)
        {
            case TimingGrade.Perfect: return new Color(1f, 0.9f, 0.3f);
            case TimingGrade.Good: return new Color(0.4f, 0.9f, 0.5f);
            case TimingGrade.Normal: return Color.white;
            default: return new Color(0.7f, 0.7f, 0.75f);
        }
    }

    // ───────────────────────── 타자 입력 ─────────────────────────

    /// <param name="on">타자 입력 시작/종료</param>
    /// <param name="instantKey">true면 입력창을 쓰지 않고 자음/모음 키 하나를 누르는 즉시 입력 (Enter 불필요)</param>
    public void SetTypingMode(bool on, bool instantKey = false)
    {
        hintText.gameObject.SetActive(!on);
        typingOn = on;
        instantMode = on && instantKey;

        // 입력창과 Enter 버튼은 정말 필요할 때(단어를 치고 Enter 하는 구간)만 보인다. 아니면 숨기고 안내 메시지가 그 자리로 내려온다
        bool needField = on && !instantKey;
        enterButton.SetActive(needField);

        if (needField)
        {
            typingRoot.SetActive(true);
            inputField.text = "";
            placeholderText.text = "단어를 입력하고 Enter";
            inputField.interactable = true;
            StartCoroutine(FocusNextFrame());
        }
        else
        {
            if (typingRoot.activeSelf) inputField.DeactivateInputField(); // 비활성화하기 전에 호출
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == typingRoot)
                EventSystem.current.SetSelectedGameObject(null);
            typingRoot.SetActive(false);
        }
    }

    private IEnumerator FocusNextFrame()
    {
        yield return null; // 활성화된 직후 한 프레임 뒤에 포커스를 줘야 안정적
        if (!typingRoot.activeInHierarchy) yield break;

        // Enter 직후 IME가 뒤늦게 확정한 글자가 입력창에 남아 있을 수 있으니 한 번 더 비운다
        inputField.text = "";
        lastLive = "";

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(typingRoot);
        inputField.ActivateInputField();
    }

    // ── 한글 IME 대응 ──────────────────────────────────────────────
    // 한글 IME는 "마지막 글자"를 조합 중인 상태로 들고 있다가 다음 글자를 칠 때 확정한다.
    // Enter가 눌리는 순간 TMP_InputField.text 에는 이 조합 중인 글자가 아직 없어서
    // "ㄱ"이나 "가"만 치면 빈 입력처럼 보이고, 뒤에 글자를 하나 더 붙여야(ㄱㄱ, 가ㅏ) 앞 글자가 인식됐다.
    // → 확정된 글자 + 조합 중인 글자(화면에 보이는 글자)를 합쳐서 판정에 쓴다.

    private string lastLive = "";

    private void Update()
    {
        UpdateCursorAnimations(Time.deltaTime);
        UpdateFaces();

        float wantY = (typingOn && !instantMode) ? MessageYEWithInput : MessageYENoInput;
        if (!Mathf.Approximately(messageYE, wantY))
            messageYE = Mathf.MoveTowards(messageYE, wantY, Mathf.Abs(MessageYEWithInput - MessageYENoInput) / 0.2f * Time.deltaTime);
        UpdateMessagePosition();

        // 자리연습 모드: 눌린 자모 키를 IME 없이 직접 읽어서 바로 제출한다
        if (instantMode && typingOn)
        {
            string jamo = HangulKeyboard.ReadJamoPressedThisFrame();
            if (jamo != null)
            {
                Debug.Log($"[Battle] 키 입력(Enter 없음) → '{jamo}'");
                WordSubmitted?.Invoke(jamo);
            }
            return;
        }

        // 타자 입력 중에는 매 프레임 "화면에 보이는 글자(조합 중인 글자 포함)"를 기록해 둔다.
        // Enter는 이 스크립트의 Update보다 먼저 처리되므로, 제출 시점에는 직전 프레임의 기록이 가장 믿을 만하다.
        if (typingRoot != null && typingRoot.activeInHierarchy && inputField != null)
            lastLive = ReadLiveText();
    }

    private string ReadLiveText()
    {
        string committed = inputField.text ?? "";

        string composing = "";
        try { composing = Input.compositionString ?? ""; }
        catch (Exception) { composing = ""; } // 구 Input Manager가 꺼진 설정이면 예외 → 아래 보조 수단 사용

        string live = committed + composing;

        // 보조: 입력창이 지금 화면에 그리고 있는 글자 (조합 중인 글자 포함)
        if (inputField.textComponent != null)
        {
            string shown = CleanShown(inputField.textComponent.GetParsedText());
            if (shown.Length > live.Length) live = shown;
        }
        return live;
    }

    private static string CleanShown(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c == '\u200B' || c == '\u200C' || c == '\u200D' || c == '\uFEFF' || char.IsControl(c)) continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string Longest(string a, string b, string c)
    {
        string best = a ?? "";
        if (b != null && b.Length >= best.Length) best = b;
        if (c != null && c.Length >= best.Length) best = c;
        return best;
    }

    private void OnFieldSubmit(string value)
    {
        string now = ReadLiveText();
        string typed = Longest(value, lastLive, now);
        Debug.Log($"[Battle] Enter 입력 — 확정된 글자='{value}' / 직전 프레임='{lastLive}' / 지금='{now}' → 판정에 사용='{typed}'");

        WordSubmitted?.Invoke(typed);

        lastLive = "";
        inputField.text = "";
        // TMP_InputField는 Enter를 누르면 포커스를 놓기 때문에 다음 프레임에 다시 잡아준다
        if (typingRoot.activeInHierarchy) StartCoroutine(FocusNextFrame());
    }

    /// <summary>입력 결과 피드백. 맞으면 초록, 틀리면 빨강 + 흔들림.</summary>
    public void FlashInput(bool ok)
    {
        StartCoroutine(FlashInputRoutine(ok));
    }

    private IEnumerator FlashInputRoutine(bool ok)
    {
        if (instantMode)
        {
            // 입력창이 숨겨져 있으니 안내 메시지가 대신 번쩍/흔들린다
            Color rgbI = ok ? settings.inputCorrectColor : settings.inputWrongColor;
            float durI = Mathf.Max(0.05f, settings.inputFlashSeconds);
            for (float ti = 0f; ti < durI; ti += Time.deltaTime)
            {
                messageText.color = Color.Lerp(rgbI, Color.white, ti / durI);
                messageShakeX = ok ? 0f : Mathf.Sin(ti * 90f) * 10f;
                yield return null;
            }
            messageText.color = Color.white;
            messageShakeX = 0f;
            yield break;
        }
        Vector2 basePos = TypingPos;
        Color rgb = ok ? settings.inputCorrectColor : settings.inputWrongColor;
        float dur = Mathf.Max(0.05f, settings.inputFlashSeconds);
        float aMax = Mathf.Max(settings.inputFlashAlphaMin, settings.inputFlashAlphaMax);
        float aMin = Mathf.Min(settings.inputFlashAlphaMin, settings.inputFlashAlphaMax);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            inputBg.color = new Color(rgb.r, rgb.g, rgb.b, Mathf.Lerp(aMax, aMin, Mathf.Clamp01(t / dur)));
            typingRect.anchoredPosition = ok ? basePos : basePos + new Vector2(Mathf.Sin(t * 90f) * 10f, 0f);
            yield return null;
        }
        inputBg.color = new Color(0f, 0f, 0f, 0.8f);
        typingRect.anchoredPosition = basePos;
    }

    // ───────────────────────── 결과 ─────────────────────────

    public void ShowResult(bool victory)
    {
        resultTitle.text = victory ? "승리!" : "패배...";
        resultTitle.color = victory ? new Color(1f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.45f);
        resultPanel.SetActive(true);
    }

    public void HideResult() { resultPanel.SetActive(false); }

    public IEnumerator WaitForConfirm()
    {
        while (true)
        {
            yield return null;
            if (PressedConfirm()) yield break;
        }
    }
}
