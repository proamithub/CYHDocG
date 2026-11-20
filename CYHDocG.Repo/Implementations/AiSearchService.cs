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
            result.AiAdviceHi = "विशेषज्ञ डॉक्टरों की सिफारिश पाने के लिए कृपया अपने लक्षणों का वर्णन करें।";
            result.AiAdviceHinglish = "Specialist doctor recommendations pane ke liye kripya apne symptoms batayein.";
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
                // Fallback to internal clinical rule engine on any API network/rate limit error
            }
        }

        // Resilient Clinical NLP Engine Fallback
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
  ""adviceEn"": ""Medical guidance in English"",
  ""adviceHi"": ""Medical guidance in Hindi"",
  ""adviceHinglish"": ""Medical guidance in Hinglish"",
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
                    new { role = "system", content = "You are a clinical triage AI. Output only valid JSON." },
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
            // Clean json markdown wrappers if returned
            string clean = jsonText.Trim();
            if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(7);
            if (clean.StartsWith("```")) clean = clean.Substring(3);
            if (clean.EndsWith("```")) clean = clean.Substring(0, clean.Length - 3);
            clean = clean.Trim();

            using var doc = JsonDocument.Parse(clean);
            var root = doc.RootElement;

            var res = new AiDiagnosisResultDto
            {
                QueryText = query,
                IdentifiedDepartment = root.GetProperty("department").GetString() ?? "General Medicine",
                SpecialtyNeeded = root.GetProperty("specialty").GetString() ?? "Physician",
                Severity = root.GetProperty("severity").GetString() ?? "Moderate",
                IsEmergency = root.GetProperty("isEmergency").GetBoolean(),
                AiAdviceEn = root.GetProperty("adviceEn").GetString() ?? "",
                AiAdviceHi = root.GetProperty("adviceHi").GetString() ?? "",
                AiAdviceHinglish = root.GetProperty("adviceHinglish").GetString() ?? ""
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
        if (ContainsAny(q, "chest pain", "heart", "cardio", "seene me dard", "dil", "palpitation", "high bp", "hypertension", "bp", "blood pressure", "ghabrahat", "shortness of breath"))
        {
            result.IdentifiedDepartment = "Cardiology";
            result.SpecialtyNeeded = "Cardiologist / Heart Specialist";
            result.Severity = ContainsAny(q, "severe", "pain", "dard", "sudden", "left arm") ? "High" : "Moderate";
            result.IsEmergency = ContainsAny(q, "severe chest", "left arm", "collapse", "breathless");
            result.MatchedKeywords.Add("Cardiac / Chest Sensation");
            result.AiAdviceEn = "Chest or heart-related discomfort requires immediate professional attention. Monitor your pulse and blood pressure.";
            result.AiAdviceHi = "सीने या हृदय से संबंधित परेशानी में तुरंत डॉक्टर से संपर्क करना चाहिए। अपने रक्तचाप (BP) की जाँच करें।";
            result.AiAdviceHinglish = "Chest ya heart related takleef me turant doctor se consult karein. BP aur pulse monitor karein.";
        }
        // 2. Dermatology (Skin)
        else if (ContainsAny(q, "skin", "rash", "itching", "khujli", "allergy", "acne", "pimple", "eczema", "fungal", "daad", "charm rog"))
        {
            result.IdentifiedDepartment = "Dermatology";
            result.SpecialtyNeeded = "Dermatologist / Skin Specialist";
            result.Severity = "Low";
            result.MatchedKeywords.Add("Dermatological / Skin condition");
            result.AiAdviceEn = "Skin conditions benefit from direct visual consultation. Avoid scratching or applying unprescribed steroid creams.";
            result.AiAdviceHi = "त्वचा संबंधी समस्या के लिए डॉक्टर को फोटो दिखाएं या लाइव परामर्श लें। बिना सलाह के क्रीम न लगाएं।";
            result.AiAdviceHinglish = "Skin problems ke liye doctor ko live dikhayein. Bina doctor ki salah ke koi steroid cream na lagayein.";
        }
        // 3. Neurology (Brain, Nerves, Headaches)
        else if (ContainsAny(q, "headache", "sir dard", "migraine", "chakkar", "dizziness", "vertigo", "seizure", "numbness", "sunn"))
        {
            result.IdentifiedDepartment = "Neurology";
            result.SpecialtyNeeded = "Neurologist";
            result.Severity = ContainsAny(q, "sudden", "blackout", "vomiting", "seizure") ? "High" : "Moderate";
            result.IsEmergency = ContainsAny(q, "blackout", "seizure", "paralysis");
            result.MatchedKeywords.Add("Neurological / Migraine symptoms");
            result.AiAdviceEn = "Frequent headaches, migraines or dizziness require proper neurological assessment. Ensure sufficient hydration.";
            result.AiAdviceHi = "बार-बार सिरदर्द, माइग्रेन या चक्कर आने पर न्यूरोलॉजिस्ट से परामर्श लें। पानी पर्याप्त मात्रा में पिएं।";
            result.AiAdviceHinglish = "Bar bar sir dard ya chakkar aane par neurologist se consult karein. Paani khoob piyein.";
        }
        // 4. Orthopedics (Bones, Joints)
        else if (ContainsAny(q, "bone", "joint", "haddi", "ghutna", "knee", "back pain", "kamar dard", "fracture", "ligament", "sprain", "moch"))
        {
            result.IdentifiedDepartment = "Orthopedics";
            result.SpecialtyNeeded = "Orthopedic Specialist / Bone Surgeon";
            result.Severity = ContainsAny(q, "fracture", "swollen", "unable to walk") ? "High" : "Moderate";
            result.MatchedKeywords.Add("Musculoskeletal / Orthopedic pain");
            result.AiAdviceEn = "Bone, knee or spine pains should be evaluated to avoid joint degeneration. Rest and avoid high impact.";
            result.AiAdviceHi = "हड्डियों, घुटने या कमर के दर्द के लिए आर्थोपेडिक विशेषज्ञ से परामर्श लें। अधिक वजन उठाने से बचें।";
            result.AiAdviceHinglish = "Haddi ya kamar dard ke liye orthopedic doctor se consult karein. Zyada wajan uthane se bachein.";
        }
        // 5. Pediatrics (Children)
        else if (ContainsAny(q, "child", "baby", "bacha", "pediatric", "infant", "newborn", "teething", "vaccination"))
        {
            result.IdentifiedDepartment = "Pediatrics";
            result.SpecialtyNeeded = "Pediatrician / Child Specialist";
            result.Severity = ContainsAny(q, "high fever", "breathing", "vomiting") ? "High" : "Moderate";
            result.MatchedKeywords.Add("Pediatric / Child care");
            result.AiAdviceEn = "Children's health needs delicate and timely pediatric guidance. Keep child hydrated and monitor temperature.";
            result.AiAdviceHi = "बच्चों की स्वास्थ्य समस्याओं में तुरंत शिशु रोग विशेषज्ञ से सलाह लें। बच्चे का तापमान चेक करते रहें।";
            result.AiAdviceHinglish = "Baccho ki bimari me turant pediatrician se milein. Temperature monitor karein.";
        }
        // 6. Gynecology (Women Health)
        else if (ContainsAny(q, "pregnancy", "period", "periods", "mahina", "gynec", "female", "menstrual", "pcod", "pcos", "garbh"))
        {
            result.IdentifiedDepartment = "Gynecology";
            result.SpecialtyNeeded = "Gynecologist / Women's Specialist";
            result.Severity = "Moderate";
            result.MatchedKeywords.Add("Gynecological / Women health");
            result.AiAdviceEn = "Women's wellness and reproductive health require personalized consultation with a verified gynecologist.";
            result.AiAdviceHi = "महिला स्वास्थ्य और प्रजनन समस्याओं के लिए स्त्री रोग विशेषज्ञ (Gynecologist) से परामर्श लें।";
            result.AiAdviceHinglish = "Ladies health aur periods related pareshani ke liye gynecologist se consult karein.";
        }
        // 7. General Medicine
        else
        {
            result.IdentifiedDepartment = "General Medicine";
            result.SpecialtyNeeded = "General Physician / MD Medicine";
            result.Severity = "Low";
            result.MatchedKeywords.Add("General constitutional symptoms");
            result.AiAdviceEn = "Consult a General Physician for comprehensive diagnosis, blood tests, and personalized care.";
            result.AiAdviceHi = "उचित निदान और दवा के लिए सामान्य चिकित्सक (General Physician) से परामर्श लें।";
            result.AiAdviceHinglish = "Proper checkup aur dawa ke liye General Physician se consult karein.";
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
