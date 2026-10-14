using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 적 턴에 플레이어가 움직이는 칸 판(N행 x M열)과 탄막.
///
/// - 칸 수는 BattleSettings.columns / rows 로 자유롭게 바꾼다.
/// - 플레이어(Soul)는 단어를 친 칸을 향해 "직선으로" 이동한다. 이동 중에도 탄에 맞는다.
///   이동 중에 다른 칸 단어를 치면 지금 위치에서 새 목적지로 방향만 바뀐다.
/// - 충돌은 Physics2D 대신 직접 계산한다 (BattleMath.SegmentHitsOrigin).
///   탄과 플레이어가 한 프레임에 아무리 빨리 움직여도 서로를 관통하지 않고, 레이어/태그 설정이 필요 없다.
/// - 모든 그래픽은 uGUI(Image / TextMeshProUGUI)라서 카메라/렌더 파이프라인과 무관하게 보인다.
/// </summary>
public class BattleArena : MonoBehaviour
{
    private class Bullet
    {
        public RectTransform rt;
        public Vector2 pos;
        public Vector2 vel;
        public float radius;
        public int damage;
        public BattleElement element;
    }

    private class Warning
    {
        public Image img;
        public float elapsed;
        public float duration;
    }

    /// <summary>플레이어가 탄에 맞았을 때 (피해량, 속성).</summary>
    public event Action<int, BattleElement> SoulHit;

    private static readonly Color CellColor = new Color(0.10f, 0.11f, 0.17f, 0.92f);
    private static readonly Color WordColor = new Color(1f, 1f, 1f, 0.95f);
    private static readonly Color WordCurrentColor = new Color(1f, 0.88f, 0.35f, 1f);

    private BattleSettings s;
    private TMP_FontAsset font;
    private int cols, rows;
    private float cell, halfW, halfH;

    private RectTransform root;
    private RectTransform cellLayer, warningLayer, soulLayer, bulletLayer, labelLayer, floatLayer;
    private Image[] cellImages;
    private TextMeshProUGUI[] cellLabels;
    private float soulFlash;          // 남은 색조 변화 시간
    private bool soulFlashAttack;

    private Image soulImg;
    private RectTransform soulRt;
    private Vector2 soulPos, soulPrev, soulTarget;
    private int soulCell;
    private float invincible;
    private float hitShake;
    private float poseScaleY = 1f, poseDetail = 1f;
    private float morphX = 1f, morphY = 1f; // 칸 배치가 바뀔 때 이전 크기에서 새 크기로 이어지게 하는 보정 배율
    private CanvasGroup labelGroup, soulGroup;
    private const float HitShakeTime = 0.3f;
    private bool running;

    private readonly List<Bullet> bullets = new List<Bullet>();
    private readonly List<Warning> warnings = new List<Warning>();

    public int Columns => cols;
    public int Rows => rows;
    public int CellCount => cols * rows;
    public float CellSize => cell;
    public bool IsRunning => running;

    /// <summary>플레이어가 "논리적으로" 서 있는 칸. 이동 중이면 목적지 칸.</summary>
    public int SoulCell => soulCell;

    public Vector2 SoulPosition => soulPos;

    public void Build(BattleSettings settings, TMP_FontAsset fontAsset, Vector2 center)
    {
        s = settings;
        font = fontAsset;
        root = (RectTransform)transform;
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = center;
        Construct(s.columns, s.rows);
    }

    /// <summary>칸 배치를 바꿔서 다시 만든다 (라운드별 배치용). 같으면 아무것도 안 한다.</summary>
    public void Relayout(int columns, int rowsCount)
    {
        columns = Mathf.Max(1, columns);
        rowsCount = Mathf.Max(1, rowsCount);
        if (columns == cols && rowsCount == rows) return;
        Construct(columns, rowsCount);
    }

