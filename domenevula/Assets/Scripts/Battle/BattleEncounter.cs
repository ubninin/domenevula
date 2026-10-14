using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 한 판의 설정 묶음 (적, 공격 패턴, 단어 단계, 라운드별 칸 배치/안내 문구/말풍선 대사, 배경).
/// 대화 에셋의 "Battle" 줄에 끼워 넣으면 그 줄에서 전투가 시작된다.
///
/// 만드는 법: Project 창 우클릭 → Create → 도메네뷸라 → Battle Encounter
/// 파일 이름은 enc_(적 이름) 형식으로. 예: enc_slime_01
///
/// 새로 만들면 BattleManager 의 기본값과 같은 내용으로 채워져 있으니, 바꿀 것만 고치면 된다.
/// (파티원·수치(Settings)·폰트·말풍선 프리팹은 전투 씬의 BattleManager 에서 그대로 가져온다)
/// </summary>
[CreateAssetMenu(fileName = "enc_new", menuName = "도메네뷸라/Battle Encounter")]
public class BattleEncounter : ScriptableObject
{
    [Header("필드 ↔ 전투 전환 연출")]
    public BattleTransitionSettings transition = new BattleTransitionSettings();

    [Header("적 (이름, HP, 설명, 공격 패턴)")]
    public BattleEnemy enemy = BattleDefaults.Enemy();

    [Header("전투 배경 (비워두면 BattleManager 의 배경)")]
    public Sprite background;

    [Header("단어 단계 (적 턴에 칸에 뜨는 단어)")]
    public List<WordTier> wordTiers = BattleDefaults.WordTiers();

    [Header("라운드별 칸 배치 (비워두면 BattleManager Settings 의 Columns × Rows 고정)")]
    public List<RoundLayout> roundLayouts = new List<RoundLayout>
    {
        new RoundLayout(1, 3, 2),
        new RoundLayout(3, 3, 3),
    };

    [Header("라운드별 플레이어 턴 안내 문구 (해당하는 항목이 없으면 적의 Description)")]
    public List<RoundMessage> roundMessages = new List<RoundMessage>();

    [Header("라운드별 말풍선 대사")]
    public List<RoundLines> roundLines = BattleDefaults.DefaultRoundLines();
}
