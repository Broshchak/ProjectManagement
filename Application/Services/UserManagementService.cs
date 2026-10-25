using System.ComponentModel.DataAnnotations;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Dtos.Users;
using Domain.Entities;

namespace Application.Services;

public sealed class UserManagementService(IUserRepository users, IPasswordHasher hasher, IUnitOfWork unitOfWork)
    : IUserManagementService
{
    public Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default) => users.GetAllAsync(ct);
    public Task<UserDto?> GetAsync(int id, CancellationToken ct = default) => users.GetDetailsAsync(id, ct);

    public async Task<UserDto?> SaveAsync(int? id, SaveUserDto request, string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.FullName) ||
            (!id.HasValue && string.IsNullOrWhiteSpace(password)))
            throw new ValidationException("Login, name and initial password are required.");
        if (request.TelegramAllowed && !request.TelegramUserId.HasValue)
            throw new ValidationException("Telegram User ID is required when Telegram access is enabled.");
        string login = request.Login.Trim().ToLowerInvariant();
        if (await users.IdentifiersInUseAsync(id, login, request.TelegramUserId, ct))
            throw new DataConflictException("Login or Telegram User ID is already in use.");
        var role = await users.GetRoleAsync(request.Role, ct)
            ?? throw new ValidationException("Unknown role.");
        var user = id.HasValue ? await users.FindAsync(id.Value, ct) : new AppUserEntity();
        if (user is null) return null;
        user.Login = login;
        user.FullName = request.FullName.Trim();
        user.RoleId = role.Id;
        user.IsActive = request.IsActive;
        user.TelegramAllowed = request.TelegramAllowed;
        user.TelegramUserId = request.TelegramUserId;
        if (password is not null) user.PasswordHash = hasher.Hash(password);
        if (!id.HasValue) await users.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await users.GetDetailsAsync(user.Id, ct);
    }

    public async Task<bool> ChangePasswordAsync(int id, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new ValidationException("Password is required.");
        var user = await users.FindAsync(id, ct);
        if (user is null) return false;
        user.PasswordHash = hasher.Hash(password);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
