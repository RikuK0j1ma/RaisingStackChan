using System;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>Exponential backoff delay calculator. Pure logic (no Unity API).</summary>
    public sealed class ReconnectBackoff
    {
        private readonly double _initialSeconds;
        private readonly double _maxSeconds;
        private readonly double _factor;
        private int _attempt;

        public ReconnectBackoff(double initialSeconds = 1.0, double maxSeconds = 30.0, double factor = 2.0)
        {
            if (initialSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(initialSeconds));
            if (maxSeconds < initialSeconds) throw new ArgumentOutOfRangeException(nameof(maxSeconds));
            if (factor < 1.0) throw new ArgumentOutOfRangeException(nameof(factor));
            _initialSeconds = initialSeconds;
            _maxSeconds = maxSeconds;
            _factor = factor;
        }

        public int Attempt => _attempt;

        /// <summary>Delay for the given 0-based attempt, capped at max.</summary>
        public TimeSpan DelayFor(int attempt)
        {
            if (attempt < 0) attempt = 0;
            var seconds = _initialSeconds * Math.Pow(_factor, attempt);
            if (double.IsInfinity(seconds) || double.IsNaN(seconds) || seconds > _maxSeconds) seconds = _maxSeconds;
            return TimeSpan.FromSeconds(seconds);
        }

        /// <summary>Returns the next delay and advances the attempt counter.</summary>
        public TimeSpan Next()
        {
            var d = DelayFor(_attempt);
            if (_attempt < int.MaxValue) _attempt++;
            return d;
        }

        public void Reset() => _attempt = 0;
    }
}
