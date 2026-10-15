using System.Collections.Generic;

/// <summary>
/// RPG Maker의 스위치/변수에 해당하는 전역 저장소.
/// 아주 단순한 static Dictionary라서 씬이 바뀌어도 유지되지만, 게임을 끄면 사라진다
/// (세이브/로드까지 필요해지면 이 클래스의 Dictionary를 JSON으로 저장/복원하면 됨).
///
/// 이벤트 커맨드(SetSwitchCommand, ConditionalBranchCommand 등)가 여기에 읽고 쓴다.
/// </summary>
public static class GameFlags
{
    private static readonly Dictionary<string, bool> Switches = new Dictionary<string, bool>();
    private static readonly Dictionary<string, int> Variables = new Dictionary<string, int>();

    public static bool GetSwitch(string key) => Switches.TryGetValue(key, out bool v) && v;
    public static void SetSwitch(string key, bool value) => Switches[key] = value;

    public static int GetVariable(string key) => Variables.TryGetValue(key, out int v) ? v : 0;
    public static void SetVariable(string key, int value) => Variables[key] = value;
    public static void AddVariable(string key, int delta) => Variables[key] = GetVariable(key) + delta;
}
