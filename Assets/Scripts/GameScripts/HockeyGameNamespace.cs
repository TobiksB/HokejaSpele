namespace HockeyGame.Game
{
    // FIXED: Create a proper class that references the global Puck
    public class Puck : global::Puck
    {
        // This class inherits from the global Puck class
        // Now HockeyGame.Game.Puck will resolve properly
    }
    
    // FIXED: Also create other game-related classes if needed
    public static class GameUtils
    {
        public static global::Puck GetGlobalPuck()
        {
            return UnityEngine.Object.FindFirstObjectByType<global::Puck>();
        }
    }
}
