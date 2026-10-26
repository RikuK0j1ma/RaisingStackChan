using System;
using RikuK0j1ma.RaisingStackChan.UseCase;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Platform.Meta
{
    /// <summary>Y button (OVRInput.Button.Four on the left controller) as the QR toggle. Polled every frame.</summary>
    public sealed class OvrQrToggleInput : IQrToggleInput, ITickable
    {
        public event Action Pressed;

        public void Tick()
        {
            if (OVRInput.GetDown(OVRInput.Button.Four, OVRInput.Controller.LTouch)) Pressed?.Invoke();
        }
    }
}
