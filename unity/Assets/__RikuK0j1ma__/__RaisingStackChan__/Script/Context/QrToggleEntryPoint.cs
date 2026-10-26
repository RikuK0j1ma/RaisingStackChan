using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.UseCase;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>Routes the Y button (IQrToggleInput) to RecalibrationService: face -> QR, QR -> face.</summary>
    public sealed class QrToggleEntryPoint : IStartable, IDisposable
    {
        private readonly IQrToggleInput input;
        private readonly RecalibrationService recalibration;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public QrToggleEntryPoint(IQrToggleInput input, RecalibrationService recalibration)
        {
            this.input = input;
            this.recalibration = recalibration;
        }

        public void Start() => input.Pressed += OnPressed;

        private void OnPressed() => recalibration.ToggleQrAsync(cts.Token).Forget();

        public void Dispose()
        {
            input.Pressed -= OnPressed;
            cts.Cancel();
            cts.Dispose();
        }
    }
}
