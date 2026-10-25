using Application.Common.Interfaces;
using Application.Dtos.Users;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace WebApi.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(AppDbContext db, IPasswordHasher hasher) : ControllerBase
{
    private IQueryable<UserDto> Query(int? id = null) =>
        from user in db.Set<AppUserEntity>().AsNoTracking()
        join role in db.Set<RoleEntity>() on user.RoleId equals role.Id
        where id == null || user.Id == id
        orderby user.FullName
        select new UserDto(user.Id, user.Login, user.FullName, role.Code, user.IsActive, user.TelegramAllowed, user.TelegramUserId);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(CancellationToken ct) => await Query().ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetUser(int id, CancellationToken ct)
    {
        var user = await Query(id).FirstOrDefaultAsync(ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public Task<ActionResult<UserDto>> Create(CreateUserDto request, CancellationToken ct) => Save(null, request, request.Password, ct);

    [HttpPut("{id:int}")]
    public Task<ActionResult<UserDto>> Update(int id, SaveUserDto request, CancellationToken ct) => Save(id, request, null, ct);

    [HttpPut("{id:int}/password")]
    public async Task<IActionResult> ChangePassword(int id, ChangeUserPasswordDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest(new { message = "Password is required." });
        var user = await db.Set<AppUserEntity>().FindAsync([id], ct);
        if (user is null) return NotFound();
        user.PasswordHash = hasher.Hash(request.Password);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ActionResult<UserDto>> Save(int? id, SaveUserDto request, string? password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.FullName) ||
            (!id.HasValue && string.IsNullOrWhiteSpace(password)))
            return BadRequest(new { message = "Login, name and initial password are required." });
        if (request.TelegramAllowed && !request.TelegramUserId.HasValue)
            return BadRequest(new { message = "Telegram User ID is required when Telegram access is enabled." });
        string login = request.Login.Trim().ToLowerInvariant();
        if (await db.Set<AppUserEntity>().AnyAsync(u => u.Id != id && u.Login.ToLower() == login, ct) ||
            (request.TelegramUserId.HasValue && await db.Set<AppUserEntity>().AnyAsync(u => u.Id != id && u.TelegramUserId == request.TelegramUserId, ct)))
            return Conflict(new { message = "Login or Telegram User ID is already in use." });
        var role = await db.Set<RoleEntity>().FirstOrDefaultAsync(r => r.Code == request.Role, ct);
        if (role is null) return BadRequest(new { message = "Unknown role." });
        var user = id.HasValue ? await db.Set<AppUserEntity>().FindAsync([id.Value], ct) : new AppUserEntity();
        if (user is null) return NotFound();
        user.Login = login;
        user.FullName = request.FullName.Trim();
        user.RoleId = role.Id;
        user.IsActive = request.IsActive;
        user.TelegramAllowed = request.TelegramAllowed;
        user.TelegramUserId = request.TelegramUserId;
        if (password is not null) user.PasswordHash = hasher.Hash(password);
        if (!id.HasValue) db.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new { message = "Login or Telegram User ID is already in use." }); }
        UserDto result = (await Query(user.Id).FirstAsync(ct));
        return id.HasValue ? Ok(result) : CreatedAtAction(nameof(GetUser), new { id = user.Id }, result);
    }
}
