using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 턴 동안 AttackPattern 목록에서 패턴을 골라 차례로 재생한다.
/// 스텝 하나 = (경고 표시 → 경고 시간 대기 → 발사). 스텝들은 시작 시각(time)에 맞춰 서로 겹쳐 진행된다.
/// </summary>
public class BattlePatternRunner : MonoBehaviour
{
    public bool IsRunning { get; private set; }

    private BattleArena arena;
    private float speedScale = 1f;
    private float tempoScale = 1f;
    private readonly HashSet<AttackSide> warnedSides = new HashSet<AttackSide>();

    /// <param name="speedScale">탄 속도 배율 (낮을수록 느림)</param>
    /// <param name="tempoScale">발사 템포 배율 (낮을수록 스텝/패턴 간격이 길어짐. 경고 시간은 그대로)</param>
    public void Begin(BattleArena targetArena, List<AttackPattern> patterns, float duration, float gap,
                      float speedScale = 1f, float tempoScale = 1f)
    {
        Cancel();
        arena = targetArena;
        this.speedScale = Mathf.Max(0.05f, speedScale);
        this.tempoScale = Mathf.Max(0.05f, tempoScale);
        warnedSides.Clear();
        IsRunning = true;
        StartCoroutine(RunTurn(patterns, duration, gap));
    }

    /// <summary>진행 중인 스텝/패턴을 전부 멈춘다 (탄 정리는 arena.SetRunning(false)가 담당).</summary>
    public void Cancel()
    {
        StopAllCoroutines();
        IsRunning = false;
    }

    private IEnumerator RunTurn(List<AttackPattern> patterns, float duration, float gap)
    {
        float end = Time.time + duration;
        int last = -1;

        // 이 칸 배치에서 피할 수 없는 공격(레인 1개짜리 방향)은 아예 쓰지 않는다
        if (patterns != null)
            patterns = patterns.FindAll(p => p != null && p.steps != null && p.steps.Exists(st => arena.CanDodge(st.side)));

        if (patterns == null || patterns.Count == 0)
        {
            Debug.LogWarning($"[Battle] 칸 배치 {arena.Columns}x{arena.Rows} 에서 쓸 수 있는(피할 수 있는) 공격 패턴이 없어 이번 턴은 공격이 없습니다.");
            yield return new WaitForSeconds(duration);
            IsRunning = false;
            yield break;
        }

        while (Time.time < end)
        {
            int idx = Random.Range(0, patterns.Count);
            if (patterns.Count > 1 && idx == last) idx = (idx + 1) % patterns.Count;
            last = idx;

            yield return RunPattern(patterns[idx], end);

            float wait = Mathf.Min(gap / tempoScale, end - Time.time);
            if (wait > 0f) yield return new WaitForSeconds(wait);
        }

        IsRunning = false;
    }

    private IEnumerator RunPattern(AttackPattern pattern, float turnEnd)
    {
        float start = Time.time;
        var steps = new List<PatternStep>();
        foreach (PatternStep st in pattern.steps)
        {
            if (arena.CanDodge(st.side)) { steps.Add(st); continue; }
            if (warnedSides.Add(st.side))
                Debug.LogWarning($"[Battle] 칸 배치 {arena.Columns}x{arena.Rows} 에서 {st.side} 방향 공격은 피할 수 없어서 건너뜁니다.");
        }
        steps.Sort((a, b) => a.time.CompareTo(b.time));

        foreach (PatternStep step in steps)
        {
            float wait = start + step.time / tempoScale - Time.time;
            if (wait > 0f) yield return new WaitForSeconds(wait);
            if (Time.time >= turnEnd) yield break;
            StartCoroutine(RunStep(step));
        }

        // 마지막 스텝의 경고가 끝날 때까지 기다린 뒤 다음 패턴으로
        float patternEnd = 0f;
        foreach (PatternStep step in steps)
            patternEnd = Mathf.Max(patternEnd, step.time / tempoScale + step.warning);
        float tail = Mathf.Min(start + patternEnd, turnEnd) - Time.time;
        if (tail > 0f) yield return new WaitForSeconds(tail);
    }

    private IEnumerator RunStep(PatternStep step)
    {
        List<int> lanes = PickLanes(step);
        foreach (int lane in lanes) arena.ShowWarning(step.side, lane, step.warning);

        yield return new WaitForSeconds(step.warning);
        if (!arena.IsRunning) yield break;

        foreach (int lane in lanes)
            arena.Fire(step.side, lane, step.speed * speedScale, step.damage, step.element);
    }

    private List<int> PickLanes(PatternStep step)
    {
        int count = arena.LaneCount(step.side);
        var lanes = new List<int>();

        switch (step.laneSelect)
        {
            case LaneSelect.Fixed:
                lanes.Add(Mathf.Clamp(step.lane, 0, count - 1));
                break;
            case LaneSelect.AtPlayer:
                lanes.Add(arena.SoulLane(step.side));
                break;
            case LaneSelect.AllButOne:
                int safe = Random.Range(0, count);
                for (int i = 0; i < count; i++)
                    if (i != safe) lanes.Add(i);
                break;
            default:
                lanes.Add(Random.Range(0, count));
                break;
        }
        return lanes;
    }
}
