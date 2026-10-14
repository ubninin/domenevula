/// <summary>탄막 충돌 계산용 순수 수학 (Unity 의존성 없음).</summary>
public static class BattleMath
{
    /// <summary>
    /// 선분 A→B 와 "원점을 중심으로 한 반지름 r 원"이 겹치는지.
    /// 탄(총알)과 플레이어가 둘 다 움직이므로, 호출하는 쪽에서
    ///   A = (프레임 시작 시 탄 위치) - (프레임 시작 시 플레이어 위치)
    ///   B = (프레임 끝 시 탄 위치)   - (프레임 끝 시 플레이어 위치)
    /// 로 "플레이어 기준 상대 좌표"를 넘긴다. 이러면 한 프레임에 아무리 빨리 지나가도 관통(터널링)하지 않는다.
    /// </summary>
    public static bool SegmentHitsOrigin(float ax, float ay, float bx, float by, float r)
    {
        float dx = bx - ax;
        float dy = by - ay;
        float len2 = dx * dx + dy * dy;

        float t = 0f;
        if (len2 > 1e-8f)
        {
            t = -(ax * dx + ay * dy) / len2;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
        }

        float px = ax + dx * t;
        float py = ay + dy * t;
        return px * px + py * py <= r * r;
    }
}
