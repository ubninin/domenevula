using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Core 씬(플레이어, 파티, 카메라, UI, 대사/퀘스트 시스템이 들어 있는 씬)에 하나만 두는 맵 관리자.
/// 맵 씬들은 이 관리자가 Additive 로 불러오고 이전 맵은 내린다. Core 씬은 계속 켜져 있으므로
/// 플레이어/파티/메뉴 상태가 맵을 옮겨도 그대로 유지된다.
///
/// 맵 이동: MapManager.Instance.GoTo("맵씬이름", "도착지점Id")  (씬 이동 오브젝트 SceneMover 가 이걸 부른다)
/// </summary>
public class MapManager : MonoBehaviour
{
    public const string CoreSceneName = "Core";

    public static MapManager Instance { get; private set; }

    /// <summary>맵 이동(페이드 포함) 중인지. GameplayLock 이 보고 조작을 막는다.</summary>
    public static bool Transitioning { get; private set; }

    /// <summary>지금 활성 맵 씬 이름.</summary>
    public static string CurrentMapName { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        Transitioning = false;
        CurrentMapName = null;
    }

#if UNITY_EDITOR
    [Tooltip("게임을 처음 시작할 때 불러올 맵 씬 (여기에 씬 파일을 끌어다 놓으면 아래 Start Map Name 이 자동으로 채워진다)")]
    [SerializeField] private UnityEditor.SceneAsset startMap;
#endif
    [Tooltip("게임 시작 시 불러올 맵 씬 이름. 맵 씬을 직접 열고 Play 한 경우에는 그 맵을 그대로 쓴다")]
    [SerializeField] private string startMapName;
    [Tooltip("시작 맵에서 설 도착 지점 Id (비우면 기본 지점)")]
    [SerializeField] private string startSpawnId;

    private Scene currentMap;
    private Bounds currentBounds;
    private bool hasCurrentBounds;

    /// <summary>지금 맵(MapInfo)의 범위. MapInfo 가 없으면 false.</summary>
    public bool TryGetCurrentBounds(out Bounds bounds)
    {
        bounds = currentBounds;
        return hasCurrentBounds;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (startMap != null) startMapName = startMap.name;
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private IEnumerator Start()
    {
        // 이미 맵 씬이 같이 켜져 있으면(맵 씬을 열고 Play 한 경우) 그걸 현재 맵으로 쓴다.
        Scene existing = FindLoadedMapScene();
        if (existing.IsValid())
        {
            currentMap = existing;
            CurrentMapName = existing.name;
            SceneManager.SetActiveScene(existing);
            yield return null; // 맵 쪽 Awake/Start 가 끝난 뒤
            ApplyMapSetup(existing, startSpawnId, adopting: true);
            yield break;
        }

        if (string.IsNullOrEmpty(startMapName))
        {
            Debug.LogWarning("MapManager: Start Map 이 비어 있어 맵을 불러오지 않았습니다.");
            yield break;
        }
        yield return MoveRoutine(startMapName, startSpawnId, false, Color.black, 0f, 0f);
    }

    private Scene FindLoadedMapScene()
    {
        foreach (MapInfo info in FindObjectsByType<MapInfo>(FindObjectsSortMode.None))
            if (info.gameObject.scene != gameObject.scene) return info.gameObject.scene;
        return default;
    }

    /// <summary>다른 맵으로 이동. 이동 중이면 무시한다.</summary>
    public void GoTo(string mapSceneName, string spawnId = null, bool fade = true,
                     Color? fadeColor = null, float fadeOutSeconds = 0.4f, float fadeInSeconds = 0.4f)
    {
        if (Transitioning) return;
        StartCoroutine(MoveRoutine(mapSceneName, spawnId, fade, fadeColor ?? Color.black, fadeOutSeconds, fadeInSeconds));
    }

    private IEnumerator MoveRoutine(string mapSceneName, string spawnId, bool fade, Color color, float outSec, float inSec)
    {
        Transitioning = true;

        if (fade) yield return ScreenFader.FadeOutRoutine(color, outSec);

        Scene next;
        if (currentMap.IsValid() && currentMap.isLoaded && currentMap.name == mapSceneName)
        {
            // 같은 맵 안에서 다른 도착 지점으로 이동 — 씬은 다시 불러오지 않는다.
            next = currentMap;
        }
        else
        {
            Scene old = currentMap;

            AsyncOperation load = SceneManager.LoadSceneAsync(mapSceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"MapManager: '{mapSceneName}' 씬을 불러오지 못했습니다. Build Settings 에 등록돼 있는지 확인하세요.");
                if (fade) yield return ScreenFader.FadeInRoutine(inSec);
                Transitioning = false;
                yield break;
            }
            yield return load;

            next = SceneManager.GetSceneByName(mapSceneName);
            SceneManager.SetActiveScene(next);

            if (old.IsValid() && old.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(old);
                if (unload != null) yield return unload;
            }

            currentMap = next;
            CurrentMapName = next.name;
            yield return null; // 새 맵의 Start 들이 돌 시간
        }

        ApplyMapSetup(next, spawnId, adopting: false);

        if (fade) yield return ScreenFader.FadeInRoutine(inSec);
        Transitioning = false;
    }

