using Dapper;
using Microsoft.Extensions.Configuration;
using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class AuthRepository : BaseRepository, IAuthRepository
{
    public AuthRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<ApiResponse<long>> RegisterUserAsync(RegisterRequestDto request)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@FullName", request.FullName);
        parameters.Add("@Email", request.Email);
        parameters.Add("@PasswordHash", request.Password);
        parameters.Add("@Phone", request.Phone);
        parameters.Add("@RoleName", request.RoleName);

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

    public async Task<UserProfileDto?> GoogleLoginAsync(GoogleAuthDto request)
    {
        var parameters = new
        {
            GoogleId = request.GoogleId,
            Email = request.Email,
            FullName = request.FullName,
            AvatarUrl = request.AvatarUrl
        };
        return await QueryFirstOrDefaultSPAsync<UserProfileDto>("dbo.usp_Auth_GoogleLogin", parameters);
    }

    public async Task<ApiResponse> ChangePasswordAsync(ChangePasswordDto request)
    {
        var result = await QueryFirstOrDefaultSPAsync<dynamic>("dbo.usp_User_ChangePassword", new
        {
            UserId = request.UserId,
            OldPasswordHash = request.OldPassword,
            NewPasswordHash = request.NewPassword
        });

        if (result != null && (int)result.Success == 1)
        {
            return ApiResponse.Ok((string)result.Message);
        }
        return ApiResponse.Fail(result != null ? (string)result.Message : "Password change failed");
    }
}
