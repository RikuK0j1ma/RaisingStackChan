using NUnit.Framework;
using RikuK0j1ma.RaisingStackChan.UseCase;

namespace RikuK0j1ma.RaisingStackChan.Tests.EditMode
{
    public class HungrySoundUseCaseTests
    {
        private sealed class FakeSound : IHungrySound
        {
            public int Count;
            public void PlayHungry() => Count++;
        }

        [Test]
        public void NotHungry_PlaysNothing()
        {
            var sound = new FakeSound();
            var useCase = new HungrySoundUseCase(sound);
            for (var i = 0; i < 100; i++) useCase.Tick(false, 0.1f);
            Assert.AreEqual(0, sound.Count);
        }

        [Test]
        public void Hungry_PlaysFirstSoundSoonThenWaitsAnInterval()
        {
            var sound = new FakeSound();
            var useCase = new HungrySoundUseCase(sound);

            useCase.Tick(true, HungrySoundUseCase.FirstDelaySeconds + 0.01f);
            Assert.AreEqual(1, sound.Count);

            // Shorter than the minimum interval: no second sound yet.
            useCase.Tick(true, HungrySoundUseCase.MinIntervalSeconds - 0.1f);
            Assert.AreEqual(1, sound.Count);

            // Longer than the maximum interval: the second one has played.
            useCase.Tick(true, HungrySoundUseCase.MaxIntervalSeconds);
            Assert.AreEqual(2, sound.Count);
        }

        [Test]
        public void ChaseStopping_RestartsTheFirstDelay()
        {
            var sound = new FakeSound();
            var useCase = new HungrySoundUseCase(sound);
            useCase.Tick(true, HungrySoundUseCase.FirstDelaySeconds + 0.01f);
            useCase.Tick(false, 0.1f);

            useCase.Tick(true, HungrySoundUseCase.FirstDelaySeconds + 0.01f);
            Assert.AreEqual(2, sound.Count);
        }
    }
}
