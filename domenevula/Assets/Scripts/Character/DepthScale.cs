using UnityEngine;

/// <summary>
/// Y값이 커질수록(화면 위쪽/멀리 갈수록) 캐릭터를 조금씩 작게 그려서
/// 사이드뷰에서 원근감이 나게 만든다. 반대로 Y가 작을수록(화면 아래/가까이) 원래 크기에 가깝게.
///
/// 붙이는 곳: Player 오브젝트 (스프라이트가 직접 달린 오브젝트, 즉 CharacterSpriteAnimator와 같은 곳)
///
/// 주의: 이 스크립트는 transform.localScale을 직접 건드린다.
/// 나중에 Player에 Collider나 Rigidbody를 추가해서 크기가 물리 판정에 영향을 주면 안 되는 상황이 되면,
/// 스프라이트+그림자만 담은 자식 오브젝트(예: "Visual")를 하나 만들어 그 쪽에 이 스크립트를 옮겨 붙이는 게 안전함.
/// 지금처럼 Player 루트에 SpriteRenderer만 있는 상태라면 그대로 둬도 문제없음.
///
/// 값 맞추는 법:
/// 1. Play 모드에서 캐릭터를 맵의 가장 아래쪽(화면 앞쪽)과 가장 위쪽(화면 안쪽)으로 이동시켜 본다.
/// 2. minY/maxY에 그 두 Y 좌표를 넣는다.
/// 3. closeScale(가까울 때, 보통 1)과 farScale(멀 때, 보통 0.75~0.9 사이)을 눈으로 보면서 조정한다.
/// </summary>
public class DepthScale : MonoBehaviour
{
    [Header("깊이로 쓸 Y 범위 (맵에 맞게 조정)")]
    [SerializeField] private float minY = -5f;   // 화면에서 가장 가까운(아래) 위치의 Y
    [SerializeField] private float maxY = 5f;    // 화면에서 가장 먼(위) 위치의 Y

    [Header("배율")]
    [SerializeField] private float closeScale = 1f;    // minY에 있을 때 크기
    [SerializeField] private float farScale = 0.85f;   // maxY에 있을 때 크기

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void LateUpdate()
    {
        float t = Mathf.InverseLerp(minY, maxY, transform.position.y); // 0(가까움)~1(멈)
        float scale = Mathf.Lerp(closeScale, farScale, t);
        transform.localScale = baseScale * scale;
    }
}
