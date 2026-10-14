using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>전투가 어떻게 끝났는지.</summary>
public enum BattleOutcome
{
    Victory,
    Defeat,
    /// <summary>전투 씬을 불러오지 못해 전투가 일어나지 않음 (Build Settings 에 씬이 없는 경우 등)</summary>
    Failed,
}

/// <summary>
/// 필드(대화) 쪽과 전투 씬 사이의 다리.
///
/// 흐름: DialogueManager 가 yield return BattleLauncher.Run(...) →
///       전투 씬을 Additive 로 불러옴 → BattleManager 가 Pending 을 읽고 전투 → Report(승패) →
///       전투 씬 언로드 → 결과를 돌려줌.
///
/// 전투 씬(BattlePrototype)만 직접 재생할 때는 Active 가 false 라서 지금까지와 똑같이 동작한다.
/// </summary>
public static class BattleLauncher
{
    /// <summary>Build Settings 에 들어 있어야 하는 전투 씬 이름</summary>
    public const string BattleSceneName = "BattlePrototype";

    /// <summary>true 인 동안 전투가 진행 중 (필드 메뉴 등이 입력을 막는 데 쓸 수 있다)</summary>
    public static bool Active { get; private set; }

    /// <summary>이번 전투의 설정. BattleManager 가 시작할 때 읽는다.</summary>
    public static BattleEncounter Pending { get; private set; }

    /// <summary>true 면 패배해도 게임오버가 아니라 대화가 이어진다. (BattleManager 는 읽기만 함)</summary>
    public static bool AllowDefeat { get; private set; }

    /// <summary>전환 연출이 끝나 전투를 시작해도 되는지. BattleManager 가 이 값이 true 가 될 때까지 기다렸다 시작한다.</summary>
    public static bool CanStart { get; private set; }

    private static bool reported;
    private static bool reportedVictory;

    /// <summary>파티원 HP (이름 → HP). 전투와 전투 사이에 유지된다. 게임을 껐다 켜면 사라진다 (저장 기능은 아직 없음).</summary>
    private static readonly Dictionary<string, int> partyHp = new Dictionary<string, int>();

    // ───────────────────────── 파티 HP ─────────────────────────

    /// <summary>전투 시작 때 쓸 HP. 저장된 값이 없으면 최대 HP, 0 이하면 1.</summary>
    public static int GetStartHp(BattleActor actor)
    {
        int hp;
        if (!partyHp.TryGetValue(actor.name, out hp)) hp = actor.maxHP;
        if (hp <= 0) hp = 1;
        return Mathf.Min(hp, actor.maxHP);
    }

    public static void SaveHp(IList<BattleActor> party)
    {
        foreach (BattleActor a in party) partyHp[a.name] = a.hp;
    }

    /// <summary>저장된 HP 를 전부 지운다 (새 게임 등).</summary>
    public static void ResetPartyHp() { partyHp.Clear(); }

    // ───────────────────────── 실행 ─────────────────────────

    /// <summary>
    /// 전투를 시작하고 끝날 때까지 기다린다.  사용: yield return BattleLauncher.Run(enc, allowDefeat, r => ...);
    /// </summary>
    public static IEnumerator Run(BattleEncounter encounter, bool allowDefeat, Action<BattleOutcome> onDone)
    {
        if (Active)
        {
            Debug.LogWarning("[Battle] 이미 전투가 진행 중입니다.");
            onDone?.Invoke(BattleOutcome.Failed);
            yield break;
        }

        Pending = encounter;
        AllowDefeat = allowDefeat;
        reported = false;
        CanStart = false;
        Active = true;

        // 1) 필드 화면을 덮는다
        var overlay = new BattleTransitionOverlay(encounter != null ? encounter.transition : null);
        yield return overlay.Cover();

        // 2) 가려진 채로 전투 씬을 불러온다
        AsyncOperation load = SceneManager.LoadSceneAsync(BattleSceneName, LoadSceneMode.Additive);
        if (load == null)
        {
            Debug.LogError($"[Battle] '{BattleSceneName}' 씬을 불러오지 못했습니다. File > Build Profiles(Build Settings) 의 Scene List 에 씬을 추가했는지 확인하세요.");
            yield return overlay.Reveal();
            overlay.Destroy();
            Active = false;
            Pending = null;
            onDone?.Invoke(BattleOutcome.Failed);
            yield break;
        }
        while (!load.isDone) yield return null;

        DedupeSceneObjects(SceneManager.GetSceneByName(BattleSceneName));

        yield return null; // 전투 UI 가 만들어질 시간
        yield return null;
        if (overlay.HoldSeconds > 0f) yield return new WaitForSeconds(overlay.HoldSeconds);

        // 3) 걷어내면서 전투 시작
        CanStart = true;
        yield return overlay.Reveal();

        while (!reported) yield return null;

        // 4) 전투가 끝나면 다시 덮고, 가려진 채로 전투 씬을 치운 뒤 필드로 돌아온다
        yield return overlay.Cover();

        AsyncOperation unload = SceneManager.UnloadSceneAsync(BattleSceneName);
        if (unload != null)
            while (!unload.isDone) yield return null;
        yield return null;
        if (overlay.HoldSeconds > 0f) yield return new WaitForSeconds(overlay.HoldSeconds);

        Active = false;
        Pending = null;
        CanStart = false;
        onDone?.Invoke(reportedVictory ? BattleOutcome.Victory : BattleOutcome.Defeat);

        yield return overlay.Reveal(); // 필드 화면이 드러난다 (이 코루틴이 끝나야 대화가 이어진다)
        overlay.Destroy();
    }

    /// <summary>BattleManager 가 전투가 끝났을 때 부른다.</summary>
    public static void Report(bool victory)
    {
        reportedVictory = victory;
        reported = true;
    }

    // 전투 씬에는 카메라/EventSystem/AudioListener 가 하나씩 들어 있어서, 필드 씬 것과 겹치면 경고가 난다.
    private static void DedupeSceneObjects(Scene battleScene)
    {
        if (!battleScene.IsValid()) return;

        foreach (EventSystem es in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (es.gameObject.scene == battleScene && HasOtherScene<EventSystem>(battleScene))
                UnityEngine.Object.Destroy(es.gameObject);
        }
        foreach (AudioListener al in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (al.gameObject.scene == battleScene && HasOtherScene<AudioListener>(battleScene))
                al.enabled = false;
        }
        foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.gameObject.scene == battleScene && HasOtherScene<Camera>(battleScene))
                cam.enabled = false; // 전투 화면은 Overlay UI 라서 카메라가 없어도 보인다
        }
    }

    private static bool HasOtherScene<T>(Scene except) where T : Component
    {
        foreach (T c in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (c.gameObject.scene != except) return true;
        return false;
    }
}
