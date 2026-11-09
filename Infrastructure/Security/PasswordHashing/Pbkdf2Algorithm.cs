using System.Globalization;
using System.Security.Cryptography;

namespace Infrastructure.Security.PasswordHashing
{
    /// <summary>
    /// PBKDF2 with HMAC-SHA256 or HMAC-SHA512. PHC form: $pbkdf2-sha512$i=210000,l=32$salt$hash
    /// (the RustCrypto/PHC convention: i = iterations, l = output length in bytes).
    /// </summary>
    public sealed class Pbkdf2Algorithm(string id, HashAlgorithmName hashName, int iterations, PasswordHashingOptions options)
        : IPasswordHashAlgorithm
    {
        private const int MaxIterations = 5_000_000;

        public string Id => id;

        public bool IsMemoryHard => false;

        public string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(options.SaltBytes);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, hashName, options.HashBytes);

            return PhcString.Format(id, null,
            [
                new("i", iterations.ToString(CultureInfo.InvariantCulture)),
                new("l", options.HashBytes.ToString(CultureInfo.InvariantCulture))
            ], salt, hash);
        }

        public bool IsSupported(PhcHash hash) => TryGetIterations(hash, out _);

        public bool Verify(PhcHash hash, string password)
        {
            if (!TryGetIterations(hash, out var storedIterations)) return false;

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, hash.Salt, storedIterations, hashName, hash.Hash.Length);
            return CryptographicOperations.FixedTimeEquals(actual, hash.Hash);
        }

        public bool NeedsRehash(PhcHash hash) =>
            !TryGetIterations(hash, out var stored)
            || stored < iterations
            || hash.Salt.Length < options.SaltBytes
            || hash.Hash.Length < options.HashBytes;

        private static bool TryGetIterations(PhcHash hash, out int iterationCount)
        {
            iterationCount = 0;

            if (hash.Version is not null) return false;
            if (hash.Parameters.Keys.Any(key => key is not ("i" or "l"))) return false;

            if (!hash.Parameters.TryGetValue("i", out var raw)
                || !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out iterationCount)
                || iterationCount is < 1 or > MaxIterations)
            {
                return false;
            }

            if (hash.Parameters.TryGetValue("l", out var rawLength)
                && (!int.TryParse(rawLength, NumberStyles.None, CultureInfo.InvariantCulture, out var length) || length != hash.Hash.Length))
            {
                return false;
            }

            return hash.Salt.Length is >= 8 and <= 64 && hash.Hash.Length is >= 16 and <= 64;
        }
    }
}
