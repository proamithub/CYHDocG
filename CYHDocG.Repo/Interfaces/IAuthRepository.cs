using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IAuthRepository
{
    Task<ApiResponse<long>> RegisterUserAsync(RegisterRequestDto request);
    Task<UserProfileDto?> LoginUserAsync(string email);
    Task<UserProfileDto?> GoogleLoginAsync(GoogleAuthDto request);
    Task<ApiResponse> ChangePasswordAsync(ChangePasswordDto request);
}
