using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace Infrastructure.Security.PasswordHashing
{
    public sealed class Argon2Algorithm(string id, int argon2Type, PasswordHashingOptions options) : IPasswordHashAlgorithm
    {
        // RFC 9106 numeric ids
        // Types: 0 = Argon2d, 1 = Argon2i, 2 = Argon2id.  Versions: 0x10 (1.0), 0x13 (1.3 = 19).
        public const int TypeArgon2i = 1;
        public const int TypeArgon2id = 2;

        private const int CurrentVersion = 0x13;
        private const int LegacyVersion = 0x10;

        public string Id => id;

        public bool IsMemoryHard => true;

        public string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(options.SaltBytes);
            var hash = Derive(password, salt, CurrentVersion, options.Argon2MemoryKiB, options.Argon2Iterations, options.Argon2Parallelism, options.HashBytes);

            return PhcString.Format(id, CurrentVersion,
            [
                new("m", options.Argon2MemoryKiB.ToString(CultureInfo.InvariantCulture)),
                new("t", options.Argon2Iterations.ToString(CultureInfo.InvariantCulture)),
                new("p", options.Argon2Parallelism.ToString(CultureInfo.InvariantCulture))
            ], salt, hash);
        }

        public bool IsSupported(PhcHash hash) => TryRead(hash, out _, out _, out _, out _);

        public bool Verify(PhcHash hash, string password)
        {
            if (!TryRead(hash, out var version, out var memory, out var iterations, out var parallelism)) return false;

            var actual = Derive(password, hash.Salt, version, memory, iterations, parallelism, hash.Hash.Length);
            return CryptographicOperations.FixedTimeEquals(actual, hash.Hash);
        }

        public bool NeedsRehash(PhcHash hash) =>
            !TryRead(hash, out var version, out var memory, out var iterations, out var parallelism)
            || version < CurrentVersion
            || memory < options.Argon2MemoryKiB
            || iterations < options.Argon2Iterations
            || parallelism != options.Argon2Parallelism
            || hash.Salt.Length < options.SaltBytes
            || hash.Hash.Length < options.HashBytes;

        private byte[] Derive(string password, byte[] salt, int version, int memoryKiB, int iterations, int parallelism, int length)
        {
            var parameters = new Argon2Parameters.Builder(argon2Type)
                .WithVersion(version)
                .WithIterations(iterations)
                .WithMemoryAsKB(memoryKiB)
                .WithParallelism(parallelism)
                .WithSalt(salt)
                .Build();

            var generator = new Argon2BytesGenerator();
            generator.Init(parameters);

            var passwordBytes = Encoding.UTF8.GetBytes(password);
            try
            {
                var output = new byte[length];
                generator.GenerateBytes(passwordBytes, output);
                return output;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passwordBytes);
            }
        }

        private bool TryRead(PhcHash hash, out int version, out int memory, out int iterations, out int parallelism)
        {
            version = memory = iterations = parallelism = 0;

            // Per the Argon2 PHC spec, a missing "v" means version 1.0 (0x10).
            version = hash.Version ?? LegacyVersion;
            if (version is not (LegacyVersion or CurrentVersion)) return false;

            if (hash.Parameters.Count != 3 || hash.Parameters.Keys.Any(key => key is not ("m" or "t" or "p"))) return false;

            if (!TryInt(hash, "m", out memory) || !TryInt(hash, "t", out iterations) || !TryInt(hash, "p", out parallelism)) return false;

            return parallelism is >= 1 and <= 16
                && iterations is >= 1 and <= 10
                && memory >= 8 * parallelism
                && memory <= options.Argon2MaxMemoryKiB
                && hash.Salt.Length is >= 8 and <= 64
                && hash.Hash.Length is >= 16 and <= 64;
        }

        private static bool TryInt(PhcHash hash, string key, out int value)
        {
            value = 0;
            return hash.Parameters.TryGetValue(key, out var raw)
                && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }
    }
}
