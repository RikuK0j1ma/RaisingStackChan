using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>Persists the last successfully used endpoint (including token) in PlayerPrefs.</summary>
    public sealed class PlayerPrefsEndpointStore
    {
        private const string Key = "RikuK0j1ma.RaisingStackChan.LastEndpoint";

        public bool TryLoad(out StackChanEndpoint endpoint)
        {
            endpoint = default;
            var s = PlayerPrefs.GetString(Key, string.Empty);
            return s.Length > 0 && StackChanEndpoint.TryParse(s, out endpoint);
        }

        public void Save(StackChanEndpoint endpoint)
        {
            PlayerPrefs.SetString(Key, endpoint.Serialize());
            PlayerPrefs.Save();
        }
    }
}
