namespace Application.Dtos.Auth
{
    // ToString() is overridden on records holding secrets to prevent them from accidentally
    // leaking, for example, through logs.
    public record LoginDto(string Login, string Password)
    {
        public override string ToString() => $"LoginDto {{ Login = {Login} }}";
    }

    public record RefreshTokenDto(string RefreshToken)
    {
        public override string ToString() => "RefreshTokenDto { }";
    }

    public record TokenPairDto(
        string AccessToken,
        DateTime AccessTokenExpiresAt,
        string RefreshToken,
        DateTime RefreshTokenExpiresAt)
    {
        public string TokenType => "Bearer";
        public override string ToString() => "TokenPairDto { }";
    }

    public record CurrentUserDto(int UserId, string RoleCode, IReadOnlyCollection<string> Permissions, string Login, string FullName)
    {
        public int Id => UserId;
        public string Role => RoleCode;
    }

    /// <summary>Minimal per-request identity data loaded from the DB (immediate revocation).</summary>
    public record AuthUserInfo(int Id, string RoleCode);
}
