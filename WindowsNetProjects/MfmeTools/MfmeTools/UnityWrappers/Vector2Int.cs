namespace MfmeTools.UnityWrappers
{
    public struct Vector2Int
    {
        public int x;
        public int y;

        public static Vector2Int zero
        { 
            get
            {
                return new Vector2Int(0, 0);
            }
        }

        public Vector2Int(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }
}
