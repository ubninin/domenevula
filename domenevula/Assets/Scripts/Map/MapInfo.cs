using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 맵 씬마다 하나씩 두는 "이 맵의 정보" 오브젝트. (맵 씬의 아무 오브젝트에 붙이면 됨, 보통 빈 오브젝트 "MapInfo")
///  - 카메라가 비출 수 있는 범위(맵 크기)를 정한다. 배경 SpriteRenderer를 연결하면 그 크기를 자동으로 쓴다.
///  - 이 맵 씬을 직접 열고 Play 했을 때, Core 씬(플레이어/UI/카메라 등)이 없으면 자동으로 같이 불러온다.
/// </summary>
public class MapInfo : MonoBehaviour
{
    [Tooltip("맵 이름 (표시/구분용, 비워도 됨)")]
    public string displayName;

    [Tooltip("이 맵의 배경 스프라이트. 연결하면 그 크기가 카메라 이동 범위가 된다. 배경이 여러 장이면 전체를 감싸는 하나를 연결")]
    public SpriteRenderer background;

    [Tooltip("배경 대신 직접 범위를 정하고 싶을 때 체크 (Background 가 비어 있어도 체크하면 이 값을 쓴다)")]
    public bool useManualBounds;
    public Vector2 manualCenter = Vector2.zero;
    public Vector2 manualSize = new Vector2(20f, 12f);

    /// <summary>카메라 이동 범위. 정할 수 없으면 false (이 경우 카메라는 범위 제한 없이 움직인다).</summary>
    public bool TryGetBounds(out Bounds bounds)
    {
        if (!useManualBounds && background != null)
        {
            bounds = background.bounds;
            return true;
        }
        if (useManualBounds)
        {
            bounds = new Bounds(manualCenter, manualSize);
            return true;
        }
        bounds = default;
        return false;
    }

    private static bool coreRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { coreRequested = false; }

    private void Awake()
    {
        // 맵 씬만 단독으로 Play 한 경우: Core 씬을 같이 불러온다 (이미 열려 있으면 아무것도 안 함).
        if (coreRequested || MapManager.Instance != null) return;
        if (gameObject.scene.name == MapManager.CoreSceneName) return;
        if (SceneManager.GetSceneByName(MapManager.CoreSceneName).IsValid()) return;

        coreRequested = true;
        SceneManager.LoadScene(MapManager.CoreSceneName, LoadSceneMode.Additive);
    }
}
