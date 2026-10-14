using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>화면 전환 연출 종류.</summary>
public enum TransitionStyle
{
    [InspectorName("페이드 (서서히 어두워짐)")] Fade,
    [InspectorName("세로 막대 (막대들이 차례로 화면을 덮음)")] Bars,
}

/// <summary>
/// 필드 ↔ 전투 전환 연출 설정. Battle Encounter 에셋마다 따로 바꿀 수 있다.
/// 흐름: [덮기] → (씬 교체, 가려진 채 대기) → [걷어내기]. 전투가 끝나고 필드로 돌아갈 때도 똑같이 한 번 더 한다.
/// 나중에 gif/이미지 연출을 넣을 땐 Frames 에 프레임 이미지를 순서대로 넣으면 덮이는 동안 재생된다.
/// </summary>
[Serializable]
public class BattleTransitionSettings
{
    [Tooltip("연출 종류")] public TransitionStyle style = TransitionStyle.Bars;
    [Tooltip("덮는 색 (A 는 최대 불투명도)")] public Color color = new Color(0.03f, 0.03f, 0.07f, 1f);
    [Tooltip("화면이 완전히 덮일 때까지 걸리는 시간(초)")] public float coverSeconds = 0.6f;
    [Tooltip("완전히 덮인 채 머무는 시간(초). 이 동안 씬이 바뀐다")] public float holdSeconds = 0.25f;
    [Tooltip("덮인 화면이 걷히는 시간(초)")] public float revealSeconds = 0.6f;
    [Range(2, 24)] [Tooltip("세로 막대 종류일 때 막대 개수")] public int barCount = 8;
    [Tooltip("(선택) 덮이는 동안 위에 재생할 연출 이미지들. gif 를 쓰려면 프레임별 이미지로 잘라서 순서대로 넣는다. 비워두면 사용 안 함")]
    public Sprite[] frames;
    [Tooltip("Frames 재생 속도 (초당 프레임 수)")] public float framesPerSecond = 12f;
}

/// <summary>전환 연출이 그려지는 화면 전체 덮개 (전투 Canvas 보다 위). 필요할 때 만들고 끝나면 지운다.</summary>
public class BattleTransitionOverlay
{
    private const float Stagger = 0.6f; // 막대들이 시작하는 시간차 (0 = 동시)

    private readonly BattleTransitionSettings t;
    private GameObject root;
    private Image fade;
    private RectTransform[] bars;
    private Image[] barImages;
    private Image frameImage;
    private float cover;

    public BattleTransitionOverlay(BattleTransitionSettings settings)
    {
        t = settings ?? new BattleTransitionSettings();

        root = new GameObject("BattleTransitionOverlay", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // 전투 화면(1000)과 필드 UI 위
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (t.style == TransitionStyle.Fade)
        {
            fade = BattleUIFactory.MakeImage("Fade", root.transform, Vector2.zero, Vector2.zero, new Color(t.color.r, t.color.g, t.color.b, 0f));
            BattleUIFactory.Stretch(fade.rectTransform);
        }
        else
        {
            int n = Mathf.Clamp(t.barCount, 2, 24);
            bars = new RectTransform[n];
            barImages = new Image[n];
            for (int i = 0; i < n; i++)
            {
                Image img = BattleUIFactory.MakeImage("Bar" + i, root.transform, Vector2.zero, Vector2.zero, new Color(t.color.r, t.color.g, t.color.b, t.color.a));
                RectTransform rt = img.rectTransform;
                rt.anchorMin = new Vector2((float)i / n, 0f);
                rt.anchorMax = new Vector2((float)(i + 1) / n, 0f);
                rt.offsetMin = new Vector2(-1f, 0f); // 막대 사이 틈이 비치지 않게 살짝 겹친다
                rt.offsetMax = new Vector2(1f, 0f);
                bars[i] = rt; barImages[i] = img;
            }
        }

        if (t.frames != null && t.frames.Length > 0)
        {
            frameImage = BattleUIFactory.MakeImage("Frames", root.transform, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0f));
            BattleUIFactory.Stretch(frameImage.rectTransform);
            frameImage.preserveAspect = false;
        }
        SetCover(0f);
    }

    /// <summary>0 = 완전히 걷힘, 1 = 완전히 덮임.</summary>
    public void SetCover(float p)
    {
        cover = Mathf.Clamp01(p);
        float e = cover * cover * (3f - 2f * cover); // smoothstep

        if (fade != null) fade.color = new Color(t.color.r, t.color.g, t.color.b, t.color.a * e);

        if (bars != null)
        {
            int n = bars.Length;
            for (int i = 0; i < n; i++)
            {
                // 막대마다 시작 시점을 조금씩 늦춘다 (왼쪽부터 차례로 내려오고/올라오는 느낌)
                float start = Stagger * i / (n - 1);
                float q = Mathf.Clamp01((e * (1f + Stagger) - start));
                q = q * q * (3f - 2f * q);
                RectTransform rt = bars[i];
                bool fromTop = (i % 2) == 0; // 짝수는 위에서 내려오고 홀수는 아래에서 올라온다
                if (fromTop) { rt.anchorMin = new Vector2(rt.anchorMin.x, 1f - q); rt.anchorMax = new Vector2(rt.anchorMax.x, 1f); }
                else { rt.anchorMin = new Vector2(rt.anchorMin.x, 0f); rt.anchorMax = new Vector2(rt.anchorMax.x, q); }
                barImages[i].enabled = q > 0.001f;
            }
        }

        if (frameImage != null)
        {
            Sprite[] fr = t.frames;
            int idx = Mathf.Clamp((int)(Time.unscaledTime * Mathf.Max(1f, t.framesPerSecond)) % fr.Length, 0, fr.Length - 1);
            frameImage.sprite = fr[idx];
            frameImage.color = new Color(1f, 1f, 1f, e);
            frameImage.enabled = fr[idx] != null && e > 0.001f;
        }
    }

    /// <summary>from → to 로 seconds 초 동안 덮개를 움직인다 (덮기: 0→1, 걷기: 1→0).</summary>
    public IEnumerator Animate(float from, float to, float seconds)
    {
        float dur = Mathf.Max(0.01f, seconds);
        for (float time = 0f; time < dur; time += Time.deltaTime)
        {
            SetCover(Mathf.Lerp(from, to, time / dur));
            yield return null;
        }
        SetCover(to);
    }

    public IEnumerator Cover() { return Animate(0f, 1f, t.coverSeconds); }
    public IEnumerator Reveal() { return Animate(1f, 0f, t.revealSeconds); }
    public float HoldSeconds { get { return Mathf.Max(0f, t.holdSeconds); } }

    public void Destroy()
    {
        if (root != null) UnityEngine.Object.Destroy(root);
        root = null;
    }
}
