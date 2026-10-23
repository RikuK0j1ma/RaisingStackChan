namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// Something that can describe its current state as text for the in-headset diagnostics panel
    /// (e.g. the QR pose provider). Implementations are optional: the panel shows what it can find.
    /// </summary>
    public interface IDiagnosticsSource
    {
        /// <summary>Multi-line status text. Called on the main thread about twice a second.</summary>
        string DescribeStatus();
    }
}
