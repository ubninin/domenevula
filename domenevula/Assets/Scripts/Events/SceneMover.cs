using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어가 이 오브젝트(Trigger 콜라이더)에 닿으면 지정한 씬으로 이동한다.
/// 메뉴: GameObject > 도메네뷸라 > 씬 이동 오브젝트(Move) 로 만들면 콜라이더까지 한 번에 세팅된다.
/// 씬 이름은 에디터에서 Target Scene 칸에 씬 파일을 끌어다 놓으면 자동으로 채워진다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SceneMover : MonoBehaviour
{
#if UNITY_EDITOR
    [Tooltip("이동할 씬 파일. 여기에 끌어다 놓으면 아래 Scene Name 이 자동으로 채워진다")]
    [SerializeField] private UnityEditor.SceneAsset targetScene;
#endif
    [Tooltip("이동할 씬 이름 (Target Scene 을 넣으면 자동 입력). 씬은 Build Settings 에 등록돼 있어야 한다 (Play 시 자동 등록됨)")]
    [SerializeField] private string sceneName;

    [Header("화면 전환 효과")]
    [Tooltip("체크하면 이동할 때 화면이 어두워졌다가(페이드아웃) 새 맵에서 다시 밝아진다(페이드인). 끄면 바로 이동")]
    [SerializeField] private bool useFade = true;
    [SerializeField] private Color fadeColor = Color.black;
    [Tooltip("화면이 덮이는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeOutDuration = 0.4f;
    [Tooltip("새 맵에서 화면이 걷히는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeInDuration = 0.4f;

    /// <summary>씬 이동 중인지. 이동 중에는 조작을 막는다 (GameplayLock 이 확인).</summary>
    public static bool Transitioning { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Transitioning = false; }

    private bool loading;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetScene != null) sceneName = targetScene.name;
    }
#endif

    private void OnTriggerStay2D(Collider2D other) => TryMove(other);

    private void TryMove(Collider2D other)
    {
        if (loading || string.IsNullOrEmpty(sceneName)) return;
        // 실제로 조작하는 플레이어만 (파티 팔로워나 가구는 무시)
        if (other.GetComponentInParent<PlayerMovement2D>() == null) return;
        // 전투/대사/이벤트 중에는 이동하지 않는다 (끝나면 계속 닿아 있을 때 이동)
        if (GameplayLock.IsActionLocked()) return;

        loading = true;
        Transitioning = true;
        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        if (useFade)
            yield return ScreenFader.FadeOutRoutine(fadeColor, fadeOutDuration);

        // 새 씬이 켜지면 이 오브젝트는 사라지므로, 페이드인은 씬 로드 이벤트에서 (사라지지 않는 ScreenFader 로) 실행한다.
        float fadeIn = fadeInDuration;
        bool fade = useFade;
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = null;
        onLoaded = (scene, mode) =>
        {
            SceneManager.sceneLoaded -= onLoaded;
            Transitioning = false;
            if (fade) ScreenFader.FadeIn(fadeIn);
        };
        SceneManager.sceneLoaded += onLoaded;

        SceneManager.LoadScene(sceneName);
    }
}
