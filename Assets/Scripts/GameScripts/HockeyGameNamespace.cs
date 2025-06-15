namespace HockeyGame.Game
{
    public class Puck : global::Puck
    {
     
    }
    
    public static class GameUtils
    {
        public static global::Puck GetGlobalPuck()
        {
            return UnityEngine.Object.FindFirstObjectByType<global::Puck>();
        }
    }
}
