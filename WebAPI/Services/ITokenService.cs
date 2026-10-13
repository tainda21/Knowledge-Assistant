using WebAPI.Models;

namespace WebAPI.Services;

public interface ITokenService
{
    string GenerateToken(User user);
}
