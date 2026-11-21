using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Repo.Implementations;

public class AiSearchService : IAiSearchService
{
    private readonly IDoctorRepository _doctorRepo;
    private readonly IConfiguration? _configuration;
    private readonly HttpClient _httpClient;

    public AiSearchService(IDoctorRepository doctorRepo, IConfiguration? configuration = null, HttpClient? httpClient = null)
    {
        _doctorRepo = doctorRepo;
        _configuration = configuration;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<AiDiagnosisResultDto> AnalyzeSymptomAsync(string query)
    {
        var result = new AiDiagnosisResultDto
        {
            QueryText = query.Trim()
        };

        if (string.IsNullOrWhiteSpace(query))
        {
            result.IdentifiedDepartment = "General Medicine";
            result.AiAdviceEn = "Please describe your symptoms to receive specialized doctor recommendations.";
            return result;
        }

        // Try External LLM if API Key is configured and enabled
        bool isAiEnabled = true;
        if (bool.TryParse(_configuration?["AiSettings:IsAiEnabled"], out bool parsedEnabled))
        {
            isAiEnabled = parsedEnabled;
        }

        string? apiKey = _configuration?["AiSettings:ApiKey"] 
                      ?? _configuration?["GeminiSettings:ApiKey"];
        string provider = _configuration?["AiSettings:Provider"] ?? "GoogleGemini";
        string model = _configuration?["AiSettings:Model"] 
                    ?? _configuration?["GeminiSettings:ModelName"] 
                    ?? "gemini-1.5-flash";

        if (isAiEnabled && !string.IsNullOrWhiteSpace(apiKey) && !apiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var llmResult = await CallLlmApiAsync(query, provider, apiKey, model);
                if (llmResult != null)
                {
                    llmResult.RecommendedDoctors = (await _doctorRepo.SearchDoctorsAsync(llmResult.IdentifiedDepartment)).ToList();
                    return llmResult;
                }
            }
            catch
            {
                // Fallback to rule engine if LLM fails
            }
        }

        // Local Rule Engine Fallback (Instant, 100% Reliable, Pure English)
        return await ExecuteRuleEngineAsync(query);
    }

