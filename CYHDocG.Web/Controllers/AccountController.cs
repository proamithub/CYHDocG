using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthRepository _authRepo;
    private readonly IConfiguration _config;

    public AccountController(IAuthRepository authRepo, IConfiguration config)
    {
        _authRepo = authRepo;
        _config = config;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToUserDashboard();
        }
        ViewData["ReturnUrl"] = returnUrl;
        ViewBag.GoogleClientId = _config["Authentication:Google:ClientId"] ?? _config["Google:ClientId"];
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequestDto model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _authRepo.LoginUserAsync(model.Email);
        if (user == null || user.PasswordHash != model.Password) // Note: In production use BCrypt/Argon2
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        await SignInUserClaimsAsync(user);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToRoleDashboard(user.RoleName);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToUserDashboard();
        }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterRequestDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authRepo.RegisterUserAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        // Auto login on register
        var user = await _authRepo.LoginUserAsync(model.Email);
        if (user != null)
        {
            await SignInUserClaimsAsync(user);
            return RedirectToRoleDashboard(user.RoleName);
        }

        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleAuthDto model)
    {
        if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.GoogleId))
        {
            return BadRequest(new { Success = false, Message = "Invalid Google account data" });
        }

        var user = await _authRepo.GoogleLoginAsync(model);
        if (user == null)
        {
            return BadRequest(new { Success = false, Message = "Google sign-in failed" });
        }

        await SignInUserClaimsAsync(user);

        string redirectUrl = user.RoleName switch
        {
            "Admin" => "/Admin/Dashboard",
            "Doctor" => "/Doctor/Dashboard",
            _ => "/User/Dashboard"
        };

        return Ok(new { Success = true, RedirectUrl = redirectUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("CYHAuthCookie");
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View();
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (long.TryParse(userIdClaim, out long userId))
        {
            model.UserId = userId;
            var result = await _authRepo.ChangePasswordAsync(model);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "Password changed successfully!";
                return RedirectToAction(nameof(ChangePassword));
            }
            ModelState.AddModelError(string.Empty, result.Message);
        }
        return View(model);
    }

    private async Task SignInUserClaimsAsync(UserProfileDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.RoleName),
            new("AvatarUrl", user.AvatarUrl ?? ""),
            new("WalletBalance", user.WalletBalance.ToString("F2")),
            new("DoctorId", user.DoctorId?.ToString() ?? "")
        };

        var claimsIdentity = new ClaimsIdentity(claims, "CYHAuthCookie");
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        await HttpContext.SignInAsync("CYHAuthCookie", new ClaimsPrincipal(claimsIdentity), authProperties);
    }

    private IActionResult RedirectToRoleDashboard(string role)
    {
        return role switch
        {
            "Admin" => RedirectToAction("Index", "Dashboard", new { area = "Admin" }),
            "Doctor" => RedirectToAction("Index", "Dashboard", new { area = "Doctor" }),
            _ => RedirectToAction("Index", "Dashboard", new { area = "User" })
        };
    }

    private IActionResult RedirectToUserDashboard()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        return RedirectToRoleDashboard(role ?? "User");
    }
}
