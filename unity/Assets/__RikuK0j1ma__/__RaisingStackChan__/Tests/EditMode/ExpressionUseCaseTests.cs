using NUnit.Framework;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.Infrastructure;
using RikuK0j1ma.RaisingStackChan.UseCase;

namespace RikuK0j1ma.RaisingStackChan.Tests.EditMode
{
    public sealed class ExpressionUseCaseTests
    {
        [Test]
        public void Decide_NotLooking_IsNeutral()
        {
            Assert.AreEqual(StackChanEmotion.Neutral, ExpressionUseCase.Decide(false, false));
            Assert.AreEqual(StackChanEmotion.Neutral, ExpressionUseCase.Decide(false, true));
        }

        [Test]
        public void Decide_LookingAtFood_IsHungry()
        {
            Assert.AreEqual(StackChanEmotion.Hungry, ExpressionUseCase.Decide(true, true));
        }

        [Test]
        public void Decide_LookingAtThePlayer_IsNeedy()
        {
            Assert.AreEqual(StackChanEmotion.Needy, ExpressionUseCase.Decide(true, false));
        }

        [Test]
        public void Messages_NewEmotions_UseLowerCaseNames()
        {
            Assert.AreEqual("{\"type\":\"emotion\",\"value\":\"hungry\",\"durationMs\":0}",
                StackChanMessages.Emotion(StackChanEmotion.Hungry, 0));
            Assert.AreEqual("{\"type\":\"emotion\",\"value\":\"needy\",\"durationMs\":0}",
                StackChanMessages.Emotion(StackChanEmotion.Needy, 0));
            Assert.AreEqual("{\"type\":\"emotion\",\"value\":\"satisfied\",\"durationMs\":3000}",
                StackChanMessages.Emotion(StackChanEmotion.Satisfied, 3000));
        }
    }
}
