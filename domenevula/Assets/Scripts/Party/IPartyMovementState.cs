/// <summary>
/// 지금 달리는 중인지(run) 알려주는 공용 인터페이스.
/// PlayerMovement2D와 PartyFollower 둘 다 구현해서,
/// 파티원이 "앞사람이 진짜 달리고 있는지"를 직접 물어볼 수 있게 한다.
/// (체인 구조라 앞사람이 플레이어든, 다른 파티원이든 똑같이 동작함)
/// </summary>
public interface IPartyMovementState
{
    bool IsRunning { get; }
}
