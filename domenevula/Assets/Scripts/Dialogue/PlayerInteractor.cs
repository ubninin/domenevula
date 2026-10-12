using UnityEngine;

/// <summary>
/// 플레이어 주변의 NPCInteractable을 찾아서, 상호작용 키(대사 진행 키와 동일:
/// Enter/Z/Space/마우스 왼쪽)를 누르면 그 NPC와의 대화를 시작한다.
///
/// 붙이는 곳: Player 오브젝트
/// 콜라이더/트리거 설정 없이 거리 계산만으로 동작하므로 바로 써도 됨.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float interactRange = 1.2f;

    private NPCInteractable currentTarget;

    private void Update()
    {
        NPCInteractable nearest = FindNearestInRange();

        if (nearest != currentTarget)
        {
            if (currentTarget != null) currentTarget.SetPrompt(false);
            currentTarget = nearest;
            if (currentTarget != null) currentTarget.SetPrompt(true);
        }

        // 대사가 이미 재생 중이면(= GameplayLock이 막혀 있으면) 새로 시작하지 않는다.
        // (대사 진행 자체는 DialogueManager가 같은 키로 알아서 처리함)
        if (currentTarget == null) return;
        if (GameplayLock.IsActionLocked()) return;

        if (DialogueManager.WasInteractKeyPressedThisFrame())
        {
            DialogueManager.Instance.PlayDialogue(currentTarget.Sequence, currentTarget.transform);
        }
    }

    private NPCInteractable FindNearestInRange()
    {
        NPCInteractable best = null;
        float bestDistSqr = interactRange * interactRange;

        foreach (NPCInteractable npc in NPCInteractable.All)
        {
            if (npc == null) continue;
            float distSqr = ((Vector2)npc.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (distSqr <= bestDistSqr)
            {
                bestDistSqr = distSqr;
                best = npc;
            }
        }
        return best;
    }

    private void OnDisable()
    {
        if (currentTarget != null) currentTarget.SetPrompt(false);
        currentTarget = null;
    }
}
