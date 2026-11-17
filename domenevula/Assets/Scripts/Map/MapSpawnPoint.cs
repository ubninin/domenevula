using UnityEngine;

/// <summary>
/// 맵 안에서 플레이어가 "도착하는 위치". 씬 이동 오브젝트(Move)의 Spawn Id 에 이 Id 를 적으면 그 위치에 선다.
/// Id 를 비워서 이동하면 Is Default 가 체크된 지점(없으면 맨 처음 찾은 지점)에 선다.
/// 메뉴: GameObject > 도메네뷸라 > 도착 지점 (Spawn Point)
/// </summary>
public class MapSpawnPoint : MonoBehaviour
{
    [Tooltip("도착 지점 이름. 예: FromHallway, FromClassroom. 맵 안에서 겹치지 않게")]
    public string id = "Default";

    [Tooltip("Id 를 지정하지 않고 이동했을 때(또는 이 맵을 처음 시작할 때) 서는 기본 지점")]
    public bool isDefault;

    private void OnDrawGizmos()
    {
        Gizmos.color = isDefault ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position + Vector3.left * 0.4f, transform.position + Vector3.right * 0.4f);
        Gizmos.DrawLine(transform.position + Vector3.down * 0.4f, transform.position + Vector3.up * 0.4f);
    }
}
