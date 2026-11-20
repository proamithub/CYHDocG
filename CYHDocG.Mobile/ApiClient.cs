using System.Net.Http.Json;
using CYHDocG.Repo.Common;

namespace CYHDocG.Mobile;

/// <summary>
/// Universal Mobile API Client connecting directly to CYHDocG.Api
/// Uses shared Repo DTOs so models are NEVER repeated.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(string apiBaseUrl = "https://cyhdocgapi.patnaorthoclinic.com")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl)
        };
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        return await _httpClient.GetFromJsonAsync<T>(endpoint);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest payload)
    {
        var response = await _httpClient.PostAsJsonAsync(endpoint, payload);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<TResponse>();
        }
        return default;
    }
}
