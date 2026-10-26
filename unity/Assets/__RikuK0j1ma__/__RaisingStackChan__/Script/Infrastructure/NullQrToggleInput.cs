using System;
using RikuK0j1ma.RaisingStackChan.UseCase;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>No input source (platforms without a Quest controller).</summary>
    public sealed class NullQrToggleInput : IQrToggleInput
    {
#pragma warning disable CS0067
        public event Action Pressed;
#pragma warning restore CS0067
    }
}
