using RikuK0j1ma.RaisingStackChan.UseCase;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>Shows no QR guidance. Replace with a Presentation implementation.</summary>
    public sealed class NullCalibrationGuide : ICalibrationGuide
    {
        public void ShowQrGuide() { }
        public void HideQrGuide() { }
    }
}
