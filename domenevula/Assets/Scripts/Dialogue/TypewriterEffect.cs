using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// TMP_Text 하나에 한 글자씩 나타나는 타자 애니메이션을 재생하는 헬퍼.
/// MonoBehaviour가 아니라서 PartyDialoguePanelController/SpeechBalloonController가
/// 멤버 변수로 들고 쓴다 (코루틴은 owner의 StartCoroutine을 빌려서 돌림).
///
/// TMP의 maxVisibleCharacters를 이용해서 글자를 하나씩 "보이게" 하는 방식이라,
/// &lt;color=#RRGGBB&gt;...&lt;/color&gt; 같은 리치 텍스트 태그를 대사에 써도
/// 태그가 잘리거나 깨지지 않는다 (태그 자체는 글자 수로 세지 않음).
/// </summary>
public class TypewriterEffect
{
    private readonly MonoBehaviour owner;
    private readonly TMP_Text target;

    private Coroutine running;
    private int totalVisibleChars;

    public bool IsRevealing { get; private set; }

    public TypewriterEffect(MonoBehaviour owner, TMP_Text target)
    {
        this.owner = owner;
        this.target = target;
    }

    /// <param name="text">표시할 전체 텍스트 (리치 텍스트 태그 포함 가능)</param>
    /// <param name="charsPerSecond">초당 몇 글자씩 나타낼지 (태그는 세지 않음)</param>
    public void Play(string text, float charsPerSecond)
    {
        if (running != null) owner.StopCoroutine(running);
        running = owner.StartCoroutine(RevealRoutine(text, charsPerSecond));
    }

    /// <summary>재생 중이면 즉시 끝까지 표시하고 멈춘다.</summary>
    public void SkipToEnd()
    {
        if (running != null)
        {
            owner.StopCoroutine(running);
            running = null;
        }
        target.maxVisibleCharacters = totalVisibleChars;
        IsRevealing = false;
    }

    private IEnumerator RevealRoutine(string text, float charsPerSecond)
    {
        IsRevealing = true;

        // 태그 포함 전체 텍스트를 한 번에 넣고, 실제 "보이는" 글자 수(태그 제외)를 구한다.
        target.text = text ?? string.Empty;
        target.maxVisibleCharacters = 0;
        target.ForceMeshUpdate(); // textInfo.characterCount를 이번 프레임에 바로 쓰기 위해 필요

        totalVisibleChars = target.textInfo.characterCount;

        float interval = 1f / Mathf.Max(0.01f, charsPerSecond);

        for (int shown = 1; shown <= totalVisibleChars; shown++)
        {
            target.maxVisibleCharacters = shown;
            yield return new WaitForSeconds(interval);
        }

        IsRevealing = false;
        running = null;
    }
}
