#pragma warning disable 0660, 0661

[System.Serializable]
public struct Coordinates {
    public static readonly Coordinates Up    = new Coordinates( 0,  1);
    public static readonly Coordinates Down  = new Coordinates( 0, -1);
    public static readonly Coordinates Left  = new Coordinates(-1,  0);
    public static readonly Coordinates Right = new Coordinates( 1,  0);
    public static readonly Coordinates Zero  = new Coordinates( 0,  0);
    public static readonly Coordinates[] FacingDirection   = new Coordinates[] { Right, Up, Left, Down }; // Set to match facing indeces 0-3
    public static readonly Coordinates[] CompassDirection  = new Coordinates[] { Up, Down, Left, Right };
    public static readonly Coordinates[] DiagonalDirection = new Coordinates[] { Up + Left, Up + Right, Down + Left, Down + Right };
    public static readonly Coordinates[] AllDirection      = new Coordinates[] { Up, Down, Left, Right, Up + Left, Up + Right, Down + Left, Down + Right };

    public int x;
    public int y;
    
    public Coordinates(int x, int y) {
        this.x = x;
        this.y = y;
    }

    public static int GetFacing(Coordinates direction) {
        for (int i = 0; i < FacingDirection.Length; i++) {
            if (direction == FacingDirection[i])
                return i;
        }
        return -1;
    }
    public override string ToString() => "(" + x + ", " + y + ")";

    public static Coordinates operator +(Coordinates a, Coordinates b) => new Coordinates( a.x + b.x,  a.y + b.y);
    public static Coordinates operator +(Coordinates a, int b)         => new Coordinates( a.x + b  ,  a.y + b  );
    public static Coordinates operator -(Coordinates a, Coordinates b) => new Coordinates( a.x - b.x,  a.y - b.y);
    public static Coordinates operator *(Coordinates a, int b)         => new Coordinates( a.x * b  ,  a.y * b  );
    public static Coordinates operator /(Coordinates a, int b)         => new Coordinates( a.x / b  ,  a.y / b  );
    public static Coordinates operator -(Coordinates a)                => new Coordinates(-a.x      , -a.y      );

    public static bool operator ==(Coordinates a, Coordinates b) => a.x == b.x && a.y == b.y;
    public static bool operator !=(Coordinates a, Coordinates b) => a.x != b.x || a.y != b.y;
}
