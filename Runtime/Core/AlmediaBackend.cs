using System;

namespace AlmediaSDK
{
    /// <summary>
    /// The backend a resolved configuration talks to: production, or the origins the host set
    /// in <see cref="AlmediaConfig.BackendUrl"/> and <see cref="AlmediaConfig.AttBackendUrl"/>.
    /// </summary>
    internal sealed class AlmediaBackend : IEquatable<AlmediaBackend>
    {
        public static readonly AlmediaBackend Production = new AlmediaBackend(null, null);

        /// <summary>The main origin. Null for production.</summary>
        public string Url { get; }

        /// <summary>The origin that receives the IDFA on iOS. Null when not set.</summary>
        public string AttUrl { get; }

        public bool IsProduction => Url == null;

        private AlmediaBackend(string url, string attUrl)
        {
            Url = url;
            AttUrl = attUrl;
        }

        /// <summary>
        /// Null or empty for both selects production. An ATT URL alone keeps an empty main URL,
        /// which the natives reject at initialize.
        /// </summary>
        public static AlmediaBackend From(string url, string attUrl)
        {
            if (string.IsNullOrEmpty(url) && string.IsNullOrEmpty(attUrl))
                return Production;

            return new AlmediaBackend(url ?? "", string.IsNullOrEmpty(attUrl) ? null : attUrl);
        }

        internal static readonly string[] ProductionHosts =
        {
            "fc-link-api.freecash.ai",
            "att-fc-link-api.freecash.ai"
        };

        private static readonly string[] LoopbackHosts = { "localhost", "127.0.0.1" };

        /// <summary>The reason <paramref name="url"/> is not a valid backend origin, or null when it is.</summary>
        public static string InvalidityReason(string url)
        {
            if (string.IsNullOrEmpty(url)) return "is empty";

            foreach (var c in url)
                if (char.IsWhiteSpace(c)) return "contains whitespace";

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
                return "is not a URL with a host";

            if (!string.IsNullOrEmpty(uri.UserInfo)) return "has user info";
            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return "has a query or fragment";
            if (uri.Port < 1) return "has a port out of range";

            var path = RawPath(url);
            if (path != "" && path != "/") return "has a path";

            var host = uri.Host.ToLowerInvariant();
            var scheme = uri.Scheme.ToLowerInvariant();
            if (scheme != "https" && !(scheme == "http" && IsLocalHost(RawHost(url))))
                return "must use https (http only for localhost, 127.0.0.1 and private IPv4 addresses)";

            if (Array.IndexOf(ProductionHosts, host) >= 0)
                return "is a production host; leave BackendUrl empty for production";

            return null;
        }

        private static bool IsLocalHost(string host)
            => Array.IndexOf(LoopbackHosts, host) >= 0 || IsPrivateIPv4(host);

        private static bool IsPrivateIPv4(string host)
        {
            var parts = host.Split('.');
            if (parts.Length != 4) return false;

            var octets = new int[4];
            for (var i = 0; i < 4; i++)
            {
                var part = parts[i];
                if (part.Length < 1 || part.Length > 3) return false;
                if (part.Length > 1 && part[0] == '0') return false;
                foreach (var c in part)
                    if (c < '0' || c > '9') return false;
                octets[i] = int.Parse(part);
                if (octets[i] > 255) return false;
            }

            return octets[0] == 10
                   || (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31)
                   || (octets[0] == 192 && octets[1] == 168);
        }

        private static readonly char[] PathDelimiters = { '/', '?', '#' };

        /// <summary>Everything after the authority, unnormalized: "" or "/" for an origin.</summary>
        private static string RawPath(string url)
        {
            var authority = url.IndexOf("://", StringComparison.Ordinal);
            if (authority < 0) return "";

            var start = url.IndexOfAny(PathDelimiters, authority + 3);
            return start < 0 ? "" : url.Substring(start);
        }

        private static string RawHost(string url)
        {
            var start = url.IndexOf("://", StringComparison.Ordinal) + 3;
            var end = url.IndexOfAny(PathDelimiters, start);
            var authority = end < 0 ? url.Substring(start) : url.Substring(start, end - start);
            var port = authority.LastIndexOf(':');
            return (port < 0 ? authority : authority.Substring(0, port)).ToLowerInvariant();
        }

        public bool Equals(AlmediaBackend other)
            => other != null
               && string.Equals(Url, other.Url, StringComparison.Ordinal)
               && string.Equals(AttUrl, other.AttUrl, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as AlmediaBackend);

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Url ?? "") * 397)
                       ^ StringComparer.Ordinal.GetHashCode(AttUrl ?? "");
            }
        }

        public override string ToString()
        {
            if (IsProduction) return "Production";
            return AttUrl == null ? $"Custom({Url})" : $"Custom({Url}, ATT {AttUrl})";
        }
    }
}
