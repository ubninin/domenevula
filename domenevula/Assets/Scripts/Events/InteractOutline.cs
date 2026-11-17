using UnityEngine;

/// <summary>
/// 스프라이트 주위에 얇은 흰색 테두리를 그리는 인디케이터. (상호작용 가능한 오브젝트 표시용)
/// 같은 스프라이트를 흰 실루엣으로 8방향으로 살짝 어긋나게 뒤에 깔아서 테두리처럼 보이게 한다.
/// MapEvent 가 필요할 때 자동으로 붙여서 쓰므로 직접 붙일 필요 없음.
/// </summary>
public class InteractOutline : MonoBehaviour
{
    private static readonly Vector2[] Dirs =
    {
        new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
        new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
        new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
    };

    private SpriteRenderer source;
    private SpriteRenderer[] copies;
    private Material material;
    private float thickness = 0.03f;
    private Color color = Color.white;
    private bool visible;

    public void Setup(SpriteRenderer sourceRenderer, float thicknessWorld, Color outlineColor)
    {
        source = sourceRenderer;
        thickness = thicknessWorld;
        color = outlineColor;
        if (copies == null) Build();
        Apply();
    }

    public void SetVisible(bool show)
    {
        visible = show;
        Apply();
    }

    private void Build()
    {
        Shader sh = Shader.Find("Dome/SpriteWhiteSilhouette");
        material = sh != null ? new Material(sh) : null;

        copies = new SpriteRenderer[Dirs.Length];
        for (int i = 0; i < copies.Length; i++)
        {
            var go = new GameObject("InteractOutline_" + i);
            go.transform.SetParent(transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            if (material != null) sr.sharedMaterial = material;
            copies[i] = sr;
        }
    }

    private void Apply()
    {
        if (copies == null) return;
        bool on = visible && source != null && source.enabled && source.sprite != null;
        foreach (SpriteRenderer c in copies) c.enabled = on;
        if (on) Sync();
    }

    private void LateUpdate()
    {
        if (copies == null || !visible || source == null) return;
        Apply();
    }

    private void Sync()
    {
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(s.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(s.y));
        for (int i = 0; i < copies.Length; i++)
        {
            SpriteRenderer c = copies[i];
            c.sprite = source.sprite;
            c.flipX = source.flipX;
            c.flipY = source.flipY;
            c.color = color;
            c.sortingLayerID = source.sortingLayerID;
            c.sortingOrder = source.sortingOrder - 1; // 본체 바로 뒤
            Transform t = c.transform;
            t.localPosition = new Vector3(Dirs[i].x * thickness / sx, Dirs[i].y * thickness / sy, 0f);
        }
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
