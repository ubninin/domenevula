using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ESC로 열고 닫는 메인 메뉴 (RPG Maker 식 커맨드 목록: 상태/아이템/장전/저장/종료).
///
/// 씬 배치: GameMenuRoot(이미 있음, Canvas_Screen의 자식)에 이 스크립트를 붙인다.
/// 인스펙터 연결:
///   commandPanel    ← 메뉴를 열면 보여줄 커맨드 목록 패널(배경 + 커맨드 텍스트들을 담은 오브젝트).
///                      평소엔 비활성화해서 꺼둔 채로 둬도 됨(Awake에서 자동으로 꺼짐).
///   cursorHighlight ← 커맨드 목록에서 선택된 줄 위에 올라갈 하이라이트 이미지(선택, 비워도 동작함).
///   commandTexts    ← 상태/아이템/장전/저장/종료 순서로 텍스트(TMP_Text) 5개를 연결.
///   partyStatusRoot ← PartyStatusRoot. "상태"를 선택하면 이걸 켜고 PartyStatusCursor로 넘긴다.
///   modeLabel       ← (선택) 메뉴 안에 현재 모드(상호작용/슈팅)를 보여줄 텍스트.
///
/// 조작: ↑/↓ 또는 W/S로 커맨드 이동, Enter/Z/Space로 확인, ESC로 뒤로가기(또는 메뉴 닫기).
/// "상태" 화면에서는 A/D 또는 ←/→로 파티원을 고르고(PartyStatusCursor), ESC로 커맨드 목록에 돌아간다.
/// </summary>
public class GameMenuController : MonoBehaviour
{
    public static GameMenuController Instance { get; private set; }

    private enum Command { Status, Item, Reload, Save, Quit }

    [SerializeField] private GameObject commandPanel;
    [SerializeField] private RectTransform cursorHighlight;
    [SerializeField] private TMP_Text[] commandTexts; // 상태/아이템/장전/저장/종료 순서
    [SerializeField] private GameObject partyStatusRoot;
    [SerializeField] private TMP_Text modeLabel;

    [Tooltip("메뉴가 열려 있는 동안 플레이어 이동/상호작용을 막을지")]
    [SerializeField] private bool lockMovementWhileOpen = true;

    private bool isOpen;
    private bool inSubmenu; // "상태" 등 서브 화면이 열려있는 중인지
    private int selectedIndex;
    private float ignoreInputUntil; // 서브메뉴 전환 직후, 그 전환을 일으킨 키 입력이 다음 화면에서 또 처리되는 것 방지

    public bool IsOpen => isOpen;
    public bool LocksMovement => lockMovementWhileOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (commandPanel != null) commandPanel.SetActive(false);
        if (partyStatusRoot != null) partyStatusRoot.SetActive(false);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        bool escPressed = kb.escapeKey.wasPressedThisFrame;

        // 메뉴는 대사 중에도 열 수 있어야 하므로 GameplayLock을 거치지 않고 ESC를 직접 확인한다.
        if (!isOpen)
        {
            if (escPressed) Open();
            return;
        }

        if (Time.unscaledTime < ignoreInputUntil) return;

        if (inSubmenu)
        {
            UpdateStatusSubmenu(kb, escPressed);
            return;
        }

        if (escPressed) { Close(); return; }

        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Move(-1);
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Move(1);
        if (WasConfirmPressed(kb)) Confirm();
    }

    private static bool WasConfirmPressed(Keyboard kb)
    {
        return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
            || kb.zKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
    }

    private void Open()
    {
        isOpen = true;
        inSubmenu = false;
        selectedIndex = 0;
        if (commandPanel != null) commandPanel.SetActive(true);
        RefreshHighlight();
        RefreshModeLabel();
    }

    private void Close()
    {
        isOpen = false;
        inSubmenu = false;
        if (commandPanel != null) commandPanel.SetActive(false);
        if (partyStatusRoot != null) partyStatusRoot.SetActive(false);
    }

    private void Move(int dir)
    {
        int count = commandTexts != null ? commandTexts.Length : 0;
        if (count == 0) return;
        selectedIndex = (selectedIndex + dir + count) % count;
        RefreshHighlight();
    }

    private void RefreshHighlight()
    {
        if (cursorHighlight == null || commandTexts == null || commandTexts.Length == 0) return;
        if (selectedIndex < 0 || selectedIndex >= commandTexts.Length || commandTexts[selectedIndex] == null) return;

        RectTransform target = commandTexts[selectedIndex].rectTransform;
        Vector3 pos = cursorHighlight.position;
        pos.y = target.position.y;
        cursorHighlight.position = pos;
    }

    private void RefreshModeLabel()
    {
        if (modeLabel == null) return;
        modeLabel.text = MapShootModeController.CurrentMode == MapShootModeController.Mode.Shoot
            ? "현재 모드: 슈팅"
            : "현재 모드: 상호작용";
    }

    private void Confirm()
    {
        if (commandTexts == null || selectedIndex < 0 || selectedIndex >= commandTexts.Length) return;

        switch ((Command)selectedIndex)
        {
            case Command.Status: OpenStatusSubmenu(); break;
            case Command.Item: Debug.Log("[Menu] 아이템 - 아직 준비 중"); break;
            case Command.Reload: Debug.Log("[Menu] 장전 - 아직 준비 중"); break;
            case Command.Save: SaveSystem.Save(); break;
            case Command.Quit: ConfirmQuit(); break;
        }
    }

    private void OpenStatusSubmenu()
    {
        inSubmenu = true;
        if (partyStatusRoot != null) partyStatusRoot.SetActive(true);

        PartyStatusCursor cursor = partyStatusRoot != null ? partyStatusRoot.GetComponent<PartyStatusCursor>() : null;
        cursor?.ResetSelection();

        ignoreInputUntil = Time.unscaledTime + 0.15f;
    }

    private void UpdateStatusSubmenu(Keyboard kb, bool escPressed)
    {
        // ESC, 또는 "상태" 커맨드를 한 번 더 확인(Enter/Z/Space)하면 상태창을 닫는다 (토글).
        if (escPressed || WasConfirmPressed(kb))
        {
            inSubmenu = false;
            if (partyStatusRoot != null) partyStatusRoot.SetActive(false);
            ignoreInputUntil = Time.unscaledTime + 0.15f;
            return;
        }

        PartyStatusCursor cursor = partyStatusRoot != null ? partyStatusRoot.GetComponent<PartyStatusCursor>() : null;
        cursor?.HandleInput();
    }

    private void ConfirmQuit()
    {
        // TODO: "정말 종료하시겠습니까?" 확인창 UI가 준비되면 바로 종료하지 말고 그걸 먼저 띄우도록 바꿀 것.
        Debug.Log("[Menu] 게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
