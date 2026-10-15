using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파티원(플레이어 뒤를 따라다니는 캐릭터들)을 관리.
/// 리더(플레이어) → 파티원1 → 파티원2 ... 순서로 체인을 구성해서,
/// 각 파티원은 "바로 앞 사람"을 followDistance만큼 간격 두고 쫓아간다.
///
/// 씬 배치: 빈 오브젝트(예: Systems/PartyManager)에 이 스크립트를 붙인다.
/// 인스펙터 연결:
///   player  ← 플레이어 오브젝트의 Transform
///   members ← 리스트에 파티원 슬롯 추가. 각 슬롯에 character(선택, CharacterDefinition)와
///            followerPrefab(필수, PartyFollower+CharacterSpriteAnimator+SpriteRenderer가
///            붙은 파티원 필드 캐릭터 프리팹)을 채운다.
/// 리스트 순서 = 따라오는 순서 (1번이 플레이어 바로 뒤, 2번이 1번 뒤, ...).
/// </summary>
public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    [System.Serializable]
    public class PartyMemberSlot
    {
        [Tooltip("선택 사항 (이름/식별용). 안 채워도 동작함")]
        public CharacterDefinition character;

        [Tooltip("필드에서 플레이어 뒤를 따라다닐 캐릭터 프리팹 (PartyFollower 포함)")]
        public GameObject followerPrefab;

        [HideInInspector] public GameObject instance;
    }

    [SerializeField] private Transform player;
    [SerializeField] private List<PartyMemberSlot> members = new List<PartyMemberSlot>();

    [Tooltip("각 파티원이 앞사람과 유지하는 간격")]
    [SerializeField] private float followDistance = 0.6f;

    public IReadOnlyList<PartyMemberSlot> Members => members;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SpawnAll();
    }

    private void SpawnAll()
    {
        if (player == null)
        {
            Debug.LogWarning("PartyManager: player가 연결되지 않았습니다.");
            return;
        }

        Transform previous = player;
        foreach (PartyMemberSlot slot in members)
        {
            previous = SpawnOne(slot, previous);
        }
    }

    private Transform SpawnOne(PartyMemberSlot slot, Transform followTarget)
    {
        if (slot.followerPrefab == null)
        {
            Debug.LogWarning("PartyManager: followerPrefab이 비어있는 슬롯이 있습니다.");
            return followTarget;
        }

        GameObject go = Instantiate(slot.followerPrefab, followTarget.position, Quaternion.identity);
        slot.instance = go;

        PartyFollower follower = go.GetComponent<PartyFollower>();
        if (follower != null)
        {
            follower.Configure(followTarget, followDistance);
        }
        else
        {
            Debug.LogWarning($"PartyManager: {slot.followerPrefab.name}에 PartyFollower 컴포넌트가 없습니다.");
        }

        return go.transform;
    }

    /// <summary>파티 맨 뒤에 새 멤버 추가 (합류 이벤트 등에서 사용).</summary>
    public void AddMember(CharacterDefinition character, GameObject followerPrefab)
    {
        Transform tail = GetTailTransform();

        var slot = new PartyMemberSlot { character = character, followerPrefab = followerPrefab };
        SpawnOne(slot, tail);
        members.Add(slot);
    }

    /// <summary>파티에서 캐릭터 제거 (이탈 이벤트 등에서 사용). 체인 중간 제거 시 뒷사람을 자동으로 재연결함.</summary>
    public void RemoveMember(CharacterDefinition character)
    {
        int index = members.FindIndex(m => m.character == character);
        if (index < 0) return;

        PartyMemberSlot slot = members[index];
        Transform newTargetForNext = index > 0 ? members[index - 1].instance.transform : player;

        if (slot.instance != null) Destroy(slot.instance);
        members.RemoveAt(index);

        // 제거된 자리 뒤에 있던 멤버가 있다면, 그 앞사람을 다시 연결해준다.
        if (index < members.Count)
        {
            PartyFollower nextFollower = members[index].instance.GetComponent<PartyFollower>();
            nextFollower?.Configure(newTargetForNext, followDistance);
        }
    }

    private Transform GetTailTransform()
    {
        if (members.Count == 0) return player;
        PartyMemberSlot last = members[members.Count - 1];
        return last.instance != null ? last.instance.transform : player;
    }

    /// <summary>플레이어(리더)의 Transform.</summary>
    public Transform Player => player;

    /// <summary>
    /// 지정한 Character Definition과 연결된 파티원의 실제(Play 중에 Instantiate된) Transform을 찾는다.
    /// 씬에 미리 놓아둔 원본/비활성 오브젝트가 아니라 지금 화면에 보이는 그 복제본을 돌려준다.
    /// 아직 파티에 없거나 인스턴스가 없으면 null.
    /// </summary>
    public Transform FindMemberTransform(CharacterDefinition character)
    {
        if (character == null) return null;
        foreach (PartyMemberSlot slot in members)
            if (slot.character == character && slot.instance != null)
                return slot.instance.transform;
        return null;
    }
}
