using System.Collections;
using UnityEngine;

/// <summary>
/// 이벤트 커맨드 하나가 실행에 필요한 정보. MapEvent가 채워서 각 커맨드에 넘겨준다.
/// </summary>
public class EventExecutionContext
{
    public MapEvent Self;       // 이 이벤트를 발생시킨 MapEvent (self switch, 자기 위치 등에 사용)
    public Transform Player;    // 상호작용한 플레이어의 Transform
}

/// <summary>
/// 이벤트 커맨드 하나의 기본형. RPG Maker 이벤트 에디터의 "커맨드 한 줄"에 해당한다.
/// 전부 코루틴(IEnumerator)으로 실행되기 때문에, "대사 출력 후 다 읽을 때까지 대기" 같은
/// 시간이 걸리는 동작도 자연스럽게 순서대로 이어서 쓸 수 있다.
///
/// 새 커맨드 종류를 추가하고 싶으면: 이 클래스를 상속받는 새 [Serializable] 클래스를 만들고
/// Execute()만 구현하면 된다. MapEvent의 커맨드 리스트에 자동으로 "+"로 추가할 수 있게 된다
/// (Unity 2021.2+의 SerializeReference 목록 기능).
/// </summary>
[System.Serializable]
public abstract class EventCommand
{
    public abstract IEnumerator Execute(EventExecutionContext ctx);
}
