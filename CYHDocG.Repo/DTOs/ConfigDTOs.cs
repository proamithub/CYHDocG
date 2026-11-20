namespace CYHDocG.Repo.DTOs;

public class AppConfigDto
{
    public AuthenticationConfig Authentication { get; set; } = new();
    public AiSettingsConfig AiSettings { get; set; } = new();
}

public class AuthenticationConfig
{
    public GoogleAuthConfig Google { get; set; } = new();
    public JwtConfig Jwt { get; set; } = new();
}

public class GoogleAuthConfig
{
    public string ClientId { get; set; } = "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com";
    public string ClientSecret { get; set; } = "YOUR_GOOGLE_CLIENT_SECRET";
}

public class JwtConfig
{
    public string SecretKey { get; set; } = "CYHDocGuru_SuperSecret_Jwt_AuthKey_2026_@Security#Token!";
    public string Issuer { get; set; } = "https://cyhdocgapi.patnaorthoclinic.com";
    public string Audience { get; set; } = "https://cyhdocg.patnaorthoclinic.com";
    public int ExpiryMinutes { get; set; } = 1440; // 24 hours
}

public class AiSettingsConfig
{
    public string Provider { get; set; } = "GoogleGemini"; // "GoogleGemini" or "OpenAI"
    public string ApiKey { get; set; } = "YOUR_GEMINI_OR_OPENAI_API_KEY";
    public string Model { get; set; } = "gemini-1.5-flash";
    public double Temperature { get; set; } = 0.3;
    public bool IsAiEnabled { get; set; } = true;
}

public class AuthTokenResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserProfileDto? User { get; set; }
}
