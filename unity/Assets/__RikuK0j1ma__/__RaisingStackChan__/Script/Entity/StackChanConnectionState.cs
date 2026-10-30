namespace RikuK0j1ma.RaisingStackChan.Entity
{
    /// <summary>Connection state of the StackChan link, for UI notification.</summary>
    public enum StackChanConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        /// <summary>Connection was lost or failed; waiting for the next backoff retry.</summary>
        Reconnecting = 3,
    }
}
