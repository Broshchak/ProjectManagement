using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Security.PasswordHashing
{
    public static class PasswordHashingOptionsFactory
    {
        private const string Section = "Auth:PasswordHashing:";

        public static PasswordHashingOptions FromConfiguration(IConfiguration configuration)
        {
            var defaults = new PasswordHashingOptions();

            var options = new PasswordHashingOptions
            {
                Algorithm = (configuration[Section + "Algorithm"] ?? defaults.Algorithm).Trim().ToLowerInvariant(),
                Pbkdf2Sha256Iterations = Int(configuration, "Pbkdf2Sha256Iterations", defaults.Pbkdf2Sha256Iterations),
                Pbkdf2Sha512Iterations = Int(configuration, "Pbkdf2Sha512Iterations", defaults.Pbkdf2Sha512Iterations),
                Argon2MemoryKiB = Int(configuration, "Argon2MemoryKiB", defaults.Argon2MemoryKiB),
                Argon2Iterations = Int(configuration, "Argon2Iterations", defaults.Argon2Iterations),
                Argon2Parallelism = Int(configuration, "Argon2Parallelism", defaults.Argon2Parallelism),
                Argon2MaxMemoryKiB = Int(configuration, "Argon2MaxMemoryKiB", defaults.Argon2MaxMemoryKiB),
                MaxConcurrentMemoryHard = Int(configuration, "MaxConcurrentMemoryHard", defaults.MaxConcurrentMemoryHard),
                QueueTimeoutSeconds = Int(configuration, "QueueTimeoutSeconds", defaults.QueueTimeoutSeconds),
                AllowLegacyIdentityV3 = Bool(configuration, "AllowLegacyIdentityV3", defaults.AllowLegacyIdentityV3)
            };

            Validate(options);
            return options;
        }

        private static void Validate(PasswordHashingOptions o)
        {
            if (!PasswordHashingOptions.SupportedAlgorithms.Contains(o.Algorithm))
            {
                throw new InvalidOperationException(
                    $"Auth:PasswordHashing:Algorithm '{o.Algorithm}' is not supported. Use one of: {string.Join(", ", PasswordHashingOptions.SupportedAlgorithms)}.");
            }

            Require(o.Pbkdf2Sha256Iterations is >= 600_000 and <= 5_000_000, "Pbkdf2Sha256Iterations must be 600000-5000000 (OWASP minimum).");
            Require(o.Pbkdf2Sha512Iterations is >= 210_000 and <= 5_000_000, "Pbkdf2Sha512Iterations must be 210000-5000000 (OWASP minimum).");
            Require(o.Argon2Parallelism is >= 1 and <= 16, "Argon2Parallelism must be 1-16.");
            Require(o.Argon2Iterations is >= 1 and <= 10, "Argon2Iterations must be 1-10.");
            Require(o.Argon2MemoryKiB >= 7_168 && o.Argon2MemoryKiB >= 8 * o.Argon2Parallelism, "Argon2MemoryKiB must be at least 7168 (7 MiB, OWASP minimum) and 8 x parallelism.");
            Require(o.Argon2MaxMemoryKiB >= o.Argon2MemoryKiB && o.Argon2MaxMemoryKiB <= 262_144, "Argon2MaxMemoryKiB must be between Argon2MemoryKiB and 262144.");
            Require(o.MaxConcurrentMemoryHard is >= 1 and <= 8, "MaxConcurrentMemoryHard must be 1-8.");
            Require(o.QueueTimeoutSeconds is >= 1 and <= 60, "QueueTimeoutSeconds must be 1-60.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Auth:PasswordHashing: " + message);
        }

        private static int Int(IConfiguration configuration, string key, int fallback)
        {
            var raw = configuration[Section + key];
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new InvalidOperationException($"Auth:PasswordHashing:{key} must be a whole number.");
        }

        private static bool Bool(IConfiguration configuration, string key, bool fallback)
        {
            var raw = configuration[Section + key];
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            return bool.TryParse(raw, out var value)
                ? value
                : throw new InvalidOperationException($"Auth:PasswordHashing:{key} must be true or false.");
        }
    }
}
