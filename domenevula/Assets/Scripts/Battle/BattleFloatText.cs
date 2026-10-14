using TMPro;
using UnityEngine;

/// <summary>위로 떠오르며 사라지는 짧은 글자 ("-8", "+6 TP" 등). 스스로 사라진다.</summary>
public class BattleFloatText : MonoBehaviour
{
    private TextMeshProUGUI text;
    private Vector2 start;
    private float elapsed;
    private const float Life = 0.9f;
    private const float Rise = 60f;

    public static void Spawn(Transform parent, Vector2 anchoredPos, string message, Color color,
                             TMP_FontAsset font, float fontSize = 40f)
    {
        TextMeshProUGUI t = BattleUIFactory.MakeText("FloatText", parent, anchoredPos, new Vector2(300f, 60f),
                                                     message, fontSize, color, font);
        var f = t.gameObject.AddComponent<BattleFloatText>();
        f.text = t;
        f.start = anchoredPos;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float k = Mathf.Clamp01(elapsed / Life);
        ((RectTransform)transform).anchoredPosition = start + Vector2.up * (Rise * k);

        Color c = text.color;
        c.a = 1f - k * k;
        text.color = c;

        if (elapsed >= Life) Destroy(gameObject);
    }
}
