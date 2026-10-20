using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Controller
{
    /// <summary>Snapshot of one hand (or controller) in world space.</summary>
    public readonly struct HandInputState
    {
        public readonly Vector3 Position;
        public readonly bool Grab;
        public readonly bool Spawn;

        public HandInputState(Vector3 position, bool grab, bool spawn)
        {
            Position = position;
            Grab = grab;
            Spawn = spawn;
        }
    }

    /// <summary>
    /// Input abstraction shared by controllers and hand tracking. One instance per hand.
    /// Meta-specific implementations (e.g. OVRHand pinch) live behind this interface.
    /// </summary>
    public interface IHandInputSource
    {
        /// <summary>Returns false while the hand / controller is not tracked.</summary>
        bool TryGetState(out HandInputState state);
    }
}
