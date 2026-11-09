namespace Infrastructure.Security.PasswordHashing
{
    public sealed class PasswordHashingOptions
    {
        public static readonly string[] SupportedAlgorithms = ["pbkdf2-sha256", "pbkdf2-sha512", "argon2id", "argon2i"];

        public string Algorithm { get; init; } = "pbkdf2-sha512";

        // OWASP minimums: PBKDF2-HMAC-SHA256 600,000 / PBKDF2-HMAC-SHA512 210,000.
        public int Pbkdf2Sha256Iterations { get; init; } = 600_000;
        public int Pbkdf2Sha512Iterations { get; init; } = 210_000;

        // OWASP Argon2id option: m=19 MiB, t=2, p=1.
        public int Argon2MemoryKiB { get; init; } = 19_456;
        public int Argon2Iterations { get; init; } = 2;
        public int Argon2Parallelism { get; init; } = 1;
        public int Argon2MaxMemoryKiB { get; init; } = 65_536;
        public int MaxConcurrentMemoryHard { get; init; } = 1;
        public int QueueTimeoutSeconds { get; init; } = 5;

        public int SaltBytes { get; init; } = 16;
        public int HashBytes { get; init; } = 32;

        public bool AllowLegacyIdentityV3 { get; init; } = true;
    }
}
