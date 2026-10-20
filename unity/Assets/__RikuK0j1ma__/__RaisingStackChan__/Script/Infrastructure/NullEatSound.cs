using RikuK0j1ma.RaisingStackChan.UseCase;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>Silent eat sound.</summary>
    public sealed class NullEatSound : IEatSound
    {
        public void PlayEat() { }
    }
}
