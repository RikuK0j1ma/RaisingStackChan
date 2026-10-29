using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.Entity;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// Picks StackChan's base face from what it is doing and sends it when it changes:
    /// food spheres out (chasing food) -> Hungry, looking at the player -> Needy, otherwise Neutral.
    /// The face after eating (Satisfied) is sent by FeedSphereUseCase for a few seconds; StackChan then comes
    /// back to the base face sent here. Sends nothing while disconnected or while the QR is being shown,
    /// and sends the current face again after a reconnect (StackChan forgets it).
    /// </summary>
    public sealed class ExpressionUseCase : IDisposable
    {
        /// <summary>Seconds to wait before sending again after a failed send.</summary>
        public const float RetrySeconds = 1f;

        private readonly IStackChanLink link;
        private readonly HeadLookUseCase look;
        private readonly SetupGuideUseCase guide;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private bool hasSent;
        private StackChanEmotion sentEmotion;
        private bool sending;
        private float sinceFail = RetrySeconds;

        public ExpressionUseCase(IStackChanLink link, HeadLookUseCase look, SetupGuideUseCase guide)
        {
            this.link = link;
            this.look = look;
            this.guide = guide;
        }

        /// <summary>The base face being shown (valid once something was sent).</summary>
        public StackChanEmotion Current => sentEmotion;

        /// <summary>The base face for a state. Pure.</summary>
        public static StackChanEmotion Decide(bool lookActive, bool lookingAtFood)
        {
            if (!lookActive) return StackChanEmotion.Neutral;
            return lookingAtFood ? StackChanEmotion.Hungry : StackChanEmotion.Needy;
        }

        /// <summary>Call every frame, after HeadLookUseCase.Tick.</summary>
        public void Tick(float deltaTime)
        {
            sinceFail += deltaTime;
            if (!link.IsConnected || guide.Flow.Phase != SetupPhase.Connected)
            {
                hasSent = false; // send again once connected and the face is visible
                return;
            }

            var wanted = Decide(look.IsActive, look.IsLookingAtFood);
            if (hasSent && wanted == sentEmotion) return;
            if (sending || sinceFail < RetrySeconds) return;
            SendAsync(wanted).Forget();
        }

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
        }

        private async UniTaskVoid SendAsync(StackChanEmotion emotion)
        {
            sending = true;
            try
            {
                await link.SendEmotionAsync(emotion, 0, cts.Token); // 0: base face, kept until the next one
                sentEmotion = emotion;
                hasSent = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                sinceFail = 0f;
                Debug.LogWarning($"[Expression] send failed: {e.Message}");
            }
            finally { sending = false; }
        }
    }
}
