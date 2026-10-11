using UnityEngine;

/// <summary>
/// 캐릭터가 카메라가 비추는 화면 밖으로 못 나가게 위치를 막는다.
/// Main Camera가 Orthographic(2D 기본값)이라고 가정한다.
///
/// 붙이는 곳: Player 오브젝트 (PlayerMovement2D와 같은 곳)
/// 인스펙터 연결: cam을 비워두면 자동으로 Camera.main을 씀
///
/// marginX/marginY: 캐릭터 스프라이트 절반 크기 정도로 넣으면
/// 캐릭터 몸체가 화면 경계에서 짤리지 않고 딱 멈춤. 눈으로 보면서 조정.
/// </summary>
[RequireComponent(typeof(Transform))]
public class ScreenBoundsClamp2D : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private float marginX = 0.5f;
    [SerializeField] private float marginY = 0.5f;

    private Rigidbody2D rb;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        // 카메라가 비추는 월드 좌표 범위 계산 (Orthographic 기준)
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 camPos = cam.transform.position;

        float minX = camPos.x - halfWidth + marginX;
        float maxX = camPos.x + halfWidth - marginX;
        float minY = camPos.y - halfHeight + marginY;
        float maxY = camPos.y + halfHeight - marginY;

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        if (rb != null) rb.position = pos;
        else transform.position = pos;
    }
}