    /// <summary>새 맵에 맞춰 카메라 범위를 갱신하고, 플레이어와 파티를 도착 지점으로 옮긴다.</summary>
    private void ApplyMapSetup(Scene map, string spawnId, bool adopting)
    {
        MapInfo info = null;
        MapSpawnPoint spawn = null;
        MapSpawnPoint anySpawn = null, defaultSpawn = null;

        foreach (MapInfo mi in FindObjectsByType<MapInfo>(FindObjectsSortMode.None))
            if (mi.gameObject.scene == map) { info = mi; break; }

        foreach (MapSpawnPoint sp in FindObjectsByType<MapSpawnPoint>(FindObjectsSortMode.None))
        {
            if (sp.gameObject.scene != map) continue;
            if (anySpawn == null) anySpawn = sp;
            if (sp.isDefault && defaultSpawn == null) defaultSpawn = sp;
            if (!string.IsNullOrEmpty(spawnId) && sp.id == spawnId && spawn == null) spawn = sp;
        }
        if (spawn == null)
        {
            if (!string.IsNullOrEmpty(spawnId) && !adopting)
                Debug.LogWarning($"MapManager: '{map.name}' 맵에서 Id 가 '{spawnId}' 인 도착 지점을 찾지 못했습니다. 기본 지점을 사용합니다.");
            spawn = defaultSpawn != null ? defaultSpawn : (adopting ? null : anySpawn);
        }

        hasCurrentBounds = info != null && info.TryGetBounds(out currentBounds);

        CameraFollow cam = FindFirstObjectByType<CameraFollow>();
        if (cam != null)
        {
            if (info != null && info.TryGetBounds(out Bounds b)) cam.SetBounds(b);
            else cam.ClearBounds();
        }

        if (spawn != null) TeleportParty(spawn.transform.position);
        else if (!adopting) Debug.LogWarning($"MapManager: '{map.name}' 맵에 도착 지점(MapSpawnPoint)이 없어 플레이어 위치를 그대로 둡니다.");

        if (cam != null) cam.SnapToTarget();
    }

    private void TeleportParty(Vector3 position)
    {
        PlayerMovement2D mover = FindFirstObjectByType<PlayerMovement2D>();
        if (mover != null)
        {
            Vector3 p = new Vector3(position.x, position.y, mover.transform.position.z);
            mover.transform.position = p;
            Rigidbody2D rb = mover.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = p;
                rb.linearVelocity = Vector2.zero;
            }
        }

        if (PartyManager.Instance != null)
        {
            foreach (PartyManager.PartyMemberSlot slot in PartyManager.Instance.Members)
            {
                if (slot.instance == null) continue;
                Vector3 z = slot.instance.transform.position;
                slot.instance.transform.position = new Vector3(position.x, position.y, z.z);
            }
        }
        Physics2D.SyncTransforms();
    }
}
