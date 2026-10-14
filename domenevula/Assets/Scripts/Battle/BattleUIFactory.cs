using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 UI를 코드로 만들 때 쓰는 작은 도우미들.
/// 전투 화면은 전부 Canvas_Screen(Screen Space Overlay, 1600x900 기준) 위의 uGUI로 그린다.
/// → 카메라/렌더 파이프라인 설정과 상관없이 항상 보인다.
/// </summary>
public static class BattleUIFactory
{
    private static Sprite circleSprite;

    /// <summary>하얀 원 스프라이트 (탄, 플레이어 표시용). 처음 쓸 때 한 번만 만든다.</summary>
    public static Sprite CircleSprite
    {
        get
        {
            if (circleSprite == null) circleSprite = BuildCircle(64);
            return circleSprite;
        }
    }

    private static Sprite BuildCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f - 1f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sp.name = "BattleCircle";
        return sp;
    }

    /// <summary>중앙 앵커 RectTransform 하나를 만든다. anchoredPosition은 부모 중심 기준.</summary>
    public static RectTransform MakeRect(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return rt;
    }

    /// <summary>부모를 꽉 채운다.</summary>
    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    public static Image MakeImage(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
                                  Color color, Sprite sprite = null)
    {
        RectTransform rt = MakeRect(name, parent, anchoredPos, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static TextMeshProUGUI MakeText(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
                                           string text, float fontSize, Color color, TMP_FontAsset font,
                                           TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        RectTransform rt = MakeRect(name, parent, anchoredPos, size);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    /// <summary>
    /// 게이지 바. 배경 위에 fill이 왼쪽부터 차오른다.
    /// (Image.fillAmount는 스프라이트가 있어야 먹기 때문에, fill의 앵커로 길이를 조절한다)
    /// </summary>
    public static RectTransform MakeBar(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
                                        Color backColor, Color fillColor)
    {
        Image back = MakeImage(name, parent, anchoredPos, size, backColor);
        Image fill = MakeImage("Fill", back.transform, Vector2.zero, Vector2.zero, fillColor);
        Stretch(fill.rectTransform, 2f);
        return fill.rectTransform;
    }

    /// <summary>MakeBar가 돌려준 fill을 0~1 비율로 채운다.</summary>
    public static void SetBarRatio(RectTransform fill, float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(ratio, 1f);
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(ratio >= 1f ? -2f : 0f, -2f);
    }
}
