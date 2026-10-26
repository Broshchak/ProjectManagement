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
        public override string ToString() => "TokenPairDto { }";
    }

    public record CreateUserDto(string Login, string Password, string FullName, string RoleCode)
    {
        public override string ToString() => $"CreateUserDto {{ Login = {Login}, RoleCode = {RoleCode} }}";
    }

    public record UserDto(int Id, string Login, string FullName, string RoleCode, bool IsActive);

    public record CurrentUserDto(int UserId, string RoleCode, IReadOnlyCollection<string> Permissions);

    /// <summary>Minimal per-request identity data loaded from the DB (immediate revocation).</summary>
    public record AuthUserInfo(int Id, string RoleCode);
}
