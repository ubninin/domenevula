using System;
using UnityEngine;

/// <summary>
/// 키 하나로 바로 입력하는 단계(자리연습)인지.
///  Auto : 이 단계 단어가 전부 낱자 자음/모음(ㄱ, ㅏ …)이면 자동으로 켬 (기본값)
///  On   : 항상 켬 (단어가 전부 낱자여야 칠 수 있음)
///  Off  : 항상 끔 (Enter로 제출)
/// </summary>
public enum InstantKeyMode { Auto, On, Off }

/// <summary>난이도 단계별 단어 목록 하나. 인스펙터에서 단어를 자유롭게 추가/수정할 수 있다.</summary>
[Serializable]
public class WordTier
{
    public string name;
    public string[] words;

    [Tooltip("켜면 Enter 없이 자음/모음 키 하나만 눌러도 바로 입력된다 (자리연습). Auto = 단어가 전부 낱자 자음/모음이면 자동으로 켬")]
    public InstantKeyMode instantKey = InstantKeyMode.Auto;

    [Header("단어 고정 (선택)")]
    [Tooltip("켜면 칸의 단어가 바뀌지 않는다. 적 턴이 시작될 때 아래 'Fixed Words' 가 칸 순서대로 놓이고, 단어를 쳐서 이동/공격해도 그대로 남는다. 꺼 두면 지금까지처럼 위 Words 에서 무작위로 뽑고, 친 칸은 새 단어로 바뀐다")]
    public bool keepWords = false;

    [Tooltip("Keep Words 가 켜졌을 때 칸에 놓일 단어. 칸 순서: 왼쪽 위부터 오른쪽으로, 줄이 끝나면 다음 줄 (Element 0 = 첫 칸). 칸 수보다 적으면 남는 칸은 위 Words 에서 무작위로 채우고, 많으면 남는 단어는 안 쓴다")]
    public string[] fixedWords;

    /// <summary>고정 모드가 켜져 있고 단어가 하나라도 적혀 있는지.</summary>
    public bool UsesFixedWords
    {
        get
        {
            if (!keepWords || fixedWords == null) return false;
            foreach (string w in fixedWords) if (!string.IsNullOrEmpty(w)) return true;
            return false;
        }
    }

    public WordTier() { }

    public WordTier(string name, string[] words)
    {
        this.name = name;
        this.words = words;
    }
}
