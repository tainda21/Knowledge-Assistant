using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.DTOs;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService
) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required." });
        }

        var normalizedUsername = username.ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(
            u => u.Username.ToLower() == normalizedUsername,
            cancellationToken
        );

        if (user is null || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash, user.Salt))
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var token = tokenService.GenerateToken(user);
        var userDto = new UserDto(user.Id, user.Username, user.Role, user.CreatedAt);

        return Ok(new LoginResponse(token, userDto));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(new UserDto(user.Id, user.Username, user.Role, user.CreatedAt));
    }
}
