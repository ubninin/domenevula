using System;
using System.Collections;
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

    [SerializeField] private PartyDialoguePanelController partyPanel;

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
                partyPanel.Show(line);
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
