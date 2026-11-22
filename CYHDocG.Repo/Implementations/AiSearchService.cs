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
            QueryText = query?.Trim() ?? string.Empty
        };

        // Fetch currently live streaming doctors
        List<DoctorReelDto> liveDoctors = new();
        try
        {
            var reels = await _doctorRepo.GetReelsFeedAsync(null, 1, 5);
            liveDoctors = reels.Where(r => r.IsLive).ToList();
        }
        catch { }

        if (string.IsNullOrWhiteSpace(query))
        {
            result.IdentifiedDepartment = "General Medicine";
            result.AiAdviceEn = "Please describe your symptoms or health queries to receive specialized clinical guidance.";
            result.LiveDoctors = liveDoctors;
            return result;
        }

        // Try External LLM if API Key is configured and enabled
        bool isAiEnabled = true;
        if (bool.TryParse(_configuration?["AiSettings:IsAiEnabled"], out bool parsedEnabled))
        {
            isAiEnabled = parsedEnabled;
        }

        string? apiKey = _configuration?["AiSettings:ApiKey"] 
                      ?? _configuration?["GeminiSettings:ApiKey"]
                      ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                      ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

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
                    llmResult.LiveDoctors = liveDoctors;
                    llmResult.RecommendedDoctors = (await _doctorRepo.SearchDoctorsAsync(llmResult.IdentifiedDepartment)).ToList();
                    AttachLiveDoctorNotice(llmResult, liveDoctors);
                    return llmResult;
                }
            }
            catch
            {
                // Fallback to rule engine if LLM fails
            }
        }

        // Local Rule Engine Fallback (Instant, 100% Reliable, Pure English)
        var ruleResult = await ExecuteRuleEngineAsync(query, liveDoctors);
        ruleResult.LiveDoctors = liveDoctors;
        return ruleResult;
    }

    private static void AttachLiveDoctorNotice(AiDiagnosisResultDto result, List<DoctorReelDto> liveDoctors)
    {
        if (!result.IsMedical)
        {
            if (liveDoctors != null && liveDoctors.Any())
            {
                var doc = liveDoctors.First();
                result.AiAdviceEn += $"\n\n🔴 **Dr. {doc.DoctorName}** ({doc.Specialty}) is currently **LIVE NOW**! You can connect with them directly for live advice or video consultation.";
            }
            else
            {
                result.AiAdviceEn += "\n\nIf you have any health-related questions or wish to consult a specialist doctor, feel free to describe your symptoms anytime!";
            }
        }
        else
        {
            if (liveDoctors != null && liveDoctors.Any())
            {
                var matchingDoc = liveDoctors.FirstOrDefault(d => d.Department.Equals(result.IdentifiedDepartment, StringComparison.OrdinalIgnoreCase)) ?? liveDoctors.First();
                result.AiAdviceEn += $"\n\n💡 **Live Specialist Available:** Dr. {matchingDoc.DoctorName} ({matchingDoc.Specialty}) is broadcasting live right now. You can join their stream or request a 1-to-1 video consultation.";
            }
        }
    }

    private async Task<AiDiagnosisResultDto?> CallLlmApiAsync(string query, string provider, string apiKey, string model)
    {
        string prompt = $@"You are CYHDocGuru's AI Clinical & Health Assistant. The user has sent this message: ""{query}"".
Your instructions:
1. Determine if this message is MEDICAL / HEALTH / SYMPTOM / MEDICINE / DOCTOR related.
   - If user asks about greetings ('hi', 'hello', 'namaste'), acknowledge warmly as CYHDoc AI Health Assistant and ask how you can help with their health or symptoms today. Set isMedical to true.
   - If user asks about NON-MEDICAL topics (e.g. politics, coding, cricket, sports, general trivia, movies, weather, jokes, finance, etc.), set isMedical to false, and in 'advice', reply with a polite apology: ""I am sorry, but as CYHDocGuru's dedicated Medical & Health AI Assistant, I can only assist with medical, symptoms, health, medicine, and doctor consultation inquiries. I cannot assist with non-health topics like '{query}'.""
   - If user asks about MEDICAL topics (symptoms, pain, illness, medicine dosage, health precautions, first aid), set isMedical to true, and provide clear, empathetic, clinically accurate advice with precautions and guidance on when to consult a doctor.
2. If isMedical is true, categorize department, specialty, severity (Low|Moderate|High), and isEmergency (true if chest pain/stroke/severe bleeding/unconsciousness, else false).

Respond in strictly valid JSON with this schema without markdown fences:
{{
  ""isMedical"": true|false,
  ""department"": ""Cardiology|Dermatology|Neurology|Orthopedics|Pediatrics|Gynecology|General Medicine"",
  ""specialty"": ""Doctor Specialty Title"",
  ""severity"": ""Low|Moderate|High"",
  ""isEmergency"": true|false,
  ""advice"": ""Detailed medical response or polite non-medical apology"",
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
            string clean = jsonText.Trim();
            if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(7);
            if (clean.StartsWith("```")) clean = clean.Substring(3);
            if (clean.EndsWith("```")) clean = clean.Substring(0, clean.Length - 3);
            clean = clean.Trim();

            using var doc = JsonDocument.Parse(clean);
            var root = doc.RootElement;

            bool isMedical = true;
            if (root.TryGetProperty("isMedical", out var medProp))
            {
                isMedical = medProp.GetBoolean();
            }

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
                IsMedical = isMedical,
                IdentifiedDepartment = root.TryGetProperty("department", out var dProp) ? (dProp.GetString() ?? "General Medicine") : "General Medicine",
                SpecialtyNeeded = root.TryGetProperty("specialty", out var sProp) ? (sProp.GetString() ?? "Physician") : "Physician",
                Severity = root.TryGetProperty("severity", out var svProp) ? (svProp.GetString() ?? "Moderate") : "Moderate",
                IsEmergency = root.TryGetProperty("isEmergency", out var emProp) && emProp.GetBoolean(),
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

    private static readonly string[] GreetingKeywords = new[]
    {
        "hi", "hello", "hey", "namaste", "good morning", "good evening", "good afternoon", "who are you", "what can you do", "help"
    };

    private static readonly string[] MedicalKeywords = new[]
    {
        "fever", "headache", "cough", "cold", "pain", "chest", "heart", "bp", "blood pressure", "hypertension",
        "skin", "rash", "itching", "itch", "allergy", "acne", "pimple", "eczema", "dermatology",
        "bone", "joint", "knee", "back", "spine", "fracture", "sprain", "orthopedic", "arthritis", "ligament",
        "child", "baby", "infant", "pediatric", "teething", "vaccine", "vaccination",
        "pregnant", "pregnancy", "period", "periods", "menstrual", "gynecology", "pcod", "pcos", "cramp",
        "stomach", "abdomen", "belly", "gastric", "acidity", "gas", "vomit", "vomiting", "nausea", "diarrhea", "loose motion", "constipation",
        "dizziness", "dizzy", "vertigo", "migraine", "nerve", "neurology", "seizure", "paralysis", "stroke", "numbness",
        "eye", "ear", "nose", "throat", "ent", "tonsil", "sinus", "hearing", "vision",
        "teeth", "tooth", "gum", "dental", "dentist", "mouth ulcer",
        "diabetes", "sugar", "insulin", "thyroid", "cholesterol", "uric acid", "kidney", "urine", "liver", "jaundice", "hepatitis",
        "infection", "viral", "bacterial", "fungus", "fungal", "wound", "cut", "burn", "swelling", "bleeding", "pus",
        "breath", "shortness of breath", "asthma", "lungs", "pneumonia", "bronchitis", "wheezing",
        "cancer", "tumor", "lump", "biopsy", "chemotherapy",
        "medicine", "tablet", "capsule", "syrup", "dosage", "dose", "antibiotic", "paracetamol", "prescription", "side effect",
        "doctor", "specialist", "hospital", "clinic", "consultation", "triage", "symptom", "disease", "illness", "treatment", "therapy",
        "diet", "nutrition", "weight loss", "weight gain", "obesity", "fitness", "vitamin", "calcium", "iron", "protein",
        "anxiety", "depression", "stress", "mental health", "sleep", "insomnia", "panic", "fatigue", "weakness"
    };

    private async Task<AiDiagnosisResultDto> ExecuteRuleEngineAsync(string query, List<DoctorReelDto> liveDoctors)
    {
        var result = new AiDiagnosisResultDto { QueryText = query.Trim() };
        string q = query.ToLowerInvariant();

        // A. Greetings
        if (ContainsAny(q, GreetingKeywords) && q.Length <= 30)
        {
            result.IsMedical = true;
            result.IdentifiedDepartment = "General Medicine";
            result.SpecialtyNeeded = "General Physician";
            result.Severity = "Low";
            result.AiAdviceEn = "Namaste! 🙏 I am your CYHDoc AI Health Assistant (Powered by Gemini AI). I am here to help you evaluate symptoms, provide general medical information, and connect you with verified specialist doctors.\n\nPlease describe what symptoms or health questions you have today.";
            AttachLiveDoctorNotice(result, liveDoctors);
            result.RecommendedDoctors = (await _doctorRepo.SearchDoctorsAsync("General Medicine")).ToList();
            return result;
        }

        // B. Non-Medical Query Check
        if (!ContainsAny(q, MedicalKeywords))
        {
            result.IsMedical = false;
            result.IdentifiedDepartment = "Non-Medical";
            result.SpecialtyNeeded = "General Physician";
            result.Severity = "Low";
            result.AiAdviceEn = $"I am sorry, but as CYHDocGuru's dedicated Medical & Health AI Assistant, I can only assist with medical, symptoms, health, medicine, and doctor consultation inquiries. I cannot assist with non-health topics like \"{query}\".";
            AttachLiveDoctorNotice(result, liveDoctors);
            return result;
        }

        // C. Medical Query Categorization
        result.IsMedical = true;

        // 1. Cardiology (Heart, BP)
        if (ContainsAny(q, "chest pain", "heart", "cardio", "chest", "palpitation", "high bp", "hypertension", "bp", "blood pressure", "shortness of breath", "cholesterol"))
        {
            result.IdentifiedDepartment = "Cardiology";
            result.SpecialtyNeeded = "Cardiologist / Heart Specialist";
            result.Severity = ContainsAny(q, "severe", "pain", "sudden", "left arm", "collapse") ? "High" : "Moderate";
            result.IsEmergency = ContainsAny(q, "severe chest", "left arm", "collapse", "breathless", "heart attack");
            result.MatchedKeywords.Add("Cardiac / Chest Sensation");
            result.AiAdviceEn = "Chest or heart-related discomfort requires immediate professional medical attention. Monitor your pulse and blood pressure, avoid physical exertion, and seek medical care promptly.";
        }
        // 2. Dermatology (Skin, Allergy)
        else if (ContainsAny(q, "skin", "rash", "itching", "itch", "allergy", "acne", "pimple", "eczema", "fungal", "fungus", "lesion"))
        {
            result.IdentifiedDepartment = "Dermatology";
            result.SpecialtyNeeded = "Dermatologist / Skin Specialist";
            result.Severity = "Low";
            result.MatchedKeywords.Add("Dermatological / Skin condition");
            result.AiAdviceEn = "Skin conditions benefit from direct visual consultation with a dermatologist. Keep the affected area clean and dry, avoid scratching, and do not apply unprescribed steroid creams.";
        }
        // 3. Neurology (Brain, Nerves, Headaches)
        else if (ContainsAny(q, "headache", "migraine", "dizziness", "dizzy", "vertigo", "seizure", "numbness", "nerve", "stroke", "paralysis"))
        {
            result.IdentifiedDepartment = "Neurology";
            result.SpecialtyNeeded = "Neurologist";
            result.Severity = ContainsAny(q, "sudden", "blackout", "vomiting", "seizure", "stroke", "paralysis") ? "High" : "Moderate";
            result.IsEmergency = ContainsAny(q, "blackout", "seizure", "paralysis", "stroke");
            result.MatchedKeywords.Add("Neurological / Migraine symptoms");
            result.AiAdviceEn = "Frequent headaches, migraines, or dizziness require proper neurological assessment. Rest in a dark quiet room, maintain hydration, and avoid screen strain.";
        }
        // 4. Orthopedics (Bones, Joints, Spine)
        else if (ContainsAny(q, "bone", "joint", "knee", "back pain", "back", "spine", "fracture", "ligament", "sprain", "arthritis"))
        {
            result.IdentifiedDepartment = "Orthopedics";
            result.SpecialtyNeeded = "Orthopedic Specialist / Bone Surgeon";
            result.Severity = ContainsAny(q, "fracture", "swollen", "unable to walk", "severe") ? "High" : "Moderate";
            result.MatchedKeywords.Add("Musculoskeletal / Orthopedic pain");
            result.AiAdviceEn = "Bone, knee, or spine pains should be evaluated to prevent joint degeneration. Practice R.I.C.E. (Rest, Ice, Compression, Elevation) and avoid heavy lifting.";
        }
        // 5. Pediatrics (Children)
        else if (ContainsAny(q, "child", "baby", "pediatric", "infant", "newborn", "teething", "vaccination", "vaccine"))
        {
            result.IdentifiedDepartment = "Pediatrics";
            result.SpecialtyNeeded = "Pediatrician / Child Specialist";
            result.Severity = ContainsAny(q, "high fever", "breathing", "vomiting", "seizure") ? "High" : "Moderate";
            result.MatchedKeywords.Add("Pediatric / Child care");
            result.AiAdviceEn = "Children's health requires delicate and timely pediatric guidance. Ensure sufficient hydration, monitor body temperature frequently, and do not administer adult medications.";
        }
        // 6. Gynecology (Women Health)
        else if (ContainsAny(q, "pregnancy", "pregnant", "period", "periods", "gynec", "female", "menstrual", "pcod", "pcos", "cramp"))
        {
            result.IdentifiedDepartment = "Gynecology";
            result.SpecialtyNeeded = "Gynecologist / Women's Specialist";
            result.Severity = "Moderate";
            result.MatchedKeywords.Add("Gynecological / Women health");
            result.AiAdviceEn = "Women's wellness and reproductive health require personalized consultation with a certified gynecologist. Track your cycle and consult regarding any irregular bleeding or intense cramps.";
        }
        // 7. Gastroenterology (Stomach, Digestion)
        else if (ContainsAny(q, "stomach", "abdomen", "belly", "gastric", "acidity", "gas", "vomit", "vomiting", "nausea", "diarrhea", "loose motion", "constipation"))
        {
            result.IdentifiedDepartment = "Gastroenterology";
            result.SpecialtyNeeded = "Gastroenterologist / Physician";
            result.Severity = ContainsAny(q, "blood", "severe", "dehydration") ? "High" : "Moderate";
            result.MatchedKeywords.Add("Gastrointestinal / Digestive health");
            result.AiAdviceEn = "Digestive discomfort or gastrointestinal symptoms should be managed with adequate hydration (ORS/fluids), bland diet, and evaluation by a physician if symptoms persist beyond 24 hours.";
        }
        // 8. ENT (Ear, Nose, Throat)
        else if (ContainsAny(q, "throat", "ear", "tonsil", "sinus", "nose", "ent", "hearing", "earache"))
        {
            result.IdentifiedDepartment = "ENT";
            result.SpecialtyNeeded = "ENT Specialist";
            result.Severity = "Low";
            result.MatchedKeywords.Add("ENT / Throat & Ear symptoms");
            result.AiAdviceEn = "Throat, ear, or sinus discomfort can be soothed with warm salt-water gargles, steam inhalation, and proper rest. Consult an ENT specialist if pain or fever worsens.";
        }
        // 9. Dental (Teeth, Gums)
        else if (ContainsAny(q, "teeth", "tooth", "gum", "dental", "dentist", "mouth ulcer", "cavity"))
        {
            result.IdentifiedDepartment = "Dental";
            result.SpecialtyNeeded = "Dentist / Dental Surgeon";
            result.Severity = "Low";
            result.MatchedKeywords.Add("Dental / Oral symptoms");
            result.AiAdviceEn = "Dental and gum issues require in-person dental examination. Rinse with warm salt water and avoid very hot, cold, or sugary foods until examined.";
        }
        // 10. General Medicine
        else
        {
            result.IdentifiedDepartment = "General Medicine";
            result.SpecialtyNeeded = "General Physician / MD Medicine";
            result.Severity = "Low";
            result.MatchedKeywords.Add("General constitutional symptoms");
            result.AiAdviceEn = "Consult a General Physician for comprehensive diagnosis, blood tests, and personalized care. Stay hydrated and monitor your symptoms.";
        }

        AttachLiveDoctorNotice(result, liveDoctors);

        // Fetch doctors
        result.RecommendedDoctors = (await _doctorRepo.SearchDoctorsAsync(result.IdentifiedDepartment)).ToList();
        return result;
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        return values.Any(v => source.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
