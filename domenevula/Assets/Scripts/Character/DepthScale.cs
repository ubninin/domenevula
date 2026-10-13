using UnityEngine;

/// <summary>
/// Y값이 커질수록(화면 위쪽/멀리 갈수록) 캐릭터를 조금씩 작게 그려서
/// 사이드뷰에서 원근감이 나게 만든다. 반대로 Y가 작을수록(화면 아래/가까이) 원래 크기에 가깝게.
///
/// 붙이는 곳: Player 오브젝트, 그리고 각 파티원 오브젝트 (스프라이트가 직접 달린 오브젝트,
///          즉 CharacterSpriteAnimator와 같은 곳)
///
/// 값 공유: minY/maxY/closeScale/farScale은 더 이상 이 컴포넌트에 직접 적지 않고,
/// DepthScaleSettings 에셋 하나를 만들어서 Player와 모든 파티원의 settings 슬롯에
/// 똑같이 연결해둔다. 그러면 그 에셋 값만 바꿔도 전부 한꺼번에 적용된다.
/// (settings를 비워두면 인스펙터의 fallback 값을 그대로 쓰므로, 급하게 테스트할 땐 안 넣어도 동작함)
///
/// 주의: 이 스크립트는 transform.localScale을 직접 건드린다.
/// 나중에 Player/파티원에 Collider나 Rigidbody를 추가해서 크기가 물리 판정에 영향을 주면 안 되는
/// 상황이 되면, 스프라이트+그림자만 담은 자식 오브젝트(예: "Visual")를 하나 만들어
/// 그 쪽에 이 스크립트를 옮겨 붙이는 게 안전함.
/// 지금처럼 루트에 SpriteRenderer만 있는 상태라면 그대로 둬도 문제없음.
///
/// 값 맞추는 법:
/// 1. Play 모드에서 캐릭터를 맵의 가장 아래쪽(화면 앞쪽)과 가장 위쪽(화면 안쪽)으로 이동시켜 본다.
/// 2. DepthScaleSettings 에셋의 minY/maxY에 그 두 Y 좌표를 넣는다.
/// 3. closeScale(가까울 때, 보통 1)과 farScale(멀 때, 보통 0.75~0.9 사이)을 눈으로 보면서 조정한다.
/// </summary>
public class DepthScale : MonoBehaviour
{
    [Tooltip("Player와 모든 파티원이 같은 에셋을 연결하면 값이 자동으로 공유된다. 비워두면 아래 fallback 값을 씀.")]
    [SerializeField] private DepthScaleSettings settings;

    [Header("Fallback (settings를 안 넣었을 때만 사용)")]
    [SerializeField] private float minY = -5f;
    [SerializeField] private float maxY = 5f;
    [SerializeField] private float closeScale = 1f;
    [SerializeField] private float farScale = 0.85f;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void LateUpdate()
    {
        float useMinY = settings != null ? settings.minY : minY;
        float useMaxY = settings != null ? settings.maxY : maxY;
        float useCloseScale = settings != null ? settings.closeScale : closeScale;
        float useFarScale = settings != null ? settings.farScale : farScale;

        float t = Mathf.InverseLerp(useMinY, useMaxY, transform.position.y); // 0(가까움)~1(멈)
        float scale = Mathf.Lerp(useCloseScale, useFarScale, t);
        transform.localScale = baseScale * scale;
    }
}
