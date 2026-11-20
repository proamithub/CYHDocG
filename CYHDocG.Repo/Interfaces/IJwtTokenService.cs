using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(UserProfileDto user, JwtConfig config);
}
