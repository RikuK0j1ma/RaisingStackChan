using System;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>"Show the QR again / back to the face" input (Y button of the left controller on Quest).</summary>
    public interface IQrToggleInput
    {
        /// <summary>Raised on the Unity main thread each time the toggle is pressed.</summary>
        event Action Pressed;
    }
}
