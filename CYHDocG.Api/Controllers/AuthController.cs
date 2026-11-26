using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;
using CYHDocG.Repo.Security;

namespace CYHDocG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthRepository _authRepo;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IGoogleTokenVerifier _googleVerifier;
    private readonly IConfiguration _config;

    public AuthController(
        IAuthRepository authRepo,
        IJwtTokenService jwtTokenService,
        IGoogleTokenVerifier googleVerifier,
        IConfiguration config)
    {
        _authRepo = authRepo;
        _jwtTokenService = jwtTokenService;
        _googleVerifier = googleVerifier;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var user = await _authRepo.AuthenticateUserAsync(request.Email, request.Password);
        if (user == null)
        {
            return Unauthorized(new { Success = false, Message = "Invalid email or password" });
        }

        var jwtConfig = JwtConfigResolver.Resolve(_config);
        var token = _jwtTokenService.GenerateToken(user, jwtConfig);
        var expiresAt = DateTime.UtcNow.AddMinutes(jwtConfig.ExpiryMinutes);

        return Ok(new AuthTokenResponseDto
        {
            Success = true,
            Message = "Login successful",
            Token = token,
            ExpiresAt = expiresAt,
            User = user
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        request.RoleName = string.Equals(request.RoleName, "Doctor", StringComparison.OrdinalIgnoreCase)
            ? "Doctor"
            : "User";

        var result = await _authRepo.RegisterUserAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        var user = await _authRepo.AuthenticateUserAsync(request.Email, request.Password);
        if (user != null)
        {
            var jwtConfig = JwtConfigResolver.Resolve(_config);
            var token = _jwtTokenService.GenerateToken(user, jwtConfig);
            var expiresAt = DateTime.UtcNow.AddMinutes(jwtConfig.ExpiryMinutes);

            return Ok(new AuthTokenResponseDto
            {
                Success = true,
                Message = "Registration and authentication successful",
                Token = token,
                ExpiresAt = expiresAt,
                User = user
            });
        }

        return Ok(result);
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleAuthDto request)
    {
        var verifiedProfile = await _googleVerifier.VerifyIdTokenAsync(request?.IdToken);
        if (verifiedProfile == null)
        {
            return Unauthorized(new { Success = false, Message = "Invalid or unverified Google ID token" });
        }

        var user = await _authRepo.GoogleLoginAsync(verifiedProfile);
        if (user == null)
        {
            return BadRequest(new { Success = false, Message = "Google sign-in failed" });
        }

        var jwtConfig = JwtConfigResolver.Resolve(_config);
        var token = _jwtTokenService.GenerateToken(user, jwtConfig);
        var expiresAt = DateTime.UtcNow.AddMinutes(jwtConfig.ExpiryMinutes);

        return Ok(new AuthTokenResponseDto
        {
            Success = true,
            Message = "Google login successful",
            Token = token,
            ExpiresAt = expiresAt,
            User = user
        });
    }
}
