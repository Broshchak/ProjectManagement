using System.ComponentModel.DataAnnotations;
using Application.Common;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Dtos.Users;
using Domain.Entities;

namespace Application.Services;

public sealed class UserManagementService(IUserRepository users, IPasswordHasher hasher, IUnitOfWork unitOfWork, IRefreshTokenRepository refreshTokens)
    : IUserManagementService
{
    public Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default) => users.GetAllAsync(ct);
    public Task<UserDto?> GetAsync(int id, CancellationToken ct = default) => users.GetDetailsAsync(id, ct);

    public Task<UserDto?> SaveAsync(int? id, SaveUserDto request, string? password, CancellationToken ct = default) =>
        id.HasValue
            ? unitOfWork.InTransactionAsync(token => SaveCoreAsync(id, request, password, token), ct)
            : SaveCoreAsync(id, request, password, ct);

    private async Task<UserDto?> SaveCoreAsync(int? id, SaveUserDto request, string? password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.FullName) ||
            (!id.HasValue && string.IsNullOrWhiteSpace(password)))
            throw new ValidationException("Login, name and initial password are required.");
        if (request.TelegramAllowed && !request.TelegramUserId.HasValue)
            throw new ValidationException("Telegram User ID is required when Telegram access is enabled.");
        string login = LoginRules.Normalize(request.Login);
        if (!LoginRules.IsValid(login))
            throw new ValidationException("Login must be 3–100 characters: lowercase letters, digits, '.', '_' or '-'.");
        if (request.Role is not null && request.RoleCode is not null && request.Role.Trim() != request.RoleCode.Trim())
            throw new ValidationException("Role and roleCode must agree.");
        if (password is not null) ValidatePassword(login, password);
        if (await users.IdentifiersInUseAsync(id, login, request.TelegramUserId, ct))
            throw new DataConflictException("Login or Telegram User ID is already in use.");
        var role = await users.GetRoleByCodeAsync((request.RoleCode ?? request.Role ?? string.Empty).Trim(), ct)
            ?? throw new ValidationException("Unknown role.");
        var user = id.HasValue ? await users.FindAsync(id.Value, ct) : new AppUserEntity();
        if (user is null) return null;
        if (!role.IsActive && (!id.HasValue || user.RoleId != role.Id))
            throw new ValidationException("A blocked role cannot be assigned.");
        if (id.HasValue && (!request.IsActive || user.RoleId != role.Id) &&
            await users.IsLastActiveAdminAsync(id.Value, ct))
            throw new DataConflictException("The last active administrator cannot be disabled or reassigned.");
        user.Login = login;
        user.FullName = request.FullName.Trim();
        user.RoleId = role.Id;
        user.IsActive = request.IsActive;
        user.TelegramAllowed = request.TelegramAllowed;
        user.TelegramUserId = request.TelegramUserId;
        if (password is not null) user.PasswordHash = await hasher.HashAsync(password, ct);
        if (!id.HasValue) await users.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
        if (id.HasValue && !user.IsActive)
            await refreshTokens.RevokeAllForUserAsync(user.Id, DateTime.UtcNow, ct);
        return await users.GetDetailsAsync(user.Id, ct);
    }

    public async Task<bool> ChangePasswordAsync(int id, string password, CancellationToken ct = default)
    {
        var user = await users.FindAsync(id, ct);
        if (user is null) return false;
        ValidatePassword(user.Login, password);
        user.PasswordHash = await hasher.HashAsync(password, ct);
        return await SaveAndRevokeAsync(id, ct);
    }

    private Task<bool> SaveAndRevokeAsync(int userId, CancellationToken ct) =>
        unitOfWork.InTransactionAsync(async token =>
        {
            await unitOfWork.SaveChangesAsync(token);
            await refreshTokens.RevokeAllForUserAsync(userId, DateTime.UtcNow, token);
            return true;
        }, ct);

    private static void ValidatePassword(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 10 or > AuthService.MaxPasswordLength)
            throw new ValidationException($"Password must be 10–{AuthService.MaxPasswordLength} characters long.");
        if (string.Equals(password, login, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Password must not be the same as the login.");
    }
}
