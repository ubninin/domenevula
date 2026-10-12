using UnityEngine;

/// <summary>
/// DepthScale이 쓰는 값(minY/maxY/closeScale/farScale)을 여러 캐릭터가 공유하기 위한 에셋.
/// 이 에셋 하나를 Player, 파티원 전부의 DepthScale에 똑같이 연결해두면,
/// 이 에셋 값만 바꿔도 모든 캐릭터에 동시에 적용된다.
///
/// 만드는 법: Project 창에서 우클릭 → Create → 도메네뷸라 → Depth Scale Settings
/// </summary>
[CreateAssetMenu(fileName = "DepthScaleSettings", menuName = "도메네뷸라/Depth Scale Settings")]
public class DepthScaleSettings : ScriptableObject
{
    [Header("깊이로 쓸 Y 범위 (맵에 맞게 조정)")]
    public float minY = -5f;   // 화면에서 가장 가까운(아래) 위치의 Y
    public float maxY = 5f;    // 화면에서 가장 먼(위) 위치의 Y

    [Header("배율")]
    public float closeScale = 1f;    // minY에 있을 때 크기
    public float farScale = 0.85f;   // maxY에 있을 때 크기
}
