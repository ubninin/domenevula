using UnityEngine;

/// <summary>
/// Y값 기준으로 SpriteRenderer의 sortingOrder를 매 프레임 갱신해서,
/// 화면 아래쪽(Y가 작은 쪽)에 있는 캐릭터가 항상 위쪽(Y가 큰 쪽) 캐릭터보다
/// 앞(위 레이어)에 그려지도록 한다. ("의사 Z축 정렬", 사이드뷰/쿼터뷰 게임에서 흔히 씀)
///
/// 붙이는 곳: 플레이어와 모든 파티원(+필요하면 NPC) 각각의 SpriteRenderer가 있는 오브젝트
/// 인스펙터 연결: spriteRenderer는 비워두면 자동으로 같은 오브젝트에서 찾음
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class YSortedRenderer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("sortingOrder = Mathf.RoundToInt(-transform.position.y * precision) + offset")]
    [SerializeField] private int precision = 100;
    [SerializeField] private int offset = 0;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * precision) + offset;
    }
}
