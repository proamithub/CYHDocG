using CYHDocG.Repo.Common;
using CYHDocG.Repo.DTOs;

namespace CYHDocG.Mobile.Services;

public class AuthMobileService
{
    private readonly ApiClient _api;
    public AuthMobileService(ApiClient api) => _api = api;

    public Task<dynamic?> LoginAsync(string email, string password) =>
        _api.PostAsync<LoginRequestDto, dynamic>("/api/auth/login", new LoginRequestDto { Email = email, Password = password });

    public Task<ApiResponse<long>?> RegisterAsync(RegisterRequestDto request) =>
        _api.PostAsync<RegisterRequestDto, ApiResponse<long>>("/api/auth/register", request);

    public Task<dynamic?> GoogleLoginAsync(GoogleAuthDto request) =>
        _api.PostAsync<GoogleAuthDto, dynamic>("/api/auth/google-login", request);
}

public class DoctorMobileService
{
    private readonly ApiClient _api;
    public DoctorMobileService(ApiClient api) => _api = api;

    public Task<IEnumerable<DoctorReelDto>?> GetReelsFeedAsync(long? currentUserId, int page = 1) =>
        _api.GetAsync<IEnumerable<DoctorReelDto>>($"/api/doctors/reels?currentUserId={currentUserId}&page={page}");

    public Task<IEnumerable<DoctorSearchDto>?> SearchDoctorsAsync(string query) =>
        _api.GetAsync<IEnumerable<DoctorSearchDto>>($"/api/doctors/search?query={Uri.EscapeDataString(query)}");
}

public class ConsultationMobileService
{
    private readonly ApiClient _api;
    public ConsultationMobileService(ApiClient api) => _api = api;

    public Task<ApiResponse<long>?> RequestConsultationAsync(long doctorId, long userId) =>
        _api.PostAsync<ConsultationRequestDto, ApiResponse<long>>("/api/consultations/request", new ConsultationRequestDto { DoctorId = doctorId, UserId = userId });

    public Task<ApiResponse<ConsultationEndDto>?> EndConsultationAsync(long consultationId, long endedByUserId) =>
        _api.PostAsync<object, ApiResponse<ConsultationEndDto>>($"/api/consultations/end?consultationId={consultationId}&endedByUserId={endedByUserId}", new { });
}

public class WalletMobileService
{
    private readonly ApiClient _api;
    public WalletMobileService(ApiClient api) => _api = api;

    public Task<WalletBalanceDto?> GetBalanceAsync(long userId) =>
        _api.GetAsync<WalletBalanceDto>($"/api/wallets/{userId}/balance");

    public Task<ApiResponse<decimal>?> TopUpAsync(long userId, decimal amount, string refId) =>
        _api.PostAsync<WalletTopUpDto, ApiResponse<decimal>>("/api/wallets/topup", new WalletTopUpDto { UserId = userId, Amount = amount, ReferenceId = refId });
}

public class PrescriptionMobileService
{
    private readonly ApiClient _api;
    public PrescriptionMobileService(ApiClient api) => _api = api;

    public Task<IEnumerable<MasterMedicineDto>?> SearchMedicinesAsync(string query) =>
        _api.GetAsync<IEnumerable<MasterMedicineDto>>($"/api/prescriptions/medicines/search?q={Uri.EscapeDataString(query)}");

    public Task<IEnumerable<PrescriptionHeaderDto>?> GetUserPrescriptionsAsync(long userId) =>
        _api.GetAsync<IEnumerable<PrescriptionHeaderDto>>($"/api/prescriptions/{userId}");
}
