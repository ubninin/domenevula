using TMPro;
using UnityEngine;

/// <summary>
/// Canvas_SpeechBalloon 프리팹(파티 외 모든 대사용 말풍선)에 붙이는 컴포넌트.
/// NPC 하위에 이 프리팹을 자식으로 붙여서 사용한다.
///
/// 붙이는 곳: Canvas_SpeechBalloon.prefab의 최상위 오브젝트
/// 인스펙터 연결: balloonText ← SpeechBalloonText, bubbleRoot ← SpeechBalloonBubble
/// (SpeechBalloonBubble 쪽을 끄고 켜야 꼬리/배경까지 같이 사라짐)
/// </summary>
public class SpeechBalloonController : MonoBehaviour
{
    [SerializeField] private TMP_Text balloonText;
    [SerializeField] private GameObject bubbleRoot;

    private void Awake()
    {
        Hide();
    }

    public void Show(DialogueLine line)
    {
        balloonText.text = line.text;
        bubbleRoot.SetActive(true);
    }

    public void Hide()
    {
        bubbleRoot.SetActive(false);
    }
}
