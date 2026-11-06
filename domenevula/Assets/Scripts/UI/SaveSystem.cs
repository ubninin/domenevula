using UnityEngine;

/// <summary>
/// 임시 저장 시스템. 실제 세이브 데이터 구조(파티 상태, 진행도 등)가 정해지기 전까지,
/// "저장했다"는 사실과 시각만 PlayerPrefs에 남겨서 메뉴의 저장 커맨드가 동작은 하게 해둔다.
/// 나중에 진짜 세이브 시스템을 만들면 이 클래스 내부만 교체하면 됨 (호출부는 그대로 Save()/HasSave()).
/// </summary>
public static class SaveSystem
{
    private const string SaveKey = "domenevula_save_exists";
    private const string SaveTimeKey = "domenevula_save_time";

    public static void Save()
    {
        PlayerPrefs.SetInt(SaveKey, 1);
        PlayerPrefs.SetString(SaveTimeKey, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        PlayerPrefs.Save();
        Debug.Log("[Save] 임시 저장 완료 (실제 진행 데이터는 아직 저장되지 않음 - 세이브 시스템 설계 전 임시 동작)");
    }

    public static bool HasSave() => PlayerPrefs.GetInt(SaveKey, 0) == 1;
}
