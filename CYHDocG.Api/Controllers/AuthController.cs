using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthRepository _authRepo;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IConfiguration _config;

    public AuthController(IAuthRepository authRepo, IJwtTokenService jwtTokenService, IConfiguration config)
    {
        _authRepo = authRepo;
        _jwtTokenService = jwtTokenService;
        _config = config;
    }

    private JwtConfig GetEffectiveJwtConfig()
    {
        var config = _config.GetSection("Authentication:Jwt").Get<JwtConfig>() ?? new JwtConfig();
        if (!string.IsNullOrWhiteSpace(_config["Jwt:Key"])) config.SecretKey = _config["Jwt:Key"]!;
        if (!string.IsNullOrWhiteSpace(_config["Jwt:Issuer"])) config.Issuer = _config["Jwt:Issuer"]!;
        if (!string.IsNullOrWhiteSpace(_config["Jwt:Audience"])) config.Audience = _config["Jwt:Audience"]!;
        return config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var user = await _authRepo.LoginUserAsync(request.Email);
        if (user == null || user.PasswordHash != request.Password)
        {
            return Unauthorized(new { Success = false, Message = "Invalid email or password" });
        }

        var jwtConfig = GetEffectiveJwtConfig();
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
        var result = await _authRepo.RegisterUserAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        // Auto login
        var user = await _authRepo.LoginUserAsync(request.Email);
        if (user != null)
        {
            var jwtConfig = GetEffectiveJwtConfig();
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
        var user = await _authRepo.GoogleLoginAsync(request);
        if (user == null)
        {
            return BadRequest(new { Success = false, Message = "Google sign-in failed" });
        }

        var jwtConfig = GetEffectiveJwtConfig();
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
