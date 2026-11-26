using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IAuthRepository
{
    Task<ApiResponse<long>> RegisterUserAsync(RegisterRequestDto request);
    Task<UserProfileDto?> LoginUserAsync(string email);
    Task<UserProfileDto?> AuthenticateUserAsync(string email, string password);
    Task<UserProfileDto?> GoogleLoginAsync(GoogleAuthDto request);
    Task<ApiResponse> ChangePasswordAsync(ChangePasswordDto request);
    Task<IEnumerable<AdminUserListDto>> GetAllUsersForAdminAsync(string? search = null);
    Task<ApiResponse> ToggleUserActiveStatusAsync(long userId);
}
