using System.Text.RegularExpressions;

namespace Application.Common
{
    public static partial class LoginRules
    {
        public const int MinLength = 3;
        public const int MaxLength = 100;

        public static string Normalize(string? login) => (login ?? string.Empty).Trim().ToLowerInvariant();

        public static bool IsValid(string normalized) =>
            normalized.Length is >= MinLength and <= MaxLength && ValidPattern().IsMatch(normalized);

        [GeneratedRegex("^[a-z0-9._-]+$")]
        private static partial Regex ValidPattern();
    }
}
