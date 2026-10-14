using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 대사 재생의 중심. 한 줄(DialogueLine)을 받아서
/// 화자가 파티원이면 PartyDialoguePanel로, 아니면 그 NPC의 말풍선으로 보낸다.
///
/// 씬 배치: 빈 오브젝트(예: Systems/DialogueManager)를 하나 만들어 이 스크립트를 붙인다.
/// 인스펙터 연결: partyPanel ← PartyDialoguePanel (PartyDialoguePanelController 포함된 오브젝트)
///
/// 사용법 예시:
///   DialogueManager.Instance.PlayDialogue(mySequence);                 // 파티끼리 대화
///   DialogueManager.Instance.PlayDialogue(mySequence, npcTransform);   // NPC와 대화 (말풍선)
///
/// 진행 입력: Enter, Z, Space, 마우스 왼쪽 클릭 (RPG Maker 느낌으로 Enter/Z 위주).
/// 나중에 Input Actions로 정식 바인딩할 때 Update()의 pressed 판정부만 바꾸면 됨.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    /// <summary>
    /// 지금 대사가 재생 중인지. 플레이어 이동/공격 등 다른 입력 스크립트가
    /// 이 값을 보고 자기 입력을 막는 데 쓴다. (메뉴창은 대사 중에도 열 수 있어야 하므로
    /// 메뉴 토글 스크립트는 이 값을 확인하지 않는다.)
    /// </summary>
    public bool IsDialogueActive => isPlaying;

    [SerializeField] private PartyDialoguePanelController partyPanel;

    [Header("파티 대사창 페이지 분할 (공백 포함 글자 수 기준)")]
    [SerializeField] private int maxCharsPerLine = DialogueTextWrapper.DefaultMaxCharsPerLine;
    [SerializeField] private int maxLinesPerPage = DialogueTextWrapper.DefaultMaxLinesPerPage;

    [Header("전투에서 패배(Allow Defeat 해제)했을 때 불러올 씬 이름 (비우면 현재 씬 다시 시작). Build Settings에 등록돼 있어야 함")]
    [SerializeField] private string gameOverSceneName;

    private bool isPlaying;
    private bool gameOver;
    private bool advanceRequested;
    private int startedOnFrame = -1;

    /// <summary>지금 화면에 떠 있는 대사 창(파티 패널 또는 말풍선). 타자 애니메이션 스킵 판단에 쓴다.</summary>
    private IDialogueDisplay activeDisplay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // partyPanel의 부모 중 하나(예: Canvas_Screen)가 꺼져있으면 partyPanel의 Awake가
        // 아예 호출되지 않아서(인스펙터 연결은 멀쩡한데도) null 에러가 난다.
        // 대사가 처음 시작되기 전에 미리 깨워서 이 문제를 막는다.
        EnsureAncestorsActive(partyPanel != null ? partyPanel.transform : null);
    }

    /// <summary>
    /// target과 그 부모들 중 꺼져있는 GameObject가 있으면 전부 켠다.
    /// (Canvas_Screen을 테스트하다가 꺼놓고 깜빡하는 실수를 방지)
    /// </summary>
    private static void EnsureAncestorsActive(Transform target)
    {
        for (Transform t = target; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 대사 진행/상호작용에 쓰는 공용 키 판정 (Enter, Z, Space, 마우스 왼쪽 클릭).
    /// NPC 상호작용(PlayerInteractor)도 같은 키를 쓰므로 여기서 공유한다.
    /// </summary>
    public static bool WasInteractKeyPressedThisFrame()
    {
        bool keyboard = Keyboard.current != null && (
                            Keyboard.current.enterKey.wasPressedThisFrame
                         || Keyboard.current.numpadEnterKey.wasPressedThisFrame
                         || Keyboard.current.zKey.wasPressedThisFrame
                         || Keyboard.current.spaceKey.wasPressedThisFrame);
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return keyboard || mouse;
    }

    private void Update()
    {
        if (!isPlaying) return;

        // PlayerInteractor가 대화를 시작시킨 바로 그 프레임의 키 입력이
        // 곧바로 첫 줄을 넘겨버리지 않도록 시작 프레임은 건너뛴다.
        if (Time.frameCount == startedOnFrame) return;

        if (!WasInteractKeyPressedThisFrame()) return;

        // 글자가 타자처럼 나오는 중이면: 이번 입력은 "즉시 끝까지 보여주기"로 쓰고
        // 다음 줄로 넘기지 않는다. 글자가 이미 다 나온 상태에서 눌러야 다음으로 넘어감.
        if (activeDisplay != null && activeDisplay.IsRevealing)
        {
            activeDisplay.SkipReveal();
        }
        else
        {
            advanceRequested = true;
        }
    }

    /// <param name="sequence">재생할 대사 묶음</param>
    /// <param name="balloonAnchor">
    /// NPC와의 대화일 때, 그 NPC(또는 Canvas_SpeechBalloon을 자식으로 둔 오브젝트)의 Transform.
    /// 파티끼리의 대화면 생략.
    /// </param>
    /// <param name="onComplete">전체 대사가 끝난 뒤 호출할 콜백 (선택)</param>
    public void PlayDialogue(DialogueSequence sequence, Transform balloonAnchor = null, Action onComplete = null)
    {
        if (isPlaying)
        {
            Debug.LogWarning("DialogueManager: 이미 대사가 재생 중입니다.");
            return;
        }
        StartCoroutine(PlayRoutine(sequence, balloonAnchor, onComplete));
    }

    private IEnumerator PlayRoutine(DialogueSequence sequence, Transform balloonAnchor, Action onComplete)
    {
        isPlaying = true;
        gameOver = false;
        startedOnFrame = Time.frameCount;

        // 말풍선 쪽 부모도 혹시 꺼져있으면 미리 깨워둔다 (partyPanel과 동일한 이유).
        EnsureAncestorsActive(balloonAnchor);

        SpeechBalloonController balloon = balloonAnchor != null
            ? balloonAnchor.GetComponentInChildren<SpeechBalloonController>(true)
            : null;

        if (balloonAnchor != null && balloon == null)
            Debug.LogWarning($"DialogueManager: {balloonAnchor.name} 아래에서 SpeechBalloonController를 찾지 못했습니다.");

        yield return PlayLines(sequence, balloon);

        activeDisplay = null;
        partyPanel.Hide();
        if (balloon != null) balloon.Hide();

        if (gameOver)
        {
            // 게임오버: isPlaying을 true로 둔 채(필드 조작 잠금 유지) 게임오버 처리로 넘어간다. onComplete는 부르지 않는다.
            HandleGameOver();
            yield break;
        }

        isPlaying = false;
        onComplete?.Invoke();
    }

    /// <summary>
    /// 대사 묶음을 한 줄씩 재생. 전투 줄의 승리/패배 대화도 이 함수로 이어서 재생한다.
    /// 게임오버가 되면 gameOver를 켜고 바로 빠져나온다.
    /// </summary>
    private IEnumerator PlayLines(DialogueSequence sequence, SpeechBalloonController balloon)
    {
        foreach (DialogueLine line in sequence.lines)
        {
            if (line.kind == DialogueLineKind.Battle)
            {
                // 대사(Text)가 적혀 있으면 전투 시작 전에 먼저 보여준다 (비어 있으면 바로 전투)
                if (!string.IsNullOrWhiteSpace(line.text))
                    yield return PlaySayLine(line, balloon);

                // 전투 전에 떠 있는 창을 모두 정리하고, 전투가 끝날 때까지 기다린다.
                if (balloon != null) balloon.Hide();
                partyPanel.Hide();
                activeDisplay = null;

                yield return PlayBattleLine(line, balloon);
                if (gameOver) yield break;
                continue;
            }

            yield return PlaySayLine(line, balloon);
        }
    }

    /// <summary>대사 한 줄을 파티 대사창 또는 말풍선으로 띄우고, 진행 입력을 받을 때까지 기다린다.</summary>
    private IEnumerator PlaySayLine(DialogueLine line, SpeechBalloonController balloon)
    {
        if (line.ShouldUsePartyPanel())
        {
            // 이전 줄이 말풍선이었다면, 파티 창으로 넘어가기 전에 말풍선부터 꺼준다.
            // (안 꺼주면 화자가 바뀌어도 말풍선이 화면에 계속 남아있음)
            if (balloon != null) balloon.Hide();

            // 파티 대사창은 한 줄 N자 / 페이지당 M줄로 자동 분할.
            // 페이지마다 한 번씩 진행 입력을 받는다 (한 DialogueLine이 여러 페이지로 보일 수 있음).
            List<string> pages = DialogueTextWrapper.SplitIntoPages(line.text, maxCharsPerLine, maxLinesPerPage);
            foreach (string page in pages)
            {
                partyPanel.ShowPage(line, page);
                activeDisplay = partyPanel;
                advanceRequested = false;
                yield return new WaitUntil(() => advanceRequested);
            }
            yield break;
        }
        else if (balloon != null)
        {
            // 이전 줄이 파티 창이었다면, 말풍선으로 넘어가기 전에 파티 창부터 꺼준다.
            partyPanel.Hide();

            balloon.Show(line);
            activeDisplay = balloon;
        }
        else
        {
            Debug.LogWarning("DialogueManager: 파티원이 아닌 화자인데 balloonAnchor가 없습니다. 대사를 건너뜁니다.");
            activeDisplay = null;
        }

        advanceRequested = false;
        yield return new WaitUntil(() => advanceRequested);
    }

    /// <summary>전투 줄 하나: 전투 실행 → 결과에 따라 승리/패배 대화 재생 (또는 게임오버).</summary>
    private IEnumerator PlayBattleLine(DialogueLine line, SpeechBalloonController balloon)
    {
        if (line.battle == null)
        {
            Debug.LogWarning("DialogueManager: 전투 줄인데 Battle 에셋이 비어 있습니다. 이 줄을 건너뜁니다.");
            yield break;
        }

        BattleOutcome outcome = BattleOutcome.Failed;
        yield return BattleLauncher.Run(line.battle, line.allowDefeat, o => outcome = o);

        if (outcome == BattleOutcome.Victory)
        {
            if (line.onVictory != null) yield return PlayLines(line.onVictory, balloon);
        }
        else if (outcome == BattleOutcome.Defeat)
        {
            if (!line.allowDefeat)
            {
                gameOver = true;
            }
            else if (line.onDefeat != null)
            {
                yield return PlayLines(line.onDefeat, balloon);
            }
        }
        // Failed(전투 씬을 못 불러옴): 에러는 BattleLauncher가 이미 로그에 남겼고, 대화는 그냥 다음 줄로 이어간다.
    }

    private void HandleGameOver()
    {
        BattleLauncher.ResetPartyHp();

        if (string.IsNullOrEmpty(gameOverSceneName))
        {
            Debug.LogWarning("DialogueManager: Game Over Scene Name이 비어 있어 현재 씬을 다시 불러옵니다. " +
                             "인스펙터에서 게임오버 씬 이름을 지정하세요 (Build Settings에 등록 필요).");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else
        {
            SceneManager.LoadScene(gameOverSceneName);
        }
    }
}
