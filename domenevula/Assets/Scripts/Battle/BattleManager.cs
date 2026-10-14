using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// 전투 한 판의 진행을 맡는다 (RPG 만들기 MV식 턴제).
///
///   라운드 시작
///    1) 커맨드 입력  : 파티원 순서대로 공격 / 스킬 / 방어를 고른다 (X로 한 명 전으로 되돌아감)
///    2) 행동 실행    : 고른 행동을 순서대로 실행. 공격/데미지 스킬은 타이밍 타격으로 배율이 정해진다
///    3) 적 턴        : 칸마다 뜬 단어를 쳐서 탄막을 피한다
///         - 지금 서 있는 칸의 단어 → 이동 없이 적에게 추가 데미지
///         - 다른 칸의 단어         → 그 칸으로 직선 이동 + TP 상승 (이동 중에도 피격 가능)
///   라운드가 지날수록 단어가 어려워진다 (자음 → 자음·모음 → 한 글자 → 두 글자 → 긴 단어)
///
/// 사용법: 이 컴포넌트를 빈 오브젝트에 붙이고 fontAsset 에 한글 TMP 폰트를 넣으면 끝.
/// (메뉴 "도메네뷸라 > 전투 프로토타입 씬 만들기" 가 이걸 자동으로 해준다)
/// 나중에 필드와 연결할 때는 BattleFinished 이벤트(true=승리)를 받으면 된다.
/// </summary>
public class BattleManager : MonoBehaviour
{
    [Header("한글이 들어있는 TMP 폰트 에셋 (예: Cafe24Dongdong SDF)")]
    public TMP_FontAsset fontAsset;

    [Header("전투 배경 (비워두면 단색 배경)")]
    [Tooltip("배경 이미지(Sprite). Project 창에서 이미지를 선택해 Texture Type 을 'Sprite (2D and UI)' 로 바꾼 뒤 끌어다 넣는다")]
    public Sprite battleBackground;
    public BackgroundFit backgroundFit = BackgroundFit.Cover;
    [Tooltip("배경 이미지에 곱해지는 색. 어둡게 하려면 회색/검정 쪽으로, 반투명하게 하려면 A 를 낮춘다")]
    public Color backgroundTint = Color.white;

    [Header("전투 수치 (밸런스 조절은 여기서)")]
    public BattleSettings settings = new BattleSettings();

    [Header("데이터")]
    public List<BattleActor> party = BattleDefaults.Party();
    public BattleEnemy enemy = BattleDefaults.Enemy();
    public List<WordTier> wordTiers = BattleDefaults.WordTiers();

    [Header("안내 문구 (화면 위쪽 글자). {enemy} {actor} 같은 낱말은 상황에 맞게 바뀜")]
    public BattleMessages messages = new BattleMessages();

    [Header("턴 전환 연출 (\"플레이어 턴!\" / \"적 턴!\") — 이미지는 나중에 여기서 교체")]
    public TurnBannerSettings turnBanner = new TurnBannerSettings();

    [Header("라운드별 말풍선 대사 (Settings 의 Speech Balloon Prefab 필요). 라운드가 바뀔 때마다 항목을 추가해서 대사를 바꾼다")]
    public List<RoundLines> roundLines = BattleDefaults.DefaultRoundLines();

    [Header("라운드별 플레이어 턴 안내 문구 (해당하는 항목이 없으면 Enemy 의 Description 이 쓰인다)")]
    public List<RoundMessage> roundMessages = new List<RoundMessage>();

    [Header("라운드별 칸 배치 (비워두면 Settings 의 Columns × Rows 고정)")]
    public List<RoundLayout> roundLayouts = new List<RoundLayout>
    {
        new RoundLayout(1, 3, 2),
        new RoundLayout(3, 3, 3),
    };

    /// <summary>전투가 끝났을 때. true = 승리, false = 패배.</summary>
    public event Action<bool> BattleFinished;

    private class PlannedAction
    {
        public BattleActor actor;
        public ActionKind kind;
        public int skillIndex;
        public bool cancelled;
    }

    private BattleUI ui;
    private BattleArena arena;
    private BattlePatternRunner runner;
    private BattleWordBoard board;

