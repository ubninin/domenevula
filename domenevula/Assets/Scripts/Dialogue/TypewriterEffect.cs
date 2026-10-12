using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    private readonly RectTransform layoutRootToRebuild;

    private Coroutine running;
    private int totalVisibleChars;

    public bool IsRevealing { get; private set; }

    /// <param name="owner">코루틴을 돌릴 MonoBehaviour</param>
    /// <param name="target">타자 효과를 적용할 텍스트</param>
    /// <param name="layoutRootToRebuild">
    /// 말풍선처럼 텍스트를 담은 배경이 ContentSizeFitter로 자동 크기 조정되는 경우,
    /// 그 배경 쪽 RectTransform을 넘기면 전체 텍스트를 넣는 즉시 레이아웃을 강제로
    /// 한 번 재계산해서 "최종 크기"로 바로 맞춘다. 필요 없으면 비워둬도 됨(null).
    /// </param>
    public TypewriterEffect(MonoBehaviour owner, TMP_Text target, RectTransform layoutRootToRebuild = null)
    {
        this.owner = owner;
        this.target = target;
        this.layoutRootToRebuild = layoutRootToRebuild;
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
        RebuildLayoutIfNeeded();
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

        // 말풍선 배경처럼 ContentSizeFitter가 걸린 부모가 있으면, 글자를 하나씩 보여주기
        // 시작하기 전에 "전체 텍스트 기준" 최종 크기로 미리 맞춰둔다.
        // (maxVisibleCharacters는 레이아웃 크기에 영향을 주지 않고 렌더링만 가린다.
        //  문제가 됐던 건 ContentSizeFitter가 2겹(Bubble, Text)으로 중첩돼 있어서
        //  스크립트로 텍스트를 바꾼 직후 자동 재계산이 같은 프레임에 안 따라오는 경우였음.)
        RebuildLayoutIfNeeded();

        float interval = 1f / Mathf.Max(0.01f, charsPerSecond);

        for (int shown = 1; shown <= totalVisibleChars; shown++)
        {
            target.maxVisibleCharacters = shown;
            yield return new WaitForSeconds(interval);
        }

        IsRevealing = false;
        running = null;
    }

    private void RebuildLayoutIfNeeded()
    {
        if (layoutRootToRebuild != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRootToRebuild);
    }
}
