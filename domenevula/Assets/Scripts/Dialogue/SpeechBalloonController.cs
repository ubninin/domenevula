using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas_SpeechBalloon 프리팹(파티 외 모든 대사용 말풍선)에 붙이는 컴포넌트.
/// NPC 하위에 이 프리팹을 자식으로 붙여서 사용한다.
///
/// 붙이는 곳: Canvas_SpeechBalloon.prefab의 최상위 오브젝트
/// 인스펙터 연결:
///   balloonText  ← SpeechBalloonText
///   bubbleRoot   ← SpeechBalloonRoot (Bubble과 Tail의 공통 부모. 이걸 꺼야 꼬리까지 같이 사라짐)
///   bubbleImage  ← SpeechBalloonBubble의 Image (줄마다 이미지 교체 기능을 안 쓸 거면 비워둬도 됨)
///   tailImage    ← SpeechBalloonTail의 Image (위와 동일)
/// </summary>
public class SpeechBalloonController : MonoBehaviour, IDialogueDisplay
{
    [SerializeField] private TMP_Text balloonText;
    [SerializeField] private GameObject bubbleRoot;
    [SerializeField] private Image bubbleImage;
    [SerializeField] private Image tailImage;

    [Header("타자 애니메이션")]
    [SerializeField] private float charsPerSecond = 30f;

    private Sprite defaultBubble;
    private Sprite defaultTail;
    private TypewriterEffect typewriter;

    public bool IsRevealing => typewriter.IsRevealing;
    public void SkipReveal() => typewriter.SkipToEnd();

    private void Awake()
    {
        typewriter = new TypewriterEffect(this, balloonText);
        if (bubbleImage != null) defaultBubble = bubbleImage.sprite;
        if (tailImage != null) defaultTail = tailImage.sprite;
        Hide();
    }

    public void Show(DialogueLine line)
    {
        bubbleRoot.SetActive(true);
        typewriter.Play(line.text, charsPerSecond);

        if (bubbleImage != null)
        {
            Sprite bubble = line.ResolveBalloonBubble();
            bubbleImage.sprite = bubble != null ? bubble : defaultBubble;
        }

        if (tailImage != null)
        {
            Sprite tail = line.ResolveBalloonTail();
            tailImage.sprite = tail != null ? tail : defaultTail;
        }
    }

    public void Hide()
    {
        bubbleRoot.SetActive(false);
    }
}