    private readonly List<PlannedAction> plans = new List<PlannedAction>();
    private int tp;
    private int round;
    private int tpGainedThisTurn;
    private bool battleOver;
    private bool victory;
    private bool enemyPhaseActive;

    // ───────────────────────── 시작 ─────────────────────────

    private void Start()
    {
        EnsureEventSystem();
        BuildAll();
        StartCoroutine(Run());
    }

    private static void EnsureEventSystem()
    {
        // TMP_InputField가 키 입력을 받으려면 EventSystem이 필요하다
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildAll()
    {
        ui = new GameObject("BattleUI").AddComponent<BattleUI>();
        ui.transform.SetParent(transform, false);
        ResetState();
        ui.Build(fontAsset, settings, party, enemy);
        ui.SetBackground(battleBackground, backgroundFit, backgroundTint);
        ui.NotAvailableText = messages.notAvailable;

        var arenaGo = new GameObject("Arena", typeof(RectTransform));
        arenaGo.transform.SetParent(ui.ArenaSlot, false);
        arena = arenaGo.AddComponent<BattleArena>();
        arena.Build(settings, fontAsset, ui.ArenaCenter);
        arena.SoulHit += OnSoulHit;
        ui.BindArena(arena);

        runner = gameObject.AddComponent<BattlePatternRunner>();
        board = new BattleWordBoard(wordTiers, arena.CellCount, Environment.TickCount);
        board.Warn = msg => Debug.LogWarning("[Battle] " + msg);

        ui.WordSubmitted += OnWordSubmitted;
    }

    private void ResetState()
    {
        foreach (BattleActor a in party) { a.hp = a.maxHP; a.mp = a.maxMP; a.guarding = false; }
        enemy.hp = enemy.maxHP;
        tp = 0;
        round = 0;
        battleOver = false;
        victory = false;
        enemyPhaseActive = false;
        plans.Clear();
    }

    private void OnDestroy()
    {
        if (arena != null) arena.SoulHit -= OnSoulHit;
        if (ui != null) ui.WordSubmitted -= OnWordSubmitted;
    }

    // ───────────────────────── 전체 흐름 ─────────────────────────

    private IEnumerator Run()
    {
        yield return null; // UI가 한 프레임 자리 잡을 시간

        while (true)
        {
            ResetState();
            arena.SetRunning(false);
            arena.ResetSoul();
            ui.HideResult();
            ui.SetLayoutInstant(false); // 플레이어 턴 배치로 시작
            Refresh();
            yield return ui.SayFor(BattleMessages.Fill(messages.encounter, enemy: enemy.name), 1.2f);

            while (!battleOver)
            {
                round++;
                Debug.Log($"[Battle] 라운드 {round} 플레이어 턴 시작 (이전 칸 {arena.Columns}x{arena.Rows})");
                yield return ui.ShowTurnBanner(turnBanner, turnBanner.playerTurn);
                yield return ui.PlayLayoutTransition(false, settings.layoutTransitionSeconds); // 배너가 끝나면 UI가 플레이어 턴 배치로 이동 (격자는 납작하게 접힘)
                yield return ApplyLayoutForRoundAnimated(); // 납작한 상태에서 칸 수가 새 크기로 부드럽게 바뀐다
                Debug.Log($"[Battle] 라운드 {round} 칸 배치 — {arena.Columns}x{arena.Rows}");
                yield return PlayerTurnStartLines(); // UI 이동이 끝난 뒤 잠깐 대사 (이때는 행동 메뉴가 아직 안 열린다)
                yield return CommandPhase();
                if (battleOver) break;

                yield return ActionPhase();
                if (battleOver) break;

                yield return EnemyReaction(); // 플레이어 턴이 끝나면 적이 말풍선으로 한마디

                yield return ui.ShowTurnBanner(turnBanner, turnBanner.enemyTurn);
                yield return EnemyPhase();
            }

            yield return Finish();
        }
    }

    private IEnumerator Finish()
    {
        runner.Cancel();
        arena.SetRunning(false);
        ui.SetTypingMode(false);
        Refresh();

        BattleFinished?.Invoke(victory);
        ui.Say(victory ? BattleMessages.Fill(messages.victory, enemy: enemy.name) : BattleMessages.Fill(messages.defeat, enemy: enemy.name));
        yield return new WaitForSeconds(0.8f);
        ui.ShowResult(victory);
        yield return ui.WaitForConfirm();
    }

    private void Refresh()
    {
        string tierName = board != null ? board.TierName : "-";
        string label = round <= 0 ? "" : $"라운드 {round}  ·  단어: {tierName}";
        ui.RefreshStatus(party, enemy, tp, settings.tpMax, label);
    }

    // ───────────────────────── 1) 커맨드 입력 ─────────────────────────

    private IEnumerator CommandPhase()
    {
        plans.Clear();
        foreach (BattleActor a in party) a.guarding = false;
        Refresh();
        ui.Say(BattleMessages.Fill(PlayerTurnMessage(), enemy: enemy.name)); // 플레이어 턴 시작: 안내 문구

        var order = party.FindAll(a => a.IsAlive);
        int i = 0;
        while (i < order.Count)
        {
            ui.SetActiveSlot(party.IndexOf(order[i]));
            var plan = new PlannedAction { actor = order[i] };
            int availableTp = tp - PlannedTpCost();
            ShowWaitingLine(order[i]); // 고르는 동안 떠 있는 말풍선 대사
            yield return PlanOne(plan, availableTp);
            ui.HideWaitBalloon();

            if (plan.cancelled)
            {
                if (i > 0)
                {
                    i--;
                    plans.RemoveAt(plans.Count - 1);
                }
                continue;
            }
            plans.Add(plan);
            i++;
        }
        ui.SetActiveSlot(-1);
    }

    /// <summary>이번 라운드의 플레이어 턴 안내 문구. 라운드별 설정이 없으면 적 설명.</summary>
    private string PlayerTurnMessage()
    {
        string text = enemy.description;
        int best = int.MinValue;
        if (roundMessages != null)
        {
            foreach (RoundMessage m in roundMessages)
            {
                if (m == null || m.fromRound > round || m.fromRound < best) continue;
                best = m.fromRound; text = m.text;
            }
        }
        return text;
    }

    private int PlannedTpCost()
    {
        int sum = 0;
        foreach (PlannedAction p in plans)
            if (p.kind == ActionKind.Skill) sum += p.actor.skills[p.skillIndex].tpCost;
        return sum;
    }

    private IEnumerator PlanOne(PlannedAction plan, int availableTp)
    {
        BattleActor actor = plan.actor;
        bool hasSkills = actor.skills != null && actor.skills.Count > 0;

        while (true)
        {
            var labels = new List<string> { "공격", "스킬", "방어" };
            var enabled = new List<bool> { true, hasSkills, true };
            yield return ui.Choose($"{actor.name}의 행동", labels, enabled, true);

            int pick = ui.LastChoice;
            if (pick < 0) { plan.cancelled = true; yield break; }

            if (pick == 0) { plan.kind = ActionKind.Attack; yield break; }
            if (pick == 2) { plan.kind = ActionKind.Guard; yield break; }

            // 스킬 목록
            var skillLabels = new List<string>();
            var skillEnabled = new List<bool>();
            foreach (BattleSkill sk in actor.skills)
            {
                skillLabels.Add($"{sk.name}  (TP {sk.tpCost})");
                skillEnabled.Add(availableTp >= sk.tpCost);
            }
            yield return ui.Choose($"스킬  (사용 가능 TP {availableTp})", skillLabels, skillEnabled, true);

            int skill = ui.LastChoice;
            if (skill < 0) continue; // 스킬창에서 취소 → 다시 커맨드 선택
            plan.kind = ActionKind.Skill;
            plan.skillIndex = skill;
            yield break;
        }
    }

    // ───────────────────────── 2) 행동 실행 ─────────────────────────

    private IEnumerator ActionPhase()
    {
        foreach (PlannedAction p in plans)
        {
            if (battleOver) yield break;
            if (!p.actor.IsAlive) continue;

            yield return PlayerLine(p); // 행동 시작 전 말풍선 대사

            switch (p.kind)
            {
                case ActionKind.Guard:
                    p.actor.guarding = true;
                    Refresh();
                    yield return ui.SayFor(BattleMessages.Fill(messages.guard, enemy: enemy.name, actor: p.actor.name), 0.8f);
                    break;

                case ActionKind.Attack:
                    yield return DoDamage(p.actor, "공격", 1f, true);
                    break;

                case ActionKind.Skill:
                    yield return DoSkill(p.actor, p.actor.skills[p.skillIndex]);
                    break;
            }

            if (enemy.hp <= 0)
            {
                battleOver = true;
                victory = true;
                yield break;
            }
        }
    }

    // ───────────────────────── 말풍선 대사 ─────────────────────────

    /// <summary>이번 라운드에 적용되는 대사 묶음 (없으면 null).</summary>
    private RoundLines CurrentLines()
    {
        RoundLines found = null;
        int best = int.MinValue;
        if (roundLines != null)
        {
            foreach (RoundLines l in roundLines)
            {
                if (l == null || l.fromRound > round || l.fromRound < best) continue;
                best = l.fromRound; found = l;
            }
        }
        return found;
    }

    /// <summary>플레이어 턴 시작 대사: 살아 있는 아군(Turn Start)과 적(Enemy At Player Turn Start)이 동시에 말한다.</summary>
    private IEnumerator PlayerTurnStartLines()
    {
        RoundLines lines = CurrentLines();
        if (lines == null) yield break;
        var specs = new List<BattleUI.BalloonSpec>();
        if (lines.party != null)
            foreach (BattleActor a in party)
            {
                if (!a.IsAlive) continue;
                ActorLine al = lines.party.Find(x => x != null && x.actorName == a.name);
                if (al != null) specs.Add(BattleUI.BalloonSpec.Party(party.IndexOf(a), BattleMessages.Fill(al.turnStart, enemy: enemy.name, actor: a.name)));
            }
        specs.Add(BattleUI.BalloonSpec.Enemy(BattleMessages.Fill(lines.enemyAtPlayerTurnStart, enemy: enemy.name)));
        yield return ui.BalloonGroup(specs);
    }

    /// <summary>적 턴 시작 대사: 적(Enemy At Enemy Turn Start)과 살아 있는 아군(Enemy Turn Start)이 동시에 말한다.</summary>
    private IEnumerator EnemyTurnStartLines()
    {
        RoundLines lines = CurrentLines();
        if (lines == null) yield break;
        var specs = new List<BattleUI.BalloonSpec>();
        if (lines.party != null)
            foreach (BattleActor a in party)
            {
                if (!a.IsAlive) continue;
                ActorLine al = lines.party.Find(x => x != null && x.actorName == a.name);
                if (al != null) specs.Add(BattleUI.BalloonSpec.Party(party.IndexOf(a), BattleMessages.Fill(al.enemyTurnStart, enemy: enemy.name, actor: a.name), settings.hidePartyTailAtEnemyTurnStart));
            }
        specs.Add(BattleUI.BalloonSpec.Enemy(BattleMessages.Fill(lines.enemyAtEnemyTurnStart, enemy: enemy.name)));
        yield return ui.BalloonGroup(specs);
    }

    private void ShowWaitingLine(BattleActor actor)
    {
        RoundLines lines = CurrentLines();
        if (lines == null || lines.party == null) return;
        ActorLine al = lines.party.Find(a => a != null && a.actorName == actor.name);
        if (al == null) return;
        ui.ShowWaitBalloon(party.IndexOf(actor), BattleMessages.Fill(al.waiting, enemy: enemy.name, actor: actor.name));
    }

    private IEnumerator PlayerLine(PlannedAction p)
    {
        RoundLines lines = CurrentLines();
        if (lines == null || lines.party == null) yield break;

        ActorLine al = lines.party.Find(a => a != null && a.actorName == p.actor.name);
        if (al == null) yield break;

        string text = "";
        string skillName = "";
        switch (p.kind)
        {
            case ActionKind.Guard: text = al.guard; break;
            case ActionKind.Attack: text = al.attack; break;
            case ActionKind.Skill:
                BattleSkill sk = p.actor.skills[p.skillIndex];
                if (tp < sk.tpCost) yield break; // TP가 모자라 못 쓰는 스킬은 대사도 없음
                text = al.skill;
                skillName = sk.name;
                break;
        }
        text = BattleMessages.Fill(text, enemy: enemy.name, actor: p.actor.name, skill: skillName, action: skillName);
        yield return ui.PartyBalloon(party.IndexOf(p.actor), text);
    }

    private IEnumerator EnemyReaction()
    {
        RoundLines lines = CurrentLines();
        if (lines == null) yield break;
        yield return ui.EnemyBalloon(BattleMessages.Fill(lines.enemyReaction, enemy: enemy.name));
    }

    private IEnumerator DoSkill(BattleActor actor, BattleSkill skill)
    {
        if (tp < skill.tpCost)
        {
            yield return ui.SayFor(BattleMessages.Fill(messages.notEnoughTp, enemy: enemy.name), 0.8f);
            yield break;
        }
        tp -= skill.tpCost;
        Refresh();

        if (skill.kind == SkillKind.Heal)
        {
            BattleActor target = LowestHpRatioAlly();
            int amount = Mathf.RoundToInt(skill.power);
            yield return ui.SayFor(BattleMessages.Fill(messages.skillUse, enemy: enemy.name, actor: actor.name, skill: skill.name), 0.6f);
            target.hp = Mathf.Min(target.maxHP, target.hp + amount);
            Refresh();
            yield return ui.SayFor(BattleMessages.Fill(messages.heal, enemy: enemy.name, actor: actor.name, skill: skill.name, target: target.name, amount: amount), 0.9f);
        }
        else
        {
            yield return DoDamage(actor, skill.name, skill.power, skill.useTiming);
        }
    }

    private IEnumerator DoDamage(BattleActor actor, string actionName, float power, bool useTiming)
    {
        yield return ui.SayFor(BattleMessages.Fill(messages.actionUse, enemy: enemy.name, actor: actor.name, action: actionName, skill: actionName), 0.5f);

        TimingGrade grade = TimingGrade.Normal;
        float mult = 1f;
        if (useTiming)
        {
            yield return ui.RunTiming(settings.timingSweepsPerSecond, settings.timingTimeoutSeconds);
            grade = ui.LastGrade;
            mult = GradeMultiplier(grade);
        }

        int damage = Mathf.Max(1, Mathf.RoundToInt(actor.attack * power * mult));
        enemy.hp = Mathf.Max(0, enemy.hp - damage);
        ui.ShakeEnemy();
        ui.FloatAtEnemy(damage.ToString(), BattleUI.GradeColor(grade));
        Refresh();

        string prefix = useTiming ? BattleUI.GradeLabel(grade) + " " : "";
        yield return ui.SayFor(BattleMessages.Fill(messages.damageDealt, enemy: enemy.name, actor: actor.name, action: actionName, skill: actionName, grade: prefix, damage: damage), 0.9f);
    }

    private float GradeMultiplier(TimingGrade g)
    {
        switch (g)
        {
            case TimingGrade.Perfect: return settings.perfectMultiplier;
            case TimingGrade.Good: return settings.goodMultiplier;
            case TimingGrade.Normal: return settings.normalMultiplier;
            default: return settings.missMultiplier;
        }
    }

    private BattleActor LowestHpRatioAlly()
    {
        BattleActor best = null;
        float bestRatio = float.MaxValue;
        foreach (BattleActor a in party)
        {
            if (!a.IsAlive) continue;
            float ratio = (float)a.hp / a.maxHP;
            if (ratio < bestRatio) { bestRatio = ratio; best = a; }
        }
        return best ?? party[0];
    }

    // ───────────────────────── 3) 적 턴 (탄막 + 타자) ─────────────────────────

    /// <summary>이번 라운드의 칸 배치(열, 행)를 고른다.</summary>
    private void ResolveLayout(out int c, out int r)
    {
        c = settings.columns; r = settings.rows;
        int best = int.MinValue;
        if (roundLayouts != null)
        {
            foreach (RoundLayout l in roundLayouts)
            {
                if (l == null || l.fromRound > round || l.fromRound < best) continue;
                best = l.fromRound; c = l.columns; r = l.rows;
            }
        }
        c = Mathf.Max(1, c); r = Mathf.Max(1, r);
    }

    private void RebuildBoard()
    {
        int keepTier = board != null ? board.Tier : 0;
        board = new BattleWordBoard(wordTiers, arena.CellCount, Environment.TickCount);
        board.SetTier(keepTier);
        board.Warn = msg => Debug.LogWarning("[Battle] " + msg);
    }

    /// <summary>칸 배치가 달라졌으면 즉시 다시 만든다 (안전장치. 평소엔 아래 애니메이션 버전이 먼저 처리한다).</summary>
    private void ApplyLayoutForRound()
    {
        int c, r;
        ResolveLayout(out c, out r);
        if (c == arena.Columns && r == arena.Rows) return;
        arena.Relayout(c, r);
        RebuildBoard();
    }

    /// <summary>칸 배치가 달라졌으면 격자가 새 크기로 부드럽게 바뀐다 (납작한 플레이어 턴 상태에서).</summary>
    private IEnumerator ApplyLayoutForRoundAnimated()
    {
        int c, r;
        ResolveLayout(out c, out r);
        if (c == arena.Columns && r == arena.Rows) yield break;
        yield return arena.RelayoutAnimated(c, r, settings.gridMorphSeconds);
        RebuildBoard();
    }

    private IEnumerator EnemyPhase()
    {
        int tier = Mathf.Min(wordTiers.Count - 1,
                             settings.startTier + (round - 1) / Mathf.Max(1, settings.roundsPerTier));
        ApplyLayoutForRound();
        Debug.Log($"[Battle] 라운드 {round} 적 턴 시작 — 칸 {arena.Columns}x{arena.Rows}, 단어 단계 {tier}");
        board.SetTier(tier);
        board.FillAll();
        for (int i = 0; i < arena.CellCount; i++) arena.SetWord(i, board.Words[i]);
        arena.ResetSoul();

        tpGainedThisTurn = 0;
        enemyPhaseActive = true;
        Refresh();

        bool instant = UsesInstantKey(wordTiers[tier]);
        ui.Say(BattleMessages.Fill(instant ? messages.enemyTurnKey : messages.enemyTurnWord, enemy: enemy.name));

        // UI 이동 애니메이션이 모두 끝난 뒤, 약간의 텀을 두고 공격을 시작한다
        yield return ui.PlayLayoutTransition(true, settings.layoutTransitionSeconds);
        yield return EnemyTurnStartLines(); // UI 이동이 끝난 뒤, 공격 시작 전에 잠깐 대사
        if (settings.enemyAttackDelaySeconds > 0f) yield return new WaitForSeconds(settings.enemyAttackDelaySeconds);

        arena.SetRunning(true);
        ui.SetTypingMode(true, instant);
        runner.Begin(arena, enemy.patterns, settings.enemyTurnSeconds, settings.patternGapSeconds,
                     settings.bulletSpeedScale, settings.patternTempoScale);

        while (runner.IsRunning && !battleOver) yield return null;

        // 턴이 끝나면 남은 탄/경고를 전부 지운다 (입력이 막힌 뒤에 도착하는 억울한 피격 방지)
        runner.Cancel();
        arena.SetRunning(false);
        ui.SetTypingMode(false);
        enemyPhaseActive = false;

        foreach (BattleActor a in party) a.guarding = false;
        Refresh();
        ui.Say("");
        Debug.Log($"[Battle] 라운드 {round} 적 턴 종료 (전투 종료={battleOver})");
    }

    /// <summary>이 단계를 Enter 없이 키 하나로 입력하는 자리연습 방식으로 할지.</summary>
    private static bool UsesInstantKey(WordTier tier)
    {
        bool allTypable = tier.words != null && tier.words.Length > 0;
        if (allTypable)
        {
            foreach (string w in tier.words)
            {
                if (!HangulKeyboard.IsTypable(w)) { allTypable = false; break; }
            }
        }

        switch (tier.instantKey)
        {
            case InstantKeyMode.On:
                if (!allTypable)
                    Debug.LogWarning($"[Battle] 단계 '{tier.name}'이 Instant Key=On인데 낱자 자음/모음이 아닌 단어가 있습니다. 그 단어는 칠 수 없습니다.");
                return true;
            case InstantKeyMode.Off:
                return false;
            default:
                return allTypable;
        }
    }

    private void OnWordSubmitted(string typed)
    {
        if (!enemyPhaseActive || battleOver) return;

        int idx = board.Match(typed, settings.allowIncompleteLastChar);
        Debug.Log($"[Battle] 입력 '{typed}' → 칸 {idx}  (현재 단어: {string.Join(", ", board.Words)})");

        if (idx < 0)
        {
            ui.FlashInput(false); // 잘못 친 입력: 효과 없음 + 빨간 흔들림
            return;
        }
        ui.FlashInput(true);

        bool attack = idx == arena.SoulCell; // 서 있는 칸 = 공격, 다른 칸 = 이동
        if (attack) StandingAttack(idx);
        else MoveTo(idx);

        // 친 칸의 단어는 새 단어로 교체 (같은 단어 연타 방지)
        board.Refresh(idx);
        arena.SetWord(idx, board.Words[idx]);
        arena.TintSoul(attack); // 칸이 아니라 플레이어 색조가 바뀐다
    }

    /// <summary>서 있는 칸의 단어: 이동 없이 적에게 추가 데미지.</summary>
    private void StandingAttack(int idx)
    {
        int damage = settings.standDamage;
        enemy.hp = Mathf.Max(0, enemy.hp - damage);
        ui.ShakeEnemy();
        ui.FloatAtEnemy(damage.ToString(), new Color(1f, 0.88f, 0.35f));
        arena.Float("+" + damage, arena.CellCenter(idx) + new Vector2(0f, 40f), new Color(1f, 0.88f, 0.35f));
        Refresh();

        if (enemy.hp <= 0)
        {
            battleOver = true;
            victory = true;
        }
    }

    /// <summary>다른 칸의 단어: 그 칸을 향해 직선 이동하고 TP 상승 (피했는지와 무관).</summary>
    private void MoveTo(int idx)
    {
        arena.MoveSoulTo(idx);

        int gain = settings.tpPerMove;
        if (settings.maxTpGainPerEnemyTurn > 0)
            gain = Mathf.Min(gain, settings.maxTpGainPerEnemyTurn - tpGainedThisTurn);
        gain = Mathf.Max(0, gain);

        int before = tp;
        tp = Mathf.Min(settings.tpMax, tp + gain);
        tpGainedThisTurn += gain;

        if (tp > before)
            arena.Float("TP+" + (tp - before), arena.CellCenter(idx) + new Vector2(0f, 40f), new Color(0.4f, 0.9f, 1f));
        Refresh();
    }

    // ───────────────────────── 피격 ─────────────────────────

    private void OnSoulHit(int damage, BattleElement element)
    {
        if (battleOver) return;

        var hitTargets = PickHitTargets();
        foreach (BattleActor target in hitTargets)
        {
            // 속성 상성은 ElementChart에서 (지금은 전부 1배)
            float elementMult = ElementChart.Multiplier(element, BattleElement.None);
            int d = Mathf.Max(1, Mathf.RoundToInt(damage * elementMult));
            if (target.guarding) d = Mathf.Max(1, Mathf.RoundToInt(d * settings.guardDamageRate));

            target.hp = Mathf.Max(0, target.hp - d);
            arena.Float($"-{d}", arena.SoulPosition + new Vector2(0f, 30f), new Color(1f, 0.4f, 0.4f));
        }
        Refresh();

        // 피격 연출: 플레이어 커서 + 피해 입은 파티원 상태창 흔들기
        arena.HitFeedback();
        foreach (BattleActor target in hitTargets) ui.ShakePartySlot(party.IndexOf(target));

        if (!party.Exists(a => a.IsAlive))
        {
            battleOver = true;
            victory = false;
            arena.SetRunning(false); // 아레나가 목록을 비우고 멈춘다
        }
    }

    private List<BattleActor> PickHitTargets()
    {
        var alive = party.FindAll(a => a.IsAlive);
        var result = new List<BattleActor>();
        if (alive.Count == 0) return result;

        switch (settings.hitTarget)
        {
            case HitTargetMode.AllParty: result.AddRange(alive); break;
            case HitTargetMode.FirstAlive: result.Add(alive[0]); break;
            default: result.Add(alive[UnityEngine.Random.Range(0, alive.Count)]); break;
        }
        return result;
    }
}
