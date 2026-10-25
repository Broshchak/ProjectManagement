using System.ComponentModel.DataAnnotations;

namespace Application.Dtos.Users;

public sealed record UserDto(int Id, string Login, string FullName, string Role, bool IsActive,
    bool TelegramAllowed, long? TelegramUserId);

public class SaveUserDto
{
    [Required, StringLength(100)] public string Login { get; set; } = "";
    [Required, StringLength(150)] public string FullName { get; set; } = "";
    [Required] public string Role { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool TelegramAllowed { get; set; }
    [Range(1, long.MaxValue)] public long? TelegramUserId { get; set; }
}

public sealed class CreateUserDto : SaveUserDto
{
    [Required, StringLength(256)] public string Password { get; set; } = "";
}

public sealed record ChangeUserPasswordDto([Required, StringLength(256)] string Password);
