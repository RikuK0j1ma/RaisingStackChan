namespace RikuK0j1ma.RaisingStackChan.Entity
{
    /// <summary>Emotion sent to StackChan. Wire value is the lower-case name.</summary>
    public enum StackChanEmotion
    {
        Neutral = 0,
        Happy = 1,

        /// <summary>Chasing food: excited face with drool.</summary>
        Hungry = 2,

        /// <summary>Chasing the player: begging face.</summary>
        Needy = 3,

        /// <summary>Just ate: happy face with hearts.</summary>
        Satisfied = 4,
    }
}