    /// <summary>
    /// 칸 배치를 바꾸되, 이전 판의 크기에서 새 판의 크기로 부드럽게 늘어나거나 줄어든다.
    /// (플레이어 턴처럼 판이 납작하고 글자가 숨겨진 상태에서 쓰면 막대가 늘었다 줄었다 하는 것처럼 보인다.)
    /// 끝날 때까지 기다리는 코루틴.
    /// </summary>
    public IEnumerator RelayoutAnimated(int columns, int rowsCount, float seconds)
    {
        columns = Mathf.Max(1, columns);
        rowsCount = Mathf.Max(1, rowsCount);
        if (columns == cols && rowsCount == rows) yield break;

        float oldW = cols * cell, oldH = rows * cell;
        Construct(columns, rowsCount);
        float newW = Mathf.Max(1f, cols * cell), newH = Mathf.Max(1f, rows * cell);

        float dur = Mathf.Max(0.01f, seconds);
        float fromX = oldW / newW, fromY = oldH / newH;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / dur);
            float ease = 1f - (1f - p) * (1f - p) * (1f - p); // 빠르게 시작해 부드럽게 멈춤
            morphX = Mathf.Lerp(fromX, 1f, ease);
            morphY = Mathf.Lerp(fromY, 1f, ease);
            ApplyPose();
            yield return null;
        }
        morphX = morphY = 1f;
        ApplyPose();
    }

    private void Construct(int columns, int rowsCount)
    {
        ClearHazards();
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject old = root.GetChild(i).gameObject;
            old.SetActive(false);
            Destroy(old);
        }

        cols = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rowsCount);
        cell = Mathf.Min(s.cellSize, s.arenaMaxWidth / cols, s.arenaMaxHeight / rows);
        halfW = cols * cell * 0.5f;
        halfH = rows * cell * 0.5f;

        root.sizeDelta = new Vector2(cols * cell, rows * cell);

        // 그리는 순서: 테두리 → 칸 → 경고 → 플레이어 → 탄 → 단어(항상 읽혀야 하니 맨 위) → 떠오르는 글자
        BattleUIFactory.MakeImage("ArenaFrame", root, Vector2.zero,
                                  new Vector2(cols * cell + 14f, rows * cell + 14f), new Color(0.85f, 0.88f, 1f, 0.9f));
        cellLayer = MakeLayer("CellLayer");
        warningLayer = MakeLayer("WarningLayer");
        soulLayer = MakeLayer("SoulLayer");
        bulletLayer = MakeLayer("BulletLayer");
        labelLayer = MakeLayer("WordLayer");
        floatLayer = MakeLayer("FloatLayer");
        labelGroup = labelLayer.gameObject.AddComponent<CanvasGroup>();
        soulGroup = soulLayer.gameObject.AddComponent<CanvasGroup>();

        int count = cols * rows;
        cellImages = new Image[count];
        cellLabels = new TextMeshProUGUI[count];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int idx = r * cols + c;
                Vector2 p = CellCenter(idx);
                // 칸 색의 투명도를 낮췄을 때 밝은 테두리가 아니라 이 어두운 바탕이 비치도록 칸마다 바탕을 깐다
                BattleUIFactory.MakeImage($"CellBack_{c}_{r}", cellLayer, p, new Vector2(cell - 6f, cell - 6f), s.cellBackColor);
                cellImages[idx] = BattleUIFactory.MakeImage($"Cell_{c}_{r}", cellLayer, p,
                                                            new Vector2(cell - 6f, cell - 6f), CellColor);
                TextMeshProUGUI label = BattleUIFactory.MakeText($"Word_{c}_{r}", labelLayer, p,
                                                                 new Vector2(cell - 12f, cell - 12f), "", 52f, WordColor, font);
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 52f;
                cellLabels[idx] = label;
            }
        }

        soulImg = BattleUIFactory.MakeImage("Soul", soulLayer, Vector2.zero, new Vector2(30f, 30f),
                                            s.soulColor, BattleUIFactory.CircleSprite);
        soulRt = soulImg.rectTransform;
        ApplyPose();
        ResetSoul();
    }

    /// <summary>
    /// 레이아웃 전환용: 판의 중심 위치, 세로 배율(납작하게), 글자·플레이어의 불투명도(0 = 숨김)를 한 번에 정한다.
    /// 칸 배치가 다시 만들어져도 이 상태는 유지된다.
    /// </summary>
    public void SetPose(Vector2 center, float scaleY, float detailAlpha)
    {
        root.anchoredPosition = center;
        poseScaleY = scaleY;
        poseDetail = Mathf.Clamp01(detailAlpha);
        ApplyPose();
    }

    private void ApplyPose()
    {
        root.localScale = new Vector3(morphX, poseScaleY * morphY, 1f);
        if (labelGroup != null) labelGroup.alpha = poseDetail;
        if (soulGroup != null) soulGroup.alpha = poseDetail;
    }

    private RectTransform MakeLayer(string layerName)
    {
        RectTransform rt = BattleUIFactory.MakeRect(layerName, root, Vector2.zero, Vector2.zero);
        BattleUIFactory.Stretch(rt);
        return rt;
    }

    // ───────────── 좌표 ─────────────

    /// <summary>칸 번호(행*열수+열, 왼쪽 위가 0) → 아레나 중심 기준 좌표</summary>
    public Vector2 CellCenter(int idx)
    {
        int c = idx % cols;
        int r = idx / cols;
        return new Vector2((c - (cols - 1) * 0.5f) * cell, ((rows - 1) * 0.5f - r) * cell);
    }

    /// <summary>Top/Bottom에서 오는 탄의 레인 수 = 열 수, Left/Right = 행 수</summary>
    public int LaneCount(AttackSide side)
    {
        return (side == AttackSide.Top || side == AttackSide.Bottom) ? cols : rows;
    }

    /// <summary>
    /// 이 방향의 공격을 피할 수 있는가? 레인이 1개뿐이면(열 1개 → 위/아래 공격, 행 1개 → 좌/우 공격)
    /// 플레이어가 옆 레인으로 비킬 곳이 없어서 무조건 맞는다.
    /// </summary>
    public bool CanDodge(AttackSide side) { return LaneCount(side) >= 2; }

    /// <summary>해당 레인의 중심선 좌표 (Top/Bottom이면 x, Left/Right이면 y)</summary>
    private float LaneCoord(AttackSide side, int lane)
    {
        if (side == AttackSide.Top || side == AttackSide.Bottom)
            return (lane - (cols - 1) * 0.5f) * cell;
        return ((rows - 1) * 0.5f - lane) * cell;
    }

    /// <summary>지금 플레이어 위치에 가장 가까운 레인 (조준탄용)</summary>
    public int SoulLane(AttackSide side)
    {
        if (side == AttackSide.Top || side == AttackSide.Bottom)
            return Mathf.Clamp(Mathf.RoundToInt(soulPos.x / cell + (cols - 1) * 0.5f), 0, cols - 1);
        return Mathf.Clamp(Mathf.RoundToInt((rows - 1) * 0.5f - soulPos.y / cell), 0, rows - 1);
    }

    // ───────────── 단어 / 칸 표시 ─────────────

    public void SetWord(int idx, string word)
    {
        if (idx < 0 || idx >= cellLabels.Length) return;
        // <nobr>: 긴 단어가 칸 안에서 두 줄로 꺾이지 않게
        cellLabels[idx].text = "<nobr>" + word + "</nobr>";
    }

    /// <summary>
    /// 플레이어(커서)의 색조를 잠깐 바꿨다가 원래 색으로 돌아오게 한다.
    /// attack=true 면 공격 색(soulAttackColor), false 면 이동 색(soulMoveColor).
    /// </summary>
    public void TintSoul(bool attack)
    {
        soulFlash = Mathf.Max(0.05f, s.soulTintSeconds);
        soulFlashAttack = attack;
    }

    private Color CurrentSoulColor()
    {
        if (soulFlash <= 0f) return s.soulColor;
        float k = Mathf.Clamp01(soulFlash / Mathf.Max(0.05f, s.soulTintSeconds));
        return Color.Lerp(s.soulColor, soulFlashAttack ? s.soulAttackColor : s.soulMoveColor, k);
    }

    public void Float(string text, Vector2 localPos, Color color)
    {
        BattleFloatText.Spawn(floatLayer, localPos, text, color, font, 36f);
    }

    // ───────────── 플레이어 ─────────────

    public void ResetSoul()
    {
        soulCell = (rows - 1) * cols + cols / 2;
        soulPos = CellCenter(soulCell);
        soulPrev = soulPos;
        soulTarget = soulPos;
        soulRt.anchoredPosition = soulPos;
        invincible = 0f;
        soulFlash = 0f;
        soulImg.color = s.soulColor;
    }

    /// <summary>그 칸을 향해 직선 이동 시작 (이동 중이면 방향만 바뀜).</summary>
    public void MoveSoulTo(int idx)
    {
        if (idx < 0 || idx >= CellCount) return;
        soulCell = idx;
        soulTarget = CellCenter(idx);
    }

    /// <summary>피격 연출: 플레이어(커서)가 흔들리며 잠깐 커진다.</summary>
    public void HitFeedback() { hitShake = HitShakeTime; }

    public void SetRunning(bool value)
    {
        running = value;
        if (!value)
        {
            ClearHazards();
            invincible = 0f;
            hitShake = 0f;
            if (soulRt != null) { soulRt.localScale = Vector3.one; soulRt.anchoredPosition = soulPos; }
            if (soulImg != null) soulImg.color = CurrentSoulColor();
        }
    }

    // ───────────── 탄막 ─────────────

    public void ShowWarning(AttackSide side, int lane, float duration)
    {
        Vector2 pos;
        Vector2 size;
        if (side == AttackSide.Top || side == AttackSide.Bottom)
        {
            pos = new Vector2(LaneCoord(side, lane), 0f);
            size = new Vector2(cell - 6f, rows * cell);
        }
        else
        {
            pos = new Vector2(0f, LaneCoord(side, lane));
            size = new Vector2(cols * cell, cell - 6f);
        }

        Image img = BattleUIFactory.MakeImage("Warning", warningLayer, pos, size, new Color(s.warningColor.r, s.warningColor.g, s.warningColor.b, Mathf.Min(s.warningAlphaMin, s.warningAlphaMax)));
        warnings.Add(new Warning { img = img, duration = Mathf.Max(0.05f, duration) });
    }

    public void Fire(AttackSide side, int lane, float speed, int damage, BattleElement element)
    {
        float along = LaneCoord(side, lane);
        float r = s.bulletRadius;
        float edge = r + 20f;

        Vector2 pos;
        Vector2 vel;
        switch (side)
        {
            case AttackSide.Top: pos = new Vector2(along, halfH + edge); vel = new Vector2(0f, -speed); break;
            case AttackSide.Bottom: pos = new Vector2(along, -halfH - edge); vel = new Vector2(0f, speed); break;
            case AttackSide.Left: pos = new Vector2(-halfW - edge, along); vel = new Vector2(speed, 0f); break;
            default: pos = new Vector2(halfW + edge, along); vel = new Vector2(-speed, 0f); break;
        }

        Image img = BattleUIFactory.MakeImage("Bullet", bulletLayer, pos, new Vector2(r * 2f, r * 2f),
                                              ElementChart.ColorOf(element), BattleUIFactory.CircleSprite);
        bullets.Add(new Bullet { rt = img.rectTransform, pos = pos, vel = vel, radius = r, damage = damage, element = element });
    }

    public void ClearHazards()
    {
        foreach (Bullet b in bullets)
            if (b.rt != null) Destroy(b.rt.gameObject);
        bullets.Clear();

        foreach (Warning w in warnings)
            if (w.img != null) Destroy(w.img.gameObject);
        warnings.Clear();
    }

    // ───────────── 매 프레임 ─────────────

    private void Update()
    {
        if (cellImages == null) return;
        float dt = Time.deltaTime;

        UpdateCellVisuals(dt);
        if (!running) return;

        // 플레이어: 목적지를 향해 일정한 속도로 직선 이동
        soulPrev = soulPos;
        soulPos = Vector2.MoveTowards(soulPos, soulTarget, s.soulSpeed * dt);
        Vector2 shakeOffset = Vector2.zero;
        if (hitShake > 0f)
        {
            hitShake -= dt;
            float k = Mathf.Clamp01(hitShake / HitShakeTime);
            shakeOffset = new Vector2(Mathf.Sin(Time.time * 80f) * 7f * k, Mathf.Cos(Time.time * 95f) * 5f * k);
            soulRt.localScale = Vector3.one * (1f + 0.6f * k);   // 맞는 순간 커졌다가 돌아옴
        }
        else soulRt.localScale = Vector3.one;
        soulRt.anchoredPosition = soulPos + shakeOffset;

        if (invincible > 0f)
        {
            invincible -= dt;
            bool visible = invincible <= 0f || ((int)(Time.time * 16f) % 2 == 0);
            Color sc = CurrentSoulColor();
            soulImg.color = visible ? sc : new Color(sc.r, sc.g, sc.b, 0.25f);
        }

        UpdateWarnings(dt);
        UpdateBullets(dt);
    }

    private void UpdateCellVisuals(float dt)
    {
        if (soulFlash > 0f) soulFlash = Mathf.Max(0f, soulFlash - dt);
        if (soulImg != null && invincible <= 0f) soulImg.color = CurrentSoulColor();

        for (int i = 0; i < cellImages.Length; i++)
        {
            Color c = (i == soulCell) ? s.currentCellColor : CellColor;
            cellImages[i].color = c;
            // 지금 서 있는 칸의 단어(= 치면 추가 공격)는 금색으로
            cellLabels[i].color = (i == soulCell) ? WordCurrentColor : WordColor;
        }
    }

    private void UpdateWarnings(float dt)
    {
        for (int i = warnings.Count - 1; i >= 0; i--)
        {
            Warning w = warnings[i];
            w.elapsed += dt;
            if (w.elapsed >= w.duration)
            {
                Destroy(w.img.gameObject);
                warnings.RemoveAt(i);
                continue;
            }
            float pulse = Mathf.Lerp(Mathf.Min(s.warningAlphaMin, s.warningAlphaMax),
                                     Mathf.Max(s.warningAlphaMin, s.warningAlphaMax),
                                     Mathf.Abs(Mathf.Sin(w.elapsed * Mathf.PI * Mathf.Max(0.1f, s.warningBlinkPerSecond))));
            w.img.color = new Color(s.warningColor.r, s.warningColor.g, s.warningColor.b, pulse);
        }
    }

    private void UpdateBullets(float dt)
    {
        float hitRange = s.soulHitRadius;
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            if (i >= bullets.Count) continue; // 이벤트 처리 중 목록이 비워졌을 수 있음
            Bullet b = bullets[i];

            Vector2 from = b.pos;
            b.pos += b.vel * dt;
            b.rt.anchoredPosition = b.pos;

            // 플레이어 기준 상대 좌표로 "이번 프레임 이동 선분"이 원과 겹치는지 검사 → 관통 없음
            if (invincible <= 0f &&
                BattleMath.SegmentHitsOrigin(from.x - soulPrev.x, from.y - soulPrev.y,
                                             b.pos.x - soulPos.x, b.pos.y - soulPos.y,
                                             hitRange + b.radius))
            {
                int damage = b.damage;
                BattleElement element = b.element;
                Destroy(b.rt.gameObject);
                bullets.RemoveAt(i);

                invincible = s.invincibleSeconds;
                SoulHit?.Invoke(damage, element);
                if (!running) return; // 핸들러가 전투를 끝냈다면 목록이 이미 비워짐
                continue;
            }

            if (Mathf.Abs(b.pos.x) > halfW + b.radius + 60f || Mathf.Abs(b.pos.y) > halfH + b.radius + 60f)
            {
                Destroy(b.rt.gameObject);
                bullets.RemoveAt(i);
            }
        }
    }
}
