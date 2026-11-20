/**
 * CYHDocGuru Language Switcher (Hindi, English, Hinglish)
 * Real-time dynamic UI translation engine
 */
(function () {
    'use strict';

    const translations = {
        en: {
            brand_subtitle: "Smart Healthcare & Tele-Consultation",
            nav_home: "Home",
            nav_live_chat: "LIVE Chat",
            nav_live_doctor: "LIVE DOCTOR",
            nav_login: "Sign In",
            nav_profile: "My Dashboard",
            nav_password: "Change Password",
            nav_logout: "Sign Out",
            hero_badge: "AI Powered Tele-Medicine 24/7",
            hero_title_1: "Your Personal Health Guardian,",
            hero_title_2: "Consult Top Doctors Live",
            hero_desc: "Instant AI symptom triage, real-time live doctor streaming reels, and smart prescription dosage reminders at your fingertips.",
            search_label: "Search Symptoms or Medical Departments",
            search_placeholder: "Type fever, heart pain, cough, dermatology...",
            search_btn: "AI Diagnose",
            awareness_tag: "Daily Health Insights",
            awareness_title: "Medical Awareness & Prevention",
            live_hook_tag: "Instant Telehealth",
            live_hook_title: "Speak Live With Certified Specialists",
            live_hook_desc: "Watch real-time live streams, join interactive video chats, or escalate from AI directly to available doctors in seconds.",
            live_hook_btn: "Explore LIVE Doctor Reels",
            live_chat_btn: "Start AI Live Chat",
            wallet_label: "Wallet Balance"
        },
        hi: {
            brand_subtitle: "स्मार्ट स्वास्थ्य एवं ऑनलाइन परामर्श",
            nav_home: "होम",
            nav_live_chat: "लाइव चैट",
            nav_live_doctor: "लाइव डॉक्टर",
            nav_login: "लॉगिन करें",
            nav_profile: "मेरा डैशबोर्ड",
            nav_password: "पासवर्ड बदलें",
            nav_logout: "लॉग आउट",
            hero_badge: "एआई संचालित टेली-मेडिसिन 24/7",
            hero_title_1: "आपका व्यक्तिगत स्वास्थ्य रक्षक,",
            hero_title_2: "विशेषज्ञ डॉक्टरों से लाइव परामर्श लें",
            hero_desc: "त्वरित एआई लक्षण पहचान, लाइव डॉक्टर वीडियो रील्स, और स्मार्ट प्रिस्क्रिप्शन दवा रिमाइंडर आपकी उंगलियों पर।",
            search_label: "अपनी बीमारी, लक्षण या विभाग खोजें",
            search_placeholder: "बुखार, सीने में दर्द, त्वचा रोग, खाँसी लिखें...",
            search_btn: "एआई से खोजें",
            awareness_tag: "दैनिक स्वास्थ्य सुझाव",
            awareness_title: "चिकित्सा जागरूकता और बचाव",
            live_hook_tag: "त्वरित ऑनलाइन डॉक्टर",
            live_hook_title: "प्रमाणित डॉक्टरों से सीधे लाइव बात करें",
            live_hook_desc: "लाइव डॉक्टर रील्स देखें, रीयल-टाइम चैट करें, और आवश्यकता पड़ने पर एआई से सीधे डॉक्टर से जुड़ें।",
            live_hook_btn: "लाइव डॉक्टर रील्स देखें",
            live_chat_btn: "एआई लाइव चैट शुरू करें",
            wallet_label: "वॉलेट बैलेंस"
        },
        hinglish: {
            brand_subtitle: "Smart Healthcare & Online Consultation",
            nav_home: "Home",
            nav_live_chat: "LIVE Chat",
            nav_live_doctor: "LIVE DOCTOR",
            nav_login: "Login Karein",
            nav_profile: "Mera Dashboard",
            nav_password: "Password Change",
            nav_logout: "Logout Karein",
            hero_badge: "AI Powered Tele-Medicine 24/7",
            hero_title_1: "Aapka Personal Health Guardian,",
            hero_title_2: "Specialist Doctors Se Live Baat Karein",
            hero_desc: "Instant AI symptom triage, real-time live doctor streaming reels aur smart prescription dawai reminders aapke hath me.",
            search_label: "Apni bimari ya department search karein",
            search_placeholder: "Bukhar, chest pain, skin rash, cough type karein...",
            search_btn: "AI Se Pucho",
            awareness_tag: "Daily Health Tips",
            awareness_title: "Medical Awareness & Health Care",
            live_hook_tag: "Instant Live Doctor",
            live_hook_title: "Certified Specialists Se Live Baat Karein",
            live_hook_desc: "Live doctor stream reels dekhein, real-time chat karein aur zaroorat padne par AI se seedhe live doctor ko request bhejein.",
            live_hook_btn: "LIVE Doctor Reels Dekhein",
            live_chat_btn: "AI Live Chat Shuru Karein",
            wallet_label: "Wallet Balance"
        }
    };

    function applyLanguage(lang) {
        if (!translations[lang]) lang = 'en';
        localStorage.setItem('cyh_lang', lang);

        document.querySelectorAll('[data-i18n]').forEach(el => {
            const key = el.getAttribute('data-i18n');
            if (translations[lang][key]) {
                if (el.tagName === 'INPUT' && el.getAttribute('placeholder') !== null) {
                    el.placeholder = translations[lang][key];
                } else {
                    el.textContent = translations[lang][key];
                }
            }
        });

        // Update active flag or dropdown display
        const langDisplay = document.getElementById('currentLangLabel');
        if (langDisplay) {
            langDisplay.textContent = lang === 'hi' ? 'हिंदी' : (lang === 'hinglish' ? 'Hinglish' : 'English');
        }
    }

    window.switchLanguage = function (lang) {
        applyLanguage(lang);
    };

    document.addEventListener('DOMContentLoaded', function () {
        const savedLang = localStorage.getItem('cyh_lang') || 'en';
        applyLanguage(savedLang);
    });
})();
