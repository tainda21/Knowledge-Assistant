using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.DTOs;
using WebAPI.Models;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController(
    AppDbContext db,
    IPasswordHasher passwordHasher
) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserDto(u.Id, u.Username, u.Role, u.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3 || username.Length > 64)
        {
            return BadRequest(new { message = "Username must be between 3 and 64 characters." });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new { message = "Password must be at least 6 characters long." });
        }

        var role = request.Role?.Trim();
        if (role != "Admin" && role != "User")
        {
            return BadRequest(new { message = "Role must be either 'Admin' or 'User'." });
        }

        var normalizedUsername = username.ToLowerInvariant();
        var exists = await db.Users.AnyAsync(
            u => u.Username.ToLower() == normalizedUsername,
            cancellationToken
        );

        if (exists)
        {
            return Conflict(new { message = "A user with this username already exists." });
        }

        var (hash, salt) = passwordHasher.HashPassword(request.Password);
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = hash,
            Salt = salt,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(newUser);
        await db.SaveChangesAsync(cancellationToken);

        var dto = new UserDto(newUser.Id, newUser.Username, newUser.Role, newUser.CreatedAt);
        return CreatedAtAction(nameof(GetAll), new { id = newUser.Id }, dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(currentUserIdStr, out var currentUserId) && currentUserId == id)
        {
            return BadRequest(new { message = "You cannot delete your own account." });
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
