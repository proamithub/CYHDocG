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

    public async Task<AiDiagnosisResultDto> AnalyzeSymptomAsync(string query, List<ChatTurnDto>? history = null)
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
                var llmResult = await CallLlmApiAsync(query, history, provider, apiKey, model);
                if (llmResult != null)
                {
                    llmResult.LiveDoctors = liveDoctors;
                    if (llmResult.IsMedical)
                    {
                        llmResult.RecommendedDoctors = (await _doctorRepo.SearchDoctorsAsync(llmResult.IdentifiedDepartment)).ToList();
                        AttachLiveDoctorNotice(llmResult, liveDoctors);
                    }
                    return llmResult;
                }
            }
            catch
            {
                // Fallback to rule engine if LLM fails
            }
        }

        // Local Rule Engine Fallback (Instant, 100% Reliable, Pure English)
        var ruleResult = await ExecuteRuleEngineAsync(query, liveDoctors, history);
        ruleResult.LiveDoctors = liveDoctors;
        return ruleResult;
    }

    private static void AttachLiveDoctorNotice(AiDiagnosisResultDto result, List<DoctorReelDto> liveDoctors)
    {
        if (!result.IsMedical)
        {
            return;
        }

        if (liveDoctors != null && liveDoctors.Any())
        {
            var matchingDoc = liveDoctors.FirstOrDefault(d => d.Department.Equals(result.IdentifiedDepartment, StringComparison.OrdinalIgnoreCase)) ?? liveDoctors.First();
            result.AiAdviceEn += $"\n\n💡 **Live Specialist Available:** Dr. {matchingDoc.DoctorName} ({matchingDoc.Specialty}) is broadcasting live right now. You can join their stream or request a 1-to-1 video consultation.";
        }
    }

    private async Task<AiDiagnosisResultDto?> CallLlmApiAsync(string query, List<ChatTurnDto>? history, string provider, string apiKey, string model)
    {
        StringBuilder historyContext = new();
        if (history != null && history.Any())
        {
            historyContext.AppendLine("Previous Conversation Turns:");
            foreach (var turn in history.TakeLast(6))
            {
                string speaker = turn.Role?.ToLowerInvariant() == "user" ? "Patient" : "Doctor AI";
                if (!string.IsNullOrWhiteSpace(turn.Content))
                {
                    historyContext.AppendLine($"{speaker}: {turn.Content.Trim()}");
                }
            }
            historyContext.AppendLine();
        }

        string prompt = $@"You are CYHDocGuru's Senior AI Clinical Triage & Health Assistant. You converse empathetically in the SAME LANGUAGE as the patient (Hindi, Hinglish, or English).

{historyContext}Current Patient Message: ""{query}""

Strict Clinical Triage Instructions:
1. Intent Classification:
   - NON-MEDICAL: If patient asks about cooking/recipes (e.g. 'मुझे खाना बनाना है', food recipes), cricket/sports, movies, coding, tech, weather, jokes, or non-health topics, set isMedical to false, and advice to """" (empty string).
   - GREETING: If patient says 'hi', 'hello', 'namaste', greet warmly as CYHDoc Medical AI and ask what health symptom they have. Set isMedical: true.
   - MEDICAL: If patient mentions ANY pain, symptom, illness, medicine, or health issue (e.g. 'मुझे दर्द है', 'पेट में दर्द', 'बुखार', 'fever', 'headache'): Set isMedical: true.

2. Dynamic Clinical Consultancy (NOT static, deeply personalized to what patient said):
   - A) If patient's symptom is VAGUE or INCOMPLETE (e.g. 'मुझे दर्द है', 'I feel pain', 'tabiyat kharab hai', 'pet me kuch ho raha hai'):
     Set isNeedsMoreInfo: true.
     Act like an attentive physician! In 'advice' and 'followUpQuestions', ask 2-3 specific clarifying questions:
     - Exact location (दर्द कहाँ हो रहा है? जैसे सिर, पेट, छाती, जोड़)
     - Severity (दर्द कितना तेज है? हल्का, मध्यम, या बहुत तेज?)
     - Duration & Nature (कब से हो रहा है? जलन, मरोड़ या भारीपन?)
     - Any other symptoms (बुखार, उल्टी, चक्कर?)
     Still provide general soothing words.
   
   - B) If patient has specified symptom location/details (e.g. 'पेट में दर्द है', '3 दिन से बुखार है', 'सिर दर्द'):
     Set isNeedsMoreInfo: false.
     Provide a comprehensive clinical response in the user's language:
     1. Empathetic explanation of what could be causing it.
     2. Specific, safe Home Remedies (घरेलू उपचार) in 'homeRemedies' array (e.g. hydration, warm compress, ajwain water for stomach gas, steam for cold, rest, light diet).
     3. Red flag warnings: When to immediately seek medical care.
     4. Clear reminder that while home remedies help with temporary comfort, an in-person or live video evaluation with a certified doctor is vital.

3. Department & Doctor Specialty:
   - Categorize department accurately: Cardiology | Dermatology | Neurology | Orthopedics | Pediatrics | Gynecology | Gastroenterology | ENT | General Medicine
   - Specialty: e.g. 'General Physician', 'Gastroenterologist', 'Cardiologist', 'Neurologist', etc.
   - Severity: Low | Moderate | High | Emergency
   - isEmergency: true if severe chest pain, stroke signs, breathing crisis, unconsciousness, severe bleeding.

Respond ONLY in valid JSON matching this schema without markdown fences:
{{
  ""isMedical"": true|false,
  ""isNeedsMoreInfo"": true|false,
  ""department"": ""Department Name"",
  ""specialty"": ""Specialist Title"",
  ""severity"": ""Low|Moderate|High|Emergency"",
  ""isEmergency"": true|false,
  ""followUpQuestions"": [""Question 1"", ""Question 2""],
  ""homeRemedies"": [""Remedy 1"", ""Remedy 2""],
  ""advice"": ""Detailed empathetic clinical response in patient's language"",
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

            if (!isMedical)
            {
                return new AiDiagnosisResultDto
                {
                    QueryText = query,
                    IsMedical = false,
                    IdentifiedDepartment = "Non-Medical",
                    SpecialtyNeeded = "General Physician",
                    Severity = "Low",
                    IsEmergency = false,
                    AiAdviceEn = string.Empty
                };
            }

            bool isNeedsMoreInfo = false;
            if (root.TryGetProperty("isNeedsMoreInfo", out var nmiProp))
            {
                isNeedsMoreInfo = nmiProp.GetBoolean();
            }

            var res = new AiDiagnosisResultDto
            {
                QueryText = query,
                IsMedical = true,
                IsNeedsMoreInfo = isNeedsMoreInfo,
                IdentifiedDepartment = root.TryGetProperty("department", out var dProp) ? (dProp.GetString() ?? "General Medicine") : "General Medicine",
                SpecialtyNeeded = root.TryGetProperty("specialty", out var sProp) ? (sProp.GetString() ?? "Physician") : "Physician",
                Severity = root.TryGetProperty("severity", out var svProp) ? (svProp.GetString() ?? "Moderate") : "Moderate",
                IsEmergency = root.TryGetProperty("isEmergency", out var emProp) && emProp.GetBoolean(),
                AiAdviceEn = advice
            };

            if (root.TryGetProperty("followUpQuestions", out var fqElem) && fqElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var qItem in fqElem.EnumerateArray())
                {
                    string s = qItem.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(s)) res.FollowUpQuestions.Add(s);
                }
            }

            if (root.TryGetProperty("homeRemedies", out var hrElem) && hrElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var rItem in hrElem.EnumerateArray())
                {
                    string s = rItem.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(s)) res.HomeRemedies.Add(s);
                }
            }

            if (root.TryGetProperty("keywords", out var kwElem) && kwElem.ValueKind == JsonValueKind.Array)
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
        "hi", "hello", "hey", "namaste", "good morning", "good evening", "good afternoon", "who are you", "what can you do", "help", "नमस्ते", "हेलो"
    };

    private static readonly string[] NonMedicalKeywords = new[]
    {
        "khana", "khana banana", "cooking", "recipe", "banaana", "banaye", "roti", "sabzi", "biryani", "dal", "chawal", "curry", "dish",
        "cricket", "ipl", "match", "movie", "film", "cinema", "gaana", "gaane", "song", "music", "weather", "mausam", "joke", "chutkula",
        "python", "code", "coding", "software", "program", "game", "gaming", "dance", "car", "bike", "travel", "flight", "politics", "neta",
        "खाना", "बनाना", "रेसिपी", "फिल्म", "मूवी", "गाना", "मौसम", "क्रिकेट", "कोडिंग"
    };

    private static readonly string[] MedicalKeywords = new[]
    {
        "fever", "bukhar", "bukhaar", "headache", "sar dard", "sardard", "sir dard", "cough", "khansi", "cold", "zukam", "nazla",
        "pain", "dard", "chest", "seene me dard", "seene mein dard", "heart", "bp", "blood pressure", "hypertension",
        "skin", "rash", "itching", "khujli", "itch", "allergy", "acne", "pimple", "eczema", "dermatology",
        "bone", "joint", "haddi", "knee", "ghutna", "back", "kamar", "spine", "fracture", "sprain", "orthopedic", "arthritis", "ligament",
        "child", "baccha", "baby", "infant", "pediatric", "teething", "vaccine", "vaccination",
        "pregnant", "pregnancy", "period", "periods", "mahina", "menstrual", "gynecology", "pcod", "pcos", "cramp",
        "stomach", "pet", "pet dard", "pet me dard", "pet mein dard", "pet kharab", "abdomen", "belly", "gastric", "acidity", "gas", "vomit", "vomiting", "ulti", "nausea", "diarrhea", "dast", "loose motion", "constipation", "kabz",
        "dizziness", "chakkar", "dizzy", "vertigo", "migraine", "nerve", "neurology", "seizure", "paralysis", "stroke", "numbness",
        "eye", "aankh", "ear", "kaan", "nose", "naak", "throat", "gala", "gala kharab", "ent", "tonsil", "sinus", "hearing", "vision",
        "teeth", "tooth", "daant", "gum", "dental", "dentist", "mouth ulcer",
        "diabetes", "sugar", "insulin", "thyroid", "cholesterol", "uric acid", "kidney", "urine", "peshab", "liver", "jaundice", "piliya", "hepatitis",
        "infection", "viral", "bacterial", "fungus", "fungal", "wound", "cut", "chot", "burn", "swelling", "sujan", "bleeding", "khoon", "pus",
        "breath", "saans", "saans lene me taklif", "shortness of breath", "asthma", "lungs", "pneumonia", "bronchitis", "wheezing",
        "cancer", "tumor", "lump", "biopsy", "chemotherapy",
        "medicine", "dawa", "dawai", "tablet", "goli", "capsule", "syrup", "dosage", "dose", "antibiotic", "paracetamol", "prescription", "side effect",
        "doctor", "specialist", "hospital", "clinic", "consultation", "triage", "symptom", "disease", "illness", "treatment", "therapy", "swasthya", "ilaj", "mariz", "bimar",
        "diet", "nutrition", "weight loss", "weight gain", "obesity", "fitness", "vitamin", "calcium", "iron", "protein",
        "anxiety", "depression", "stress", "mental health", "sleep", "insomnia", "panic", "fatigue", "weakness", "kamzori",
        "बुखार", "दर्द", "पेट", "सिर", "खांसी", "जुकाम", "उल्टी", "दस्त", "चक्कर", "दवा", "दवाई", "डॉक्टर", "इलाज", "बीमारी", "स्वास्थ्य", "छाती", "हार्ट", "चोट", "सूजन", "खून", "कमर", "हड्डी", "आंख", "कान", "गला", "दांत", "सांस"
    };

    private async Task<AiDiagnosisResultDto> ExecuteRuleEngineAsync(string query, List<DoctorReelDto> liveDoctors, List<ChatTurnDto>? history)
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
        if (ContainsAny(q, NonMedicalKeywords) || !ContainsAny(q, MedicalKeywords))
        {
            result.IsMedical = false;
            result.IdentifiedDepartment = "Non-Medical";
            result.SpecialtyNeeded = "General Physician";
            result.Severity = "Low";
            result.AiAdviceEn = string.Empty;
            return result;
        }

        // C. Medical Query Categorization
        result.IsMedical = true;

        // Check if query is vague (e.g. "mujhe dard hai", "dard", "pain", "tabiyat kharab")
        bool isVague = (ContainsAny(q, "dard", "pain", "दर्द") && !ContainsAny(q, "pet", "stomach", "sir", "sar", "head", "chest", "seene", "kamar", "back", "joint", "haddi", "gala"))
                    || q.Equals("mujhe dard hai", StringComparison.OrdinalIgnoreCase)
                    || q.Equals("dard ho raha hai", StringComparison.OrdinalIgnoreCase)
                    || q.Equals("tabiyat kharab hai", StringComparison.OrdinalIgnoreCase)
                    || q.Length <= 15;

        if (isVague)
        {
            result.IsNeedsMoreInfo = true;
            result.IdentifiedDepartment = "General Medicine";
            result.SpecialtyNeeded = "General Physician";
            result.Severity = "Moderate";
            result.AiAdviceEn = "दर्द होने की स्थिति में शरीर को आराम देना जरूरी है। आपको सटीक क्लिनिकल सलाह और उचित घरेलू उपचार सुझाने के लिए, कृपया नीचे दिए गए प्रश्नों के उत्तर दें:";
            result.FollowUpQuestions.Add("दर्द शरीर के किस हिस्से में हो रहा है? (जैसे सिर, पेट, छाती, कमर या जोड़)");
            result.FollowUpQuestions.Add("दर्द कितना तेज है? (हल्का, मध्यम या बहुत तेज)");
            result.FollowUpQuestions.Add("यह दर्द कब से है और क्या साथ में बुखार, उल्टी या चक्कर भी है?");
            result.HomeRemedies.Add("आरामदायक मुद्रा में विश्राम करें और अचानक कोई भारी गतिविधि न करें।");
            result.HomeRemedies.Add("पर्याप्त गुनगुना पानी पिएं।");
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
