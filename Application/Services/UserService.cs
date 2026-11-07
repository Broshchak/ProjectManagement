using System.ComponentModel.DataAnnotations;
using Application.Common;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Dtos.Auth;
using Domain.Entities;

namespace Application.Services
{
    public class UserService(IUserRepository users, IPasswordHasher hasher, IUnitOfWork unitOfWork) : IUserService
    {
        private const int MinPasswordLength = 10;

        public async Task<UserDto> CreateUserAsync(CreateUserDto dto, CancellationToken ct = default)
        {
            var login = LoginRules.Normalize(dto.Login);
            if (!LoginRules.IsValid(login))
            {
                throw new ValidationException(
                    $"Login must be {LoginRules.MinLength}-{LoginRules.MaxLength} characters: lowercase letters, digits, '.', '_' or '-'.");
            }

            var password = dto.Password ?? string.Empty;
            if (password.Length < MinPasswordLength || password.Length > AuthService.MaxPasswordLength)
            {
                throw new ValidationException(
                    $"Password must be {MinPasswordLength}-{AuthService.MaxPasswordLength} characters long.");
            }

            if (string.Equals(password, login, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("Password must not be the same as the login.");
            }

            var fullName = dto.FullName?.Trim();
            if (string.IsNullOrEmpty(fullName) || fullName.Length > 150)
            {
                throw new ValidationException("Full name is required (max 150 characters).");
            }

            var role = await users.GetRoleByCodeAsync(dto.RoleCode?.Trim() ?? string.Empty, ct)
                ?? throw new ValidationException("Unknown role.");

            if (await users.LoginExistsAsync(login, ct))
            {
                throw new ConflictException("A user with this login already exists.");
            }

            var user = new AppUserEntity
            {
                RoleId = role.Id,
                Login = login,
                PasswordHash = await hasher.HashAsync(password, ct),
                FullName = fullName,
                IsActive = true
            };

            await users.AddAsync(user, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return new UserDto(user.Id, user.Login, user.FullName, role.Code, user.IsActive);
        }
    }
}
