using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 메뉴의 "상태" 화면에서 파티원 슬롯 사이를 상하좌우로 이동하는 커서 (WASD / 방향키).
/// 선택된 슬롯만 PartySlotUI.SetSelected(true)로 오른쪽으로 튀어나온다.
///
/// 이동 규칙: 누른 방향에 있는 슬롯 중 가장 가까운 것으로 이동한다(화면상 위치 기준).
/// 그 방향에 슬롯이 없으면, 위/아래는 반대쪽 끝으로 순환하고 좌/우는 그대로 둔다.
///
/// 붙이는 곳: PartyStatusRoot
/// GameMenuController가 "상태"를 열면 ResetSelection()을, 열려있는 동안 매 프레임 HandleInput()을 호출한다.
/// </summary>
public class PartyStatusCursor : MonoBehaviour
{
    private PartySlotUI[] slots;
    private int selectedIndex;

    private void Awake()
    {
        slots = GetComponentsInChildren<PartySlotUI>(true);
    }

    /// <summary>이 화면이 열릴 때마다 호출. 첫 슬롯부터 다시 선택한다.</summary>
    public void ResetSelection()
    {
        if (slots == null) slots = GetComponentsInChildren<PartySlotUI>(true);
        selectedIndex = 0;
        for (int i = 0; i < slots.Length; i++) slots[i].Refresh(i); // HP/MP 게이지 값 갱신
        ApplySelection();
    }

    public void HandleInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || slots == null || slots.Length == 0) return;

        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Move(Vector2.up);
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Move(Vector2.down);
        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Move(Vector2.left);
        if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Move(Vector2.right);
    }

    private void Move(Vector2 dir)
    {
        Vector2 from = slots[selectedIndex].RestWorldPosition;
        int best = -1;
        float bestScore = float.MaxValue;

        for (int i = 0; i < slots.Length; i++)
        {
            if (i == selectedIndex) continue;
            Vector2 delta = slots[i].RestWorldPosition - from;
            float along = Vector2.Dot(delta, dir);
            if (along <= 1f) continue; // 그 방향에 있는 슬롯만
            float across = Mathf.Abs(Vector2.Dot(delta, new Vector2(dir.y, dir.x)));
            float score = along + across * 2f; // 같은 줄/열에 가까운 슬롯을 우선
            if (score < bestScore) { bestScore = score; best = i; }
        }

        // 그 방향에 없으면: 위/아래만 반대쪽 끝으로 순환
        if (best < 0 && dir.y != 0f)
        {
            float far = float.MinValue;
            for (int i = 0; i < slots.Length; i++)
            {
                if (i == selectedIndex) continue;
                float v = -Vector2.Dot(slots[i].RestWorldPosition, dir); // 반대쪽일수록 큼
                if (v > far) { far = v; best = i; }
            }
        }

        if (best < 0) return;
        selectedIndex = best;
        ApplySelection();
    }

    private void ApplySelection()
    {
        for (int i = 0; i < slots.Length; i++)
            slots[i].SetSelected(i == selectedIndex);
    }
}
