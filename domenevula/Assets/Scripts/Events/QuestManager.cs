using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아주 단순한 퀘스트 진행 상태 관리자. "진행 중" / "완료" 두 상태만 ID(문자열)로 추적한다.
/// 퀘스트 내용(이름, 설명, 보상 등)은 여기 없음 - 필요해지면 QuestDefinition 같은
/// ScriptableObject를 따로 만들어서 questId와 매칭시키면 된다. 지금은 이벤트 커맨드
/// (StartQuestCommand/CompleteQuestCommand)가 상태만 바꿀 수 있게 최소한으로 만든 것.
///
/// 씬에 빈 오브젝트 하나 만들어서 붙여두면 됨 (DialogueManager처럼 싱글톤).
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public event Action<string> QuestStarted;
    public event Action<string> QuestCompleted;

    private readonly HashSet<string> activeQuests = new HashSet<string>();
    private readonly HashSet<string> completedQuests = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void StartQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId) || activeQuests.Contains(questId)) return;
        activeQuests.Add(questId);
        QuestStarted?.Invoke(questId);
    }

    public void CompleteQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        activeQuests.Remove(questId);
        completedQuests.Add(questId);
        QuestCompleted?.Invoke(questId);
    }

    public bool IsActive(string questId) => activeQuests.Contains(questId);
    public bool IsCompleted(string questId) => completedQuests.Contains(questId);
}
