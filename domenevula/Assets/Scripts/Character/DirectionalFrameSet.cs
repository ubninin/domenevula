using UnityEngine;

/// <summary>
/// 한 모션(walk 또는 idle)의 4방향 프레임 묶음.
/// 각 배열에 Sprite Editor로 슬라이스한 12개 조각을 순서대로 드래그해서 넣는다.
/// (sheet_walk_0~11 → down, sheet_walk_12~23 → left, sheet_walk_24~35 → right, sheet_walk_36~47 → up)
/// </summary>
[System.Serializable]
public class DirectionalFrameSet
{
    public Sprite[] down = new Sprite[12];
    public Sprite[] left = new Sprite[12];
    public Sprite[] right = new Sprite[12];
    public Sprite[] up = new Sprite[12];

    public Sprite[] Get(FacingDirection dir)
    {
        switch (dir)
        {
            case FacingDirection.Left: return left;
            case FacingDirection.Right: return right;
            case FacingDirection.Up: return up;
            default: return down;
        }
    }
}

public enum FacingDirection { Down, Left, Right, Up }
