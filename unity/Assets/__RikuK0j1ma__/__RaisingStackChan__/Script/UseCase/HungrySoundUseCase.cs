using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// Plays the hungry sound at random intervals while StackChan is chasing food. The first sound comes
    /// shortly after it starts chasing; the timer restarts every time the chase stops.
    /// </summary>
    public sealed class HungrySoundUseCase
    {
        public const float FirstDelaySeconds = 0.5f;
        public const float MinIntervalSeconds = 2.5f;
        public const float MaxIntervalSeconds = 6f;

        private readonly IHungrySound sound;
        private float remaining = FirstDelaySeconds;

        public HungrySoundUseCase(IHungrySound sound)
        {
            this.sound = sound;
        }

        /// <summary>Call every frame. hungry: StackChan is looking at food right now.</summary>
        public void Tick(bool hungry, float deltaTime)
        {
            if (!hungry)
            {
                remaining = FirstDelaySeconds;
                return;
            }

            remaining -= deltaTime;
            if (remaining > 0f) return;

            sound.PlayHungry();
            remaining = Random.Range(MinIntervalSeconds, MaxIntervalSeconds);
        }
    }
}
