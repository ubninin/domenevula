using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 화면 상단에 "OO 모드로 전환" 문구를 잠깐 띄웠다가 사라지는 알림(토스트).
///
/// 붙이는 곳: Canvas_Screen 아래에 새로 만들 UI 텍스트 오브젝트(예: ModeToastText).
/// 화면 위쪽 중앙 정도에 배치해두면 됨. 평소엔 안 보여도 상관없음 - 이 스크립트가
/// Awake에서 알파를 0으로 만들어두고 Show()가 호출될 때만 켠다 (오브젝트 자체는 항상 활성 상태로 둘 것).
/// MapShootModeController의 toast 필드에 이 오브젝트를 연결하면 됨.
/// </summary>
public class ModeToast : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private float visibleSeconds = 1.5f;
    [SerializeField] private float fadeSeconds = 0.3f;

    private CanvasGroup group;
    private Coroutine running;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
    }

    public void Show(MapShootModeController.Mode mode)
    {
        if (label != null)
            label.text = mode == MapShootModeController.Mode.Shoot ? "슈팅 모드로 전환" : "상호작용 모드로 전환";

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(visibleSeconds);
        yield return Fade(1f, 0f);
    }

    private IEnumerator Fade(float from, float to)
    {
        float dur = Mathf.Max(0.01f, fadeSeconds);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        group.alpha = to;
    }
}
