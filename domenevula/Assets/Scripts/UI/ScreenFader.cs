using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이벤트 커맨드용 화면 전체 페이드 인/아웃 (암전 등). BattleTransitionOverlay와 달리,
/// Fade Out 한 뒤 다음 커맨드로 넘어가도 화면이 계속 덮여있어야 하므로(Fade In을 부르기 전까지 유지),
/// 매번 만들고 지우는 대신 씬에 하나만 만들어서 계속 재사용한다 (DontDestroyOnLoad).
///
/// 쓰는 법: MapEvent 커맨드 목록에 "화면 페이드아웃"/"화면 페이드인"을 추가해서 쓰면 됨.
/// 직접 코드에서 쓰고 싶으면 ScreenFader.FadeOutRoutine(color, seconds)를 yield return 하거나,
/// ScreenFader.FadeOut(color, seconds)로 기다리지 않고 바로 재생만 시켜도 된다.
/// </summary>
public static class ScreenFader
{
    private static Image overlay;
    private static ScreenFaderRunner runner;

    private static void EnsureOverlay()
    {
        if (overlay != null) return;

        var root = new GameObject("ScreenFaderOverlay", typeof(RectTransform));
        Object.DontDestroyOnLoad(root);
        runner = root.AddComponent<ScreenFaderRunner>();

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000; // 전투 전환 연출(5000)보다도 위, 거의 모든 UI 위에 그려지도록

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var imgObj = new GameObject("Fade", typeof(RectTransform));
        imgObj.transform.SetParent(root.transform, false);
        overlay = imgObj.AddComponent<Image>();
        overlay.raycastTarget = false;
        overlay.color = new Color(0f, 0f, 0f, 0f); // 처음엔 완전히 투명(안 덮인 상태)

        RectTransform rt = overlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>지금 상태에서 color(알파 포함)까지 seconds초 동안 서서히 덮는다. 기다렸다 쓰려면 yield return.</summary>
    public static IEnumerator FadeOutRoutine(Color color, float seconds)
    {
        EnsureOverlay();
        yield return Animate(overlay.color, color, seconds);
    }

    /// <summary>지금 덮인 색에서 투명하게(알파 0) 걷어낸다. 기다렸다 쓰려면 yield return.</summary>
    public static IEnumerator FadeInRoutine(float seconds)
    {
        EnsureOverlay();
        Color from = overlay.color;
        yield return Animate(from, new Color(from.r, from.g, from.b, 0f), seconds);
    }

    /// <summary>기다리지 않고 바로 재생만 시작한다 (다음 커맨드로 바로 넘어가고 싶을 때).</summary>
    public static void FadeOut(Color color, float seconds)
    {
        EnsureOverlay();
        runner.StartCoroutine(FadeOutRoutine(color, seconds));
    }

    /// <summary>위와 동일, Fade In 버전.</summary>
    public static void FadeIn(float seconds)
    {
        EnsureOverlay();
        runner.StartCoroutine(FadeInRoutine(seconds));
    }

    private static IEnumerator Animate(Color from, Color to, float seconds)
    {
        float dur = Mathf.Max(0.01f, seconds);
        for (float time = 0f; time < dur; time += Time.deltaTime)
        {
            overlay.color = Color.Lerp(from, to, time / dur);
            yield return null;
        }
        overlay.color = to;
    }
}

/// <summary>ScreenFader가 기다리지 않는 코루틴을 실행하기 위해 붙이는 빈 MonoBehaviour.</summary>
internal class ScreenFaderRunner : MonoBehaviour { }
