using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private bool isPlaying;
    private bool advanceRequested;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (!isPlaying) return;

        bool pressed = (Keyboard.current != null && (
                            Keyboard.current.enterKey.wasPressedThisFrame
                         || Keyboard.current.numpadEnterKey.wasPressedThisFrame
                         || Keyboard.current.zKey.wasPressedThisFrame
                         || Keyboard.current.spaceKey.wasPressedThisFrame))
                     || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        if (pressed) advanceRequested = true;
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

        SpeechBalloonController balloon = balloonAnchor != null
            ? balloonAnchor.GetComponentInChildren<SpeechBalloonController>(true)
            : null;

        if (balloonAnchor != null && balloon == null)
            Debug.LogWarning($"DialogueManager: {balloonAnchor.name} 아래에서 SpeechBalloonController를 찾지 못했습니다.");

        foreach (DialogueLine line in sequence.lines)
        {
            if (line.speaker != null && line.speaker.isPartyMember)
            {
                // 파티 대사창은 한 줄 N자 / 페이지당 M줄로 자동 분할.
                // 페이지마다 한 번씩 진행 입력을 받는다 (한 DialogueLine이 여러 페이지로 보일 수 있음).
                List<string> pages = DialogueTextWrapper.SplitIntoPages(line.text, maxCharsPerLine, maxLinesPerPage);
                foreach (string page in pages)
                {
                    partyPanel.ShowPage(line, page);
                    advanceRequested = false;
                    yield return new WaitUntil(() => advanceRequested);
                }
                continue;
            }
            else if (balloon != null)
            {
                balloon.Show(line);
            }
            else
            {
                Debug.LogWarning("DialogueManager: 파티원이 아닌 화자인데 balloonAnchor가 없습니다. 대사를 건너뜁니다.");
            }

            advanceRequested = false;
            yield return new WaitUntil(() => advanceRequested);
        }

        partyPanel.Hide();
        if (balloon != null) balloon.Hide();

        isPlaying = false;
        onComplete?.Invoke();
    }
}
