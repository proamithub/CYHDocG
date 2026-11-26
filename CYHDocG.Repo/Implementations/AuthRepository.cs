using Dapper;
using Microsoft.Extensions.Configuration;
using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;
using CYHDocG.Repo.Security;

namespace CYHDocG.Repo.Implementations;

public class AuthRepository : BaseRepository, IAuthRepository
{
    public AuthRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<ApiResponse<long>> RegisterUserAsync(RegisterRequestDto request)
    {
        // Public registration may only create User or Doctor accounts (never Admin)
        string safeRole = string.Equals(request.RoleName, "Doctor", StringComparison.OrdinalIgnoreCase)
            ? "Doctor"
            : "User";

        string hashedPassword = PasswordHasher.Hash(request.Password);

        var parameters = new DynamicParameters();
        parameters.Add("@FullName", request.FullName);
        parameters.Add("@Email", request.Email);
        parameters.Add("@PasswordHash", hashedPassword);
        parameters.Add("@Phone", request.Phone);
        parameters.Add("@RoleName", safeRole);

        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_Auth_RegisterUser", parameters);
        if (result != null && (int)result.Success == 1)
        {
            return ApiResponse<long>.Ok((long)result.UserId, (string)result.Message);
        }
        return ApiResponse<long>.Fail(result != null ? (string)result.Message : "Registration failed");
    }

    public async Task<UserProfileDto?> LoginUserAsync(string email)
    {
        return await QueryFirstOrDefaultSPAsync<UserProfileDto>("dbo.usp_Auth_LoginUser", new { Email = email });
    }

    public async Task<UserProfileDto?> AuthenticateUserAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var user = await LoginUserAsync(email.Trim());
        if (user == null || !PasswordHasher.Verify(password, user.PasswordHash, out bool needsRehash))
        {
            return null;
        }

        if (needsRehash)
        {
            try
            {
                string upgradedHash = PasswordHasher.Hash(password);
                using var connection = CreateConnection();
                await connection.ExecuteAsync(
                    "UPDATE dbo.Users SET PasswordHash = @NewHash, UpdatedAt = SYSUTCDATETIME() WHERE UserId = @UserId",
                    new { NewHash = upgradedHash, UserId = user.UserId });
            }
            catch
            {
                // Do not fail login if background rehash update fails
            }
        }

        user.PasswordHash = null;
        return user;
    }

    public async Task<UserProfileDto?> GoogleLoginAsync(GoogleAuthDto request)
    {
        var parameters = new
        {
            GoogleId = request.GoogleId,
            Email = request.Email,
            FullName = request.FullName,
            AvatarUrl = request.AvatarUrl
        };
        var user = await QueryFirstOrDefaultSPAsync<UserProfileDto>("dbo.usp_Auth_GoogleLogin", parameters);
        if (user != null)
        {
            user.PasswordHash = null;
        }
        return user;
    }

    public async Task<ApiResponse> ChangePasswordAsync(ChangePasswordDto request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return ApiResponse.Fail("New password must be at least 6 characters");
        }

        using var connection = CreateConnection();
        var storedHash = await connection.QueryFirstOrDefaultAsync<string?>(
            "SELECT PasswordHash FROM dbo.Users WHERE UserId = @UserId AND IsActive = 1",
            new { UserId = request.UserId });

        if (string.IsNullOrEmpty(storedHash))
        {
            return ApiResponse.Fail("Password login is not enabled for this Google-linked account.");
        }

        if (!PasswordHasher.Verify(request.OldPassword, storedHash, out _))
        {
            return ApiResponse.Fail("Current password is incorrect");
        }

        string newHash = PasswordHasher.Hash(request.NewPassword);
        int affected = await connection.ExecuteAsync(
            "UPDATE dbo.Users SET PasswordHash = @NewHash, UpdatedAt = SYSUTCDATETIME() WHERE UserId = @UserId AND IsActive = 1",
            new { NewHash = newHash, UserId = request.UserId });

        return affected > 0
            ? ApiResponse.Ok("Password changed successfully")
            : ApiResponse.Fail("Unable to update password");
    }

    public async Task<IEnumerable<AdminUserListDto>> GetAllUsersForAdminAsync(string? search = null)
    {
        return await QuerySPAsync<AdminUserListDto>("dbo.usp_Admin_GetUsers", new { Search = search });
    }

    public async Task<ApiResponse> ToggleUserActiveStatusAsync(long userId)
    {
        var result = await QueryFirstOrDefaultSPAsync<bool?>("dbo.usp_Admin_ToggleUserStatus", new { UserId = userId });
        bool isActive = result ?? true;
        return ApiResponse.Ok(isActive ? "User activated successfully" : "User deactivated successfully");
    }
}
