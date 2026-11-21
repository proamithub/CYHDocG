using System.ComponentModel.DataAnnotations;

namespace CYHDocG.Repo.DTOs;

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequestDto
{
    [Required(ErrorMessage = "Full Name is required")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is mandatory")]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Mobile number must be a valid 10-digit number starting with 6, 7, 8, or 9")]
    public string Phone { get; set; } = string.Empty;

    public string RoleName { get; set; } = "User";
}

public class GoogleAuthDto
{
    public string GoogleId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class UserProfileDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string RoleName { get; set; } = "User";
    public decimal WalletBalance { get; set; }
    public long? DoctorId { get; set; }
    public bool IsActive { get; set; }
}

public class ChangePasswordDto
{
    public long UserId { get; set; }
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class AdminUserListDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string RoleName { get; set; } = "User";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal WalletBalance { get; set; }
    public long? DoctorId { get; set; }
    public bool IsDoctorApproved { get; set; }
}
