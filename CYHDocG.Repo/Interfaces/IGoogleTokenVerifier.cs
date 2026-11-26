using CYHDocG.Repo.DTOs;

namespace CYHDocG.Repo.Interfaces;

public interface IGoogleTokenVerifier
{
    Task<GoogleAuthDto?> VerifyIdTokenAsync(string? idToken);
    Task<string?> ResolveClientIdAsync();
}
