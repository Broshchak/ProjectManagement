using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Infrastructure.Security.PasswordHashing
{
    public sealed record PhcHash(
        string Id,
        int? Version,
        IReadOnlyDictionary<string, string> Parameters,
        byte[] Salt,
        byte[] Hash);

    /// <summary>
    /// Strict parser/formatter for the PHC string format:<br/>
    ///   $id$[v=N$]key=value,key=value$salt$hash     (salt and hash: standard Base64 WITHOUT padding)<br/>
    /// Examples:<br/>
    ///   $argon2id$v=19$m=19456,t=2,p=1$salt$hash<br/>
    ///   $pbkdf2-sha512$i=210000,l=32$salt$hash<br/>
    /// Only the shape with parameters, salt and hash all present is accepted (that is all passwords need).
    /// </summary>
    public static partial class PhcString
    {
        private const int MaxLength = 512;

        public static bool TryParse(string? value, out PhcHash? result)
        {
            result = null;

            if (string.IsNullOrEmpty(value) || value.Length > MaxLength || value[0] != '$') return false;

            var segments = value[1..].Split('$');
            if (segments.Length is not (4 or 5)) return false;

            if (!IdPattern().IsMatch(segments[0])) return false;

            int? version = null;
            var next = 1;
            if (segments.Length == 5)
            {
                if (!VersionPattern().IsMatch(segments[1])) return false;
                version = int.Parse(segments[1].AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture);
                next = 2;
            }

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in segments[next].Split(','))
            {
                var equals = pair.IndexOf('=');
                if (equals <= 0) return false;

                var key = pair[..equals];
                var val = pair[(equals + 1)..];
                if (!KeyPattern().IsMatch(key) || !ValuePattern().IsMatch(val) || !parameters.TryAdd(key, val)) return false;
            }

            if (!TryDecode(segments[next + 1], out var salt) || !TryDecode(segments[next + 2], out var hash)) return false;

            result = new PhcHash(segments[0], version, parameters, salt, hash);
            return true;
        }

        public static string Format(
            string id,
            int? version,
            IEnumerable<KeyValuePair<string, string>> parameters,
            byte[] salt,
            byte[] hash)
        {
            var builder = new StringBuilder("$").Append(id);

            if (version is not null)
            {
                builder.Append("$v=").Append(version.Value.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append('$').Append(string.Join(',', parameters.Select(p => $"{p.Key}={p.Value}")));
            builder.Append('$').Append(Encode(salt));
            builder.Append('$').Append(Encode(hash));

            return builder.ToString();
        }

        private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

        private static bool TryDecode(string text, out byte[] bytes)
        {
            bytes = [];

            if (text.Length is 0 or > 700 || text.Length % 4 == 1 || !Base64Pattern().IsMatch(text)) return false;

            try
            {
                bytes = Convert.FromBase64String(text + new string('=', (4 - text.Length % 4) % 4));
                return bytes.Length > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        [GeneratedRegex("^[a-z0-9-]{1,32}$")] private static partial Regex IdPattern();
        [GeneratedRegex("^v=[0-9]{1,4}$")] private static partial Regex VersionPattern();
        [GeneratedRegex("^[a-z0-9-]{1,32}$")] private static partial Regex KeyPattern();
        [GeneratedRegex("^[a-zA-Z0-9/+.-]{1,64}$")] private static partial Regex ValuePattern();
        [GeneratedRegex("^[A-Za-z0-9+/]+$")] private static partial Regex Base64Pattern();
    }
}
