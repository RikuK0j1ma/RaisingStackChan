using System;
using Newtonsoft.Json.Linq;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>Where the StackChan WebSocket server is. Pure logic (no Unity API).</summary>
    public readonly struct StackChanEndpoint : IEquatable<StackChanEndpoint>
    {
        /// <summary>mDNS host name advertised by MR LINK.</summary>
        public const string MdnsHost = "stackchan.local";

        /// <summary>Fallback port when none is known. Must match the firmware (#3); unverified.</summary>
        public const int DefaultPort = 8080;

        public string Host { get; }
        public int Port { get; }
        public string Token { get; }

        public StackChanEndpoint(string host, int port, string token)
        {
            Host = host;
            Port = port;
            Token = token ?? string.Empty;
        }

        public bool IsMdns => string.Equals(Host, MdnsHost, StringComparison.OrdinalIgnoreCase);

        public Uri ToUri()
        {
            var query = Token.Length > 0 ? "/?token=" + Uri.EscapeDataString(Token) : "/";
            return new Uri("ws://" + Host + ":" + Port + query);
        }

        /// <summary>Same port and token, but addressed by the mDNS host name.</summary>
        public StackChanEndpoint WithMdnsHost() => new StackChanEndpoint(MdnsHost, Port, Token);

        /// <summary>
        /// Parses a QR payload. Accepts JSON {"ip":"..","port":8080,"token":".."} (or "host"),
        /// or a URL ws://host:port/?token=...
        /// </summary>
        public static bool TryParse(string payload, out StackChanEndpoint endpoint)
        {
            endpoint = default;
            if (string.IsNullOrWhiteSpace(payload)) return false;
            payload = payload.Trim();

            if (payload[0] == '{')
            {
                try
                {
                    var o = JObject.Parse(payload);
                    var host = (string)(o["ip"] ?? o["host"]);
                    if (string.IsNullOrWhiteSpace(host)) return false;
                    var port = o["port"] != null ? (int)o["port"] : DefaultPort;
                    if (!IsValidPort(port)) return false;
                    endpoint = new StackChanEndpoint(host.Trim(), port, (string)o["token"]);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            if (Uri.TryCreate(payload, UriKind.Absolute, out var uri)
                && (uri.Scheme == "ws" || uri.Scheme == "http") && !string.IsNullOrEmpty(uri.Host))
            {
                var port = uri.IsDefaultPort ? DefaultPort : uri.Port;
                if (!IsValidPort(port)) return false;
                endpoint = new StackChanEndpoint(uri.Host, port, ParseToken(uri.Query));
                return true;
            }

            return false;
        }

        /// <summary>Compact JSON for persistence. Read back with <see cref="TryParse"/>.</summary>
        public string Serialize()
        {
            var o = new JObject { ["host"] = Host, ["port"] = Port, ["token"] = Token };
            return o.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static bool IsValidPort(int port) => port > 0 && port <= 65535;

        private static string ParseToken(string query)
        {
            if (string.IsNullOrEmpty(query)) return string.Empty;
            foreach (var pair in query.TrimStart('?').Split('&'))
            {
                var kv = pair.Split(new[] { '=' }, 2);
                if (kv.Length == 2 && kv[0] == "token") return Uri.UnescapeDataString(kv[1]);
            }
            return string.Empty;
        }

        public bool Equals(StackChanEndpoint other) =>
            string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase) && Port == other.Port && Token == other.Token;

        public override bool Equals(object obj) => obj is StackChanEndpoint e && Equals(e);

        public override int GetHashCode() =>
            ((Host ?? string.Empty).ToLowerInvariant().GetHashCode() * 31 + Port) * 31 + (Token ?? string.Empty).GetHashCode();
    }
}
