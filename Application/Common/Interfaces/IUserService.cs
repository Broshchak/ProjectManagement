using Application.Dtos.Auth;

namespace Application.Common.Interfaces
{
    public interface IUserService
    {
        Task<UserDto> CreateUserAsync(CreateUserDto dto, CancellationToken ct = default);
    }
}
