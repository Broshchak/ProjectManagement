using System.ComponentModel.DataAnnotations;
using System.Text;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Security.PasswordHashing
{
    public sealed class PasswordHasherService : IPasswordHasher, IDisposable
    {
        private const int MaxStoredHashLength = 512;
        private static readonly object LegacySubject = new();

        private readonly PasswordHashingOptions _options;
        private readonly Dictionary<string, IPasswordHashAlgorithm> _algorithms;
        private readonly IPasswordHashAlgorithm _default;
        private readonly SemaphoreSlim _memoryGate;
        private readonly PasswordHasher<object> _legacy = new();
        private readonly Lazy<string> _dummyHash;
        private readonly ILogger<PasswordHasherService> _logger;

        public PasswordHasherService(
            PasswordHashingOptions options,
            IEnumerable<IPasswordHashAlgorithm> algorithms,
            ILogger<PasswordHasherService> logger)
        {
            _options = options;
            _logger = logger;
            _algorithms = algorithms.ToDictionary(a => a.Id, StringComparer.Ordinal);
            _default = _algorithms.TryGetValue(options.Algorithm, out var found)
                ? found
                : throw new InvalidOperationException($"No implementation is registered for password algorithm '{options.Algorithm}'.");

            _memoryGate = new SemaphoreSlim(options.MaxConcurrentMemoryHard);
            _dummyHash = new Lazy<string>(() => _default.Hash(Guid.NewGuid().ToString("N")));
        }

        public async Task<string> HashAsync(string password, CancellationToken ct = default)
        {
            var normalized = Normalize(password)
                ?? throw new ValidationException("The password contains invalid characters.");

            return await RunAsync(_default, () => _default.Hash(normalized), ct);
        }

        public async Task<PasswordVerifyResult> VerifyAsync(string passwordHash, string password, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(passwordHash) || passwordHash.Length > MaxStoredHashLength) return PasswordVerifyResult.Failed;

            // Old ASP.NET Identity V3 hash (base64, no leading '$'): verify on the raw password, then always migrate.
            if (passwordHash[0] != '$')
            {
                return _options.AllowLegacyIdentityV3 ? VerifyLegacy(passwordHash, password) : PasswordVerifyResult.Failed;
            }

            var normalized = Normalize(password);
            if (normalized is null || !PhcString.TryParse(passwordHash, out var phc) || phc is null) return PasswordVerifyResult.Failed;

            if (!_algorithms.TryGetValue(phc.Id, out var algorithm) || !algorithm.IsSupported(phc))
            {
                _logger.LogWarning("A stored password hash was rejected: unsupported algorithm or parameters ({Algorithm}).", phc.Id);
                return PasswordVerifyResult.Failed;
            }

            if (!await RunAsync(algorithm, () => algorithm.Verify(phc, normalized), ct)) return PasswordVerifyResult.Failed;

            var migrate = !string.Equals(phc.Id, _default.Id, StringComparison.Ordinal) || algorithm.NeedsRehash(phc);
            return migrate ? PasswordVerifyResult.SuccessRehashNeeded : PasswordVerifyResult.Success;
        }

        public async Task VerifyDummyAsync(string password, CancellationToken ct = default)
        {
            var normalized = Normalize(password) ?? string.Empty;

            await RunAsync(_default, () =>
            {
                PhcString.TryParse(_dummyHash.Value, out var phc);
                return _default.Verify(phc!, normalized);
            }, ct);
        }

        public void Dispose() => _memoryGate.Dispose();

        private PasswordVerifyResult VerifyLegacy(string passwordHash, string password)
        {
            return _legacy.VerifyHashedPassword(LegacySubject, passwordHash, password) switch
            {
                PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded => PasswordVerifyResult.SuccessRehashNeeded,
                _ => PasswordVerifyResult.Failed
            };
        }

        private async Task<T> RunAsync<T>(IPasswordHashAlgorithm algorithm, Func<T> work, CancellationToken ct)
        {
            if (!algorithm.IsMemoryHard) return work();

            if (!await _memoryGate.WaitAsync(TimeSpan.FromSeconds(_options.QueueTimeoutSeconds), ct))
            {
                throw new ServiceBusyException("The server is busy. Try again in a moment.");
            }

            try
            {
                return work();
            }
            finally
            {
                _memoryGate.Release();
            }
        }

        /// <summary>
        /// NFKC normalization (NIST SP 800-63B), so the same visible password hashes the same on every keyboard and client.
        /// Returns null for strings that are not valid Unicode (lone surrogates).
        /// </summary>
        private static string? Normalize(string password)
        {
            try
            {
                return password.Normalize(NormalizationForm.FormKC);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
