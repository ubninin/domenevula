using UnityEngine;

/// <summary>
/// 카메라가 타겟(보통 Player)을 부드럽게 따라가고, 배경 이미지 바깥이 보이지 않게
/// 카메라 범위를 배경 크기 안으로 제한한다 (RPG Maker에서 맵 끝에 도달했을 때처럼).
///
/// 붙이는 곳: Main Camera
/// 인스펙터 연결:
///   target ← Player의 Transform
///   backgroundBounds ← 배경 이미지의 SpriteRenderer (연결하면 min/maxX/Y를 매 Awake마다 자동 계산)
///   (배경이 SpriteRenderer가 아니라 Tilemap이면 backgroundBounds는 비워두고
///    minX/maxX/minY/maxY를 직접 입력해도 됨)
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Tooltip("카메라가 타겟을 따라잡는 부드러움 정도. 작을수록 빠르게 딱 붙고, 클수록 느긋하게 따라감")]
    [SerializeField] private float smoothTime = 0.15f;

    [Tooltip("카메라와 타겟 사이 고정 오프셋 (보통 Z만 -10 정도로 음수를 줘서 2D 카메라 뒤쪽에 둠)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("맵 경계로 카메라 범위 제한")]
    [SerializeField] private bool useBounds = true;

    [Tooltip("배경 이미지의 SpriteRenderer를 연결하면 아래 Min/Max 값을 자동으로 계산해서 채운다. " +
             "배경이 여러 장이면 가장 바깥쪽(전체 맵 크기)을 감싸는 오브젝트 하나를 연결하면 됨")]
    [SerializeField] private SpriteRenderer backgroundBounds;

    [Tooltip("backgroundBounds를 연결했으면 이 값들은 자동으로 덮어써진다. " +
             "직접 입력하고 싶으면 backgroundBounds를 비워두면 됨")]
    [SerializeField] private float minX;
    [SerializeField] private float maxX;
    [SerializeField] private float minY;
    [SerializeField] private float maxY;

    private Vector3 velocity;
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (backgroundBounds != null)
        {
            Bounds b = backgroundBounds.bounds;
            minX = b.min.x;
            maxX = b.max.x;
            minY = b.min.y;
            maxY = b.max.y;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;

        if (useBounds)
        {
            float halfHeight = cam != null ? cam.orthographicSize : 5f;
            float halfWidth = halfHeight * (cam != null ? cam.aspect : 16f / 9f);

            // 배경이 카메라가 보여주는 폭/높이보다 작으면(Clamp min>max가 되는 경우)
            // 억지로 양쪽을 벌리는 대신 가운데에 고정해서 자리를 피한다.
            desiredPosition.x = (maxX - minX < halfWidth * 2f)
                ? (minX + maxX) * 0.5f
                : Mathf.Clamp(desiredPosition.x, minX + halfWidth, maxX - halfWidth);

            desiredPosition.y = (maxY - minY < halfHeight * 2f)
                ? (minY + maxY) * 0.5f
                : Mathf.Clamp(desiredPosition.y, minY + halfHeight, maxY - halfHeight);
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
    }
}
