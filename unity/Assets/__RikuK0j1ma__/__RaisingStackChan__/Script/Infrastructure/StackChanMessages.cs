using Newtonsoft.Json.Linq;
using RikuK0j1ma.RaisingStackChan.Entity;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>JSON text messages of the MOGUMOGU protocol (docs/design.md). Pure logic.</summary>
    public static class StackChanMessages
    {
        public static string Emotion(StackChanEmotion emotion, int durationMs)
        {
            var o = new JObject
            {
                ["type"] = "emotion",
                ["value"] = emotion.ToString().ToLowerInvariant(),
                ["durationMs"] = durationMs,
            };
            return o.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>Neck direction in degrees (yaw + right, pitch + up). Rounded to 0.1 to keep the message small.</summary>
        public static string Look(float yawDeg, float pitchDeg)
        {
            var o = new JObject
            {
                ["type"] = "look",
                ["yaw"] = System.Math.Round(yawDeg, 1),
                ["pitch"] = System.Math.Round(pitchDeg, 1),
            };
            return o.ToString(Newtonsoft.Json.Formatting.None);
        }

        public static string ShowQr()
        {
            return new JObject { ["type"] = "showQr" }.ToString(Newtonsoft.Json.Formatting.None);
        }

        public static string HideQr()
        {
            return new JObject { ["type"] = "hideQr" }.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>Returns the "type" of an incoming message, or null if it is not a JSON object with one.</summary>
        public static string TryGetType(string json, out JObject obj)
        {
            obj = null;
            try
            {
                obj = JObject.Parse(json);
                return (string)obj["type"];
            }
            catch (System.Exception)
            {
                return null;
            }
        }
    }
}
