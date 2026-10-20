using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>Persists the face anchor UUID in PlayerPrefs.</summary>
    public sealed class PlayerPrefsFaceAnchorIdStore : IFaceAnchorIdStore
    {
        private const string Key = "RikuK0j1ma.RaisingStackChan.FaceAnchorUuid";

        public bool TryLoad(out string uuid)
        {
            uuid = PlayerPrefs.GetString(Key, string.Empty);
            return uuid.Length > 0;
        }

        public void Save(string uuid)
        {
            PlayerPrefs.SetString(Key, uuid);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