    private async Task<AiDiagnosisResultDto?> CallLlmApiAsync(string query, string provider, string apiKey, string model)
    {
        string prompt = $@"You are CYHDocGuru AI clinical triage assistant. Analyze this patient query: ""{query}"".
Respond in strictly valid JSON with this schema without markdown fences:
{{
  ""department"": ""Cardiology|Dermatology|Neurology|Orthopedics|Pediatrics|Gynecology|General Medicine"",
  ""specialty"": ""Doctor Specialty Title"",
  ""severity"": ""Low|Moderate|High"",
  ""isEmergency"": true|false,
  ""advice"": ""Medical guidance in English"",
  ""keywords"": [""key1"", ""key2""]
}}";

        if (provider.Equals("GoogleGemini", StringComparison.OrdinalIgnoreCase))
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                var respString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(respString);
                string text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString() ?? "";

                return ParseLlmJsonResponse(text, query);
            }
        }
        else if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            string url = "https://api.openai.com/v1/chat/completions";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = "You are a clinical triage AI. Output only valid JSON in English." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.2
            };

            req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(req);
            if (response.IsSuccessStatusCode)
            {
                var respString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(respString);
                string text = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content").GetString() ?? "";

                return ParseLlmJsonResponse(text, query);
            }
        }

        return null;
    }

    private AiDiagnosisResultDto? ParseLlmJsonResponse(string jsonText, string query)
    {
        try
        {
            string clean = jsonText.Trim();
            if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(7);
            if (clean.StartsWith("```")) clean = clean.Substring(3);
            if (clean.EndsWith("```")) clean = clean.Substring(0, clean.Length - 3);
            clean = clean.Trim();

            using var doc = JsonDocument.Parse(clean);
            var root = doc.RootElement;

            string advice = "";
            if (root.TryGetProperty("advice", out var advProp))
            {
                advice = advProp.GetString() ?? "";
            }
            else if (root.TryGetProperty("adviceEn", out var advEnProp))
            {
                advice = advEnProp.GetString() ?? "";
            }

            var res = new AiDiagnosisResultDto
            {
                QueryText = query,
                IdentifiedDepartment = root.GetProperty("department").GetString() ?? "General Medicine",
                SpecialtyNeeded = root.GetProperty("specialty").GetString() ?? "Physician",
                Severity = root.GetProperty("severity").GetString() ?? "Moderate",
                IsEmergency = root.GetProperty("isEmergency").GetBoolean(),
                AiAdviceEn = advice
            };

            if (root.TryGetProperty("keywords", out var kwElem))
            {
                foreach (var k in kwElem.EnumerateArray())
                {
                    res.MatchedKeywords.Add(k.GetString() ?? "");
                }
            }

            return res;
        }
        catch
        {
            return null;
        }
    }

    private async Task<AiDiagnosisResultDto> ExecuteRuleEngineAsync(string query)
    {
        var result = new AiDiagnosisResultDto { QueryText = query.Trim() };
        string q = query.ToLowerInvariant();

        // 1. Cardiology (Heart)
        if (ContainsAny(q, "chest pain", "heart", "cardio", "chest", "palpitation", "high bp", "hypertension", "bp", "blood pressure", "shortness of breath"))
        {
            result.IdentifiedDepartment = "Cardiology";
            result.SpecialtyNeeded = "Cardiologist / Heart Specialist";
            result.Severity = ContainsAny(q, "severe", "pain", "sudden", "left arm") ? "High" : "Moderate";
            result.IsEmergency = ContainsAny(q, "severe chest", "left arm", "collapse", "breathless");
            result.MatchedKeywords.Add("Cardiac / Chest Sensation");
            result.AiAdviceEn = "Chest or heart-related discomfort requires immediate professional medical attention. Monitor your pulse and blood pressure.";
        }
        // 2. Dermatology (Skin)
        else if (ContainsAny(q, "skin", "rash", "itching", "allergy", "acne", "pimple", "eczema", "fungal", "lesion"))
        {
            result.IdentifiedDepartment = "Dermatology";
            result.SpecialtyNeeded = "Dermatologist / Skin Specialist";
            result.Severity = "Low";
            result.MatchedKeywords.Add("Dermatological / Skin condition");
            result.AiAdviceEn = "Skin conditions benefit from direct visual consultation. Avoid scratching or applying unprescribed steroid creams.";
        }
        // 3. Neurology (Brain, Nerves, Headaches)
        else if (ContainsAny(q, "headache", "migraine", "dizziness", "vertigo", "seizure", "numbness"))
        {
            result.IdentifiedDepartment = "Neurology";
            result.SpecialtyNeeded = "Neurologist";
            result.Severity = ContainsAny(q, "sudden", "blackout", "vomiting", "seizure") ? "High" : "Moderate";
            result.IsEmergency = ContainsAny(q, "blackout", "seizure", "paralysis");
            result.MatchedKeywords.Add("Neurological / Migraine symptoms");
            result.AiAdviceEn = "Frequent headaches, migraines or dizziness require proper neurological assessment. Ensure sufficient hydration.";
        }
        // 4. Orthopedics (Bones, Joints)
        else if (ContainsAny(q, "bone", "joint", "knee", "back pain", "fracture", "ligament", "sprain"))
        {
            result.IdentifiedDepartment = "Orthopedics";
            result.SpecialtyNeeded = "Orthopedic Specialist / Bone Surgeon";
            result.Severity = ContainsAny(q, "fracture", "swollen", "unable to walk") ? "High" : "Moderate";
            result.MatchedKeywords.Add("Musculoskeletal / Orthopedic pain");
            result.AiAdviceEn = "Bone, knee or spine pains should be evaluated to avoid joint degeneration. Rest and avoid high impact.";
        }
        // 5. Pediatrics (Children)
        else if (ContainsAny(q, "child", "baby", "pediatric", "infant", "newborn", "teething", "vaccination"))
        {
            result.IdentifiedDepartment = "Pediatrics";
            result.SpecialtyNeeded = "Pediatrician / Child Specialist";
            result.Severity = ContainsAny(q, "high fever", "breathing", "vomiting") ? "High" : "Moderate";
            result.MatchedKeywords.Add("Pediatric / Child care");
            result.AiAdviceEn = "Children's health needs delicate and timely pediatric guidance. Keep the child hydrated and monitor temperature.";
        }
        // 6. Gynecology (Women Health)
        else if (ContainsAny(q, "pregnancy", "period", "periods", "gynec", "female", "menstrual", "pcod", "pcos"))
        {
            result.IdentifiedDepartment = "Gynecology";
            result.SpecialtyNeeded = "Gynecologist / Women's Specialist";
            result.Severity = "Moderate";
            result.MatchedKeywords.Add("Gynecological / Women health");
            result.AiAdviceEn = "Women's wellness and reproductive health require personalized consultation with a verified gynecologist.";
        }
        // 7. General Medicine
        else
        {
            result.IdentifiedDepartment = "General Medicine";
            result.SpecialtyNeeded = "General Physician / MD Medicine";
            result.Severity = "Low";
            result.MatchedKeywords.Add("General constitutional symptoms");
            result.AiAdviceEn = "Consult a General Physician for comprehensive diagnosis, blood tests, and personalized care.";
        }

        // Fetch doctors
        result.RecommendedDoctors = (await _doctorRepo.SearchDoctorsAsync(result.IdentifiedDepartment)).ToList();
        return result;
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        return values.Any(v => source.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
