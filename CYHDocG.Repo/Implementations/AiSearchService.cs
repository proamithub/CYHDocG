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

        // 1. Gastroenterology (Stomach, Pet, Gas, Acidity, Vomiting)
        if (ContainsAny(q, "stomach", "pet", "pet dard", "pet me dard", "pet mein dard", "pet kharab", "abdomen", "belly", "gastric", "acidity", "gas", "vomit", "vomiting", "ulti", "nausea", "diarrhea", "dast", "loose motion", "constipation", "kabz", "मरोड़", "पेट", "उल्टी", "दस्त", "कब्ज"))
        {
            result.IdentifiedDepartment = "Gastroenterology";
            result.SpecialtyNeeded = "Gastroenterologist / Physician";
            result.Severity = ContainsAny(q, "blood", "severe", "dehydration", "khoon", "asahania") ? "High" : "Moderate";
            result.MatchedKeywords.Add("Gastrointestinal / Stomach pain");
            result.AiAdviceEn = "पेट में दर्द, गैस, मरोड़ या अपच पाचन तंत्र में गड़बड़ी के आम लक्षण हैं। भारी या तला-भुना भोजन बिल्कुल न लें और शरीर को पर्याप्त आराम दें।";
            result.HomeRemedies.Add("हल्का गुनगुना पानी पिएं और भारी या ऑयली खाना पूरी तरह बंद रखें।");
            result.HomeRemedies.Add("अजवाइन और काला नमक का गुनगुना पानी या सौंफ का पानी गैस व मरोड़ में तुरंत आराम देता है।");
            result.HomeRemedies.Add("पेट पर गर्म पानी की थैली (hot water bag) से हल्की सिकाई करें और बाईं करवट (left side) लेटें।");
            result.HomeRemedies.Add("उल्टी या दस्त की स्थिति में ORS का घोल या नारियल पानी घूंट-घूंट करके पिएं।");
        }
        // 2. Neurology (Headache, Sir Dard, Migraine, Chakkar)
        else if (ContainsAny(q, "headache", "migraine", "dizziness", "dizzy", "vertigo", "seizure", "numbness", "nerve", "stroke", "paralysis", "sir dard", "sar dard", "sardard", "sirdard", "chakkar", "सिर", "सिरदर्द", "चक्कर"))
        {
            result.IdentifiedDepartment = "Neurology";
            result.SpecialtyNeeded = "Neurologist / Physician";
            result.Severity = ContainsAny(q, "sudden", "blackout", "vomiting", "seizure", "stroke", "paralysis") ? "High" : "Moderate";
            result.IsEmergency = ContainsAny(q, "blackout", "seizure", "paralysis", "stroke");
            result.MatchedKeywords.Add("Neurological / Headache symptoms");
            result.AiAdviceEn = "सिरदर्द या चक्कर आना अत्यधिक तनाव, नींद की कमी, डिहाइड्रेशन या माइग्रेन की वजह से हो सकता है। आंखों पर रोशनी का तनाव कम करें और शांत रहें।";
            result.HomeRemedies.Add("शांत और अंधेरे कमरे में लेटकर गहरी सांसें लें और आराम करें।");
            result.HomeRemedies.Add("माथे और गर्दन के पीछे ठंडे या सामान्य पानी की पट्टी रखें।");
            result.HomeRemedies.Add("मोबाइल, टीवी और लैपटॉप की स्क्रीन से पूरी तरह दूरी बनाएं।");
            result.HomeRemedies.Add("भरपूर मात्रा में पानी पिएं और अदरक या पुदीने की हल्की गुनगुनी चाय लें।");
        }
        // 3. Cardiology (Chest, Seene Me Dard, Heart, BP)
        else if (ContainsAny(q, "chest pain", "heart", "cardio", "chest", "palpitation", "high bp", "hypertension", "bp", "blood pressure", "shortness of breath", "cholesterol", "seene me dard", "seene mein dard", "chhati", "छाती", "हार्ट", "दिल"))
        {
            result.IdentifiedDepartment = "Cardiology";
            result.SpecialtyNeeded = "Cardiologist / Heart Specialist";
            result.Severity = ContainsAny(q, "severe", "pain", "sudden", "left arm", "collapse", "tez") ? "High" : "Moderate";
            result.IsEmergency = ContainsAny(q, "severe chest", "left arm", "collapse", "breathless", "heart attack", "chhati me dard");
            result.MatchedKeywords.Add("Cardiac / Chest Sensation");
            result.AiAdviceEn = "सीने में दर्द या भारीपन को कभी नज़रअंदाज़ नहीं करना चाहिए। तुरंत किसी भी शारीरिक गतिविधि को रोकें और आराम से बैठें।";
            result.HomeRemedies.Add("तुरंत आरामदायक स्थिति में बैठ जाएं, कपड़े ढीले करें और गहरी, शांत सांसें लें।");
            result.HomeRemedies.Add("किसी भी प्रकार का तनाव या शारीरिक परिश्रम बिल्कुल न करें।");
            result.HomeRemedies.Add("⚠️ चेतावनी: यदि दर्द बाएँ हाथ, जबड़े में फैले या पसीना/सांस फूलने लगे तो तुरंत आपातकालीन एम्बुलेंस (108/112) बुलाएं।");
        }
        // 4. Orthopedics (Bone, Joint, Knee, Back, Kamar Dard)
        else if (ContainsAny(q, "bone", "joint", "knee", "back pain", "back", "spine", "fracture", "ligament", "sprain", "arthritis", "kamar", "kamar dard", "ghutna", "haddi", "कमर", "हड्डी", "जोड़", "घुटना"))
        {
            result.IdentifiedDepartment = "Orthopedics";
            result.SpecialtyNeeded = "Orthopedic Specialist / Bone Surgeon";
            result.Severity = ContainsAny(q, "fracture", "swollen", "unable to walk", "severe") ? "High" : "Moderate";
            result.MatchedKeywords.Add("Musculoskeletal / Orthopedic pain");
            result.AiAdviceEn = "कमर, घुटने या जोड़ों का दर्द मांसपेशियों में खिंचाव, गलत पोस्चर या आर्थराइटिस के कारण हो सकता है। झुककर भारी सामान न उठाएं।";
            result.HomeRemedies.Add("R.I.C.E. तकनीक अपनाएं: प्रभावित हिस्से को आराम दें और उस पर ज्यादा दबाव न डालें।");
            result.HomeRemedies.Add("गर्म पानी की सिकाई या दर्दनिवारक आयुर्वेदिक तेल से हल्के हाथों से मालिश करें।");
            result.HomeRemedies.Add("सख्त सतह या उपयुक्त गद्दे पर सोएं और बैठते समय पीठ को सीधा रखें।");
        }
        // 5. ENT (Throat, Gala, Khansi, Cold, Zukam, Ear)
        else if (ContainsAny(q, "throat", "ear", "tonsil", "sinus", "nose", "ent", "hearing", "earache", "gala", "gala kharab", "khansi", "cough", "cold", "zukam", "nazla", "गला", "खांसी", "जुकाम", "कान"))
        {
            result.IdentifiedDepartment = "ENT";
            result.SpecialtyNeeded = "ENT Specialist / Physician";
            result.Severity = "Low";
            result.MatchedKeywords.Add("ENT / Throat & Ear symptoms");
            result.AiAdviceEn = "गले में खराश, खांसी या जुकाम मौसमी संक्रमण या एलर्जी हो सकता है। गर्म तरल पदार्थों का सेवन करें और ठंडी हवा से बचें।";
            result.HomeRemedies.Add("गुनगुने पानी में थोड़ा सेंधा नमक डालकर दिन में 2-3 बार गरारे करें।");
            result.HomeRemedies.Add("अजवाइन या विक्स डालकर गर्म पानी की भाप (steam) लें।");
            result.HomeRemedies.Add("अदरक, तुलसी, काली मिर्च और शहद का काढ़ा या गुनगुनी चाय पिएं।");
            result.HomeRemedies.Add("फ्रिज का ठंडा पानी और तैलीय खट्टे खाद्य पदार्थों से पूरी तरह परहेज़ करें।");
        }
        // 6. Dermatology (Skin, Allergy, Khujli)
        else if (ContainsAny(q, "skin", "rash", "itching", "itch", "allergy", "acne", "pimple", "eczema", "fungal", "fungus", "lesion", "khujli", "chaale", "दाद", "खुजली", "त्वचा"))
        {
            result.IdentifiedDepartment = "Dermatology";
            result.SpecialtyNeeded = "Dermatologist / Skin Specialist";
            result.Severity = "Low";
            result.MatchedKeywords.Add("Dermatological / Skin condition");
            result.AiAdviceEn = "त्वचा पर खुजली, दाने या लाल चकत्ते एलर्जी या फंगल इन्फेक्शन हो सकते हैं। प्रभावित जगह को खरोंचें नहीं।";
            result.HomeRemedies.Add("प्रभावित हिस्से को ठंडे पानी से साफ करें और सूती तौलिए से थपथपाकर सुखाएं।");
            result.HomeRemedies.Add("नारियल तेल या एलोवेरा जेल लगाएं ताकि जलन और खुजली शांत हो सके।");
            result.HomeRemedies.Add("ढीले और सूती कपड़े पहनें, बिना डॉक्टर की सलाह के कोई स्टेरॉयड क्रीम न लगाएं।");
        }
        // 7. Pediatrics (Child, Baccha)
        else if (ContainsAny(q, "child", "baby", "pediatric", "infant", "newborn", "teething", "vaccination", "vaccine", "baccha", "chota baccha", "बच्चा"))
        {
            result.IdentifiedDepartment = "Pediatrics";
            result.SpecialtyNeeded = "Pediatrician / Child Specialist";
            result.Severity = ContainsAny(q, "high fever", "breathing", "vomiting", "seizure") ? "High" : "Moderate";
            result.MatchedKeywords.Add("Pediatric / Child care");
            result.AiAdviceEn = "बच्चों की स्वास्थ्य समस्याओं में विशेष देखभाल की आवश्यकता होती है। बिना विशेषज्ञ की सलाह के बच्चों को कोई भी वयस्क दवा न दें।";
            result.HomeRemedies.Add("बच्चे को पर्याप्त स्तनपान या तरल पदार्थ देकर हाइड्रेटेड रखें।");
            result.HomeRemedies.Add("बुखार की स्थिति में बच्चे के माथे और पैरों पर सामान्य पानी की पट्टी रखें।");
        }
        // 8. Gynecology (Women Health)
        else if (ContainsAny(q, "pregnancy", "pregnant", "period", "periods", "gynec", "female", "menstrual", "pcod", "pcos", "cramp", "mahina", "garbh", "महिला"))
        {
            result.IdentifiedDepartment = "Gynecology";
            result.SpecialtyNeeded = "Gynecologist / Women's Specialist";
            result.Severity = "Moderate";
            result.MatchedKeywords.Add("Gynecological / Women health");
            result.AiAdviceEn = "मासिक धर्म में ऐंठन या महिलाओं से जुड़े लक्षणों में शरीर को विश्राम दें। गंभीर दर्द होने पर विशेषज्ञ से सलाह लें।";
            result.HomeRemedies.Add("निचले पेट पर गर्म पानी की थैली (hot water bottle) से सिकाई करें।");
            result.HomeRemedies.Add("हल्का गुनगुना पानी और हर्बल चाय पिएं, तनाव से बचें।");
        }
        // 9. General Medicine & Fever (Bukhar, Weakness, Infection)
        else
        {
            result.IdentifiedDepartment = "General Medicine";
            result.SpecialtyNeeded = "General Physician / MD Medicine";
            result.Severity = ContainsAny(q, "high", "tez", "chot", "severe") ? "Moderate" : "Low";
            result.MatchedKeywords.Add("General constitutional symptoms");
            result.AiAdviceEn = "बुखार, थकान या सामान्य अस्वस्थता संक्रमण से लड़ने का शारीरिक संकेत है। शरीर को पूर्ण आराम दें और तापमान को मापते रहें।";
            result.HomeRemedies.Add("भरपूर आराम करें और डिहाइड्रेशन से बचने के लिए पानी, ओआरएस, सूप या नारियल पानी पिएं।");
            result.HomeRemedies.Add("माथे पर सामान्य नल के पानी की पट्टी रखें और सूती, ढीले कपड़े पहनें।");
            result.HomeRemedies.Add("हल्का, सुपाच्य भोजन (जैसे मूंग दाल की खिचड़ी या दलिया) लें।");
        }

        AttachLiveDoctorNotice(result, liveDoctors);

        // Fetch doctors
        var docs = (await _doctorRepo.SearchDoctorsAsync(result.IdentifiedDepartment)).ToList();
        if (!docs.Any())
        {
            docs = (await _doctorRepo.SearchDoctorsAsync("")).Take(3).ToList();
        }
        result.RecommendedDoctors = docs;
        return result;
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        return values.Any(v => source.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
