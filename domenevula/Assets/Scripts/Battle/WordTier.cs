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

    public WordTier() { }

    public WordTier(string name, string[] words)
    {
        this.name = name;
        this.words = words;
    }
}
