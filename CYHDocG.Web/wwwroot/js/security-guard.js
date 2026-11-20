/**
 * CYHDocGuru Security Guard
 * Dynamic Anti-Inspect, DevTools Lock & Security Toast Engine
 * Configurable via Admin Toggle (AppSettings: AntiInspectEnabled)
 */
(function () {
    'use strict';

    // Check if security engine is enabled on page
    const metaTag = document.querySelector('meta[name="anti-inspect-enabled"]');
    const isSecurityActive = metaTag ? metaTag.getAttribute('content') === 'true' : true;

    if (!isSecurityActive) {
        console.log('[CYHDocGuru Security] Anti-inspect is currently disabled by Admin.');
        return;
    }

    let isToastVisible = false;
    let lastLogTime = 0;

    // Create & Inject Sleek Security Toast Container
    function createSecurityToast() {
        if (document.getElementById('cyh-security-toast')) return;

        const toastHtml = `
        <div id="cyh-security-toast" class="fixed top-5 left-1/2 -translate-x-1/2 z-[99999] pointer-events-none transition-all duration-300 ease-out transform -translate-y-20 opacity-0 max-w-sm sm:max-w-md w-[92%]">
            <div class="bg-slate-900/95 backdrop-blur-md border border-red-500/40 text-white px-4 py-3.5 rounded-2xl shadow-2xl flex items-center gap-3.5 ring-1 ring-red-500/20">
                <div class="w-10 h-10 rounded-xl bg-red-600/20 border border-red-500/30 flex items-center justify-center flex-shrink-0 text-red-500">
                    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
                        <line x1="12" y1="8" x2="12" y2="12"/>
                        <line x1="12" y1="16" x2="12.01" y2="16"/>
                    </svg>
                </div>
                <div class="flex-1 min-w-0">
                    <div class="flex items-center gap-2">
                        <span class="text-xs font-bold tracking-wider uppercase text-red-400">Security Alert</span>
                        <span class="w-1.5 h-1.5 rounded-full bg-red-500 animate-pulse"></span>
                    </div>
                    <p id="cyh-toast-message" class="text-xs sm:text-sm text-slate-200 mt-0.5 font-medium leading-snug">
                        Developer tools and source inspection are restricted by administrator.
                    </p>
                </div>
            </div>
        </div>
        `;
        document.body.insertAdjacentHTML('beforeend', toastHtml);
    }

    // Trigger Warning Toast with vibration & auto-hide
    function triggerSecurityAlert(reason) {
        createSecurityToast();
        const toast = document.getElementById('cyh-security-toast');
        if (!toast) return;

        // Vibrate mobile devices if supported
        if ('vibrate' in navigator) {
            navigator.vibrate([100, 50, 100]);
        }

        toast.classList.remove('-translate-y-20', 'opacity-0');
        toast.classList.add('translate-y-0', 'opacity-100');
        isToastVisible = true;

        // Send audit log to server (throttled to 1 log per 5 seconds)
        const now = Date.now();
        if (now - lastLogTime > 5000) {
            lastLogTime = now;
            logSecurityIncident(reason);
        }

        setTimeout(() => {
            toast.classList.remove('translate-y-0', 'opacity-100');
            toast.classList.add('-translate-y-20', 'opacity-0');
            isToastVisible = false;
        }, 3200);
    }

    // Log attempt to backend
    function logSecurityIncident(action) {
        try {
            fetch('/api/security/log-event', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    eventAction: action,
                    userAgent: navigator.userAgent
                })
            }).catch(() => {});
        } catch (e) {}
    }

    // 1. Block Context Menu (Right Click)
    document.addEventListener('contextmenu', function (e) {
        e.preventDefault();
        triggerSecurityAlert('RightClickBlocked');
        return false;
    }, { capture: true });

    // 2. Block DevTools Shortcuts (F12, Ctrl+Shift+I, Ctrl+Shift+J, Ctrl+Shift+C, Ctrl+U, Ctrl+S)
    document.addEventListener('keydown', function (e) {
        // F12
        if (e.key === 'F12' || e.keyCode === 123) {
            e.preventDefault();
            e.stopPropagation();
            triggerSecurityAlert('F12_Pressed');
            return false;
        }

        const isCtrlOrMeta = e.ctrlKey || e.metaKey;

        // Ctrl + Shift + (I, J, C)
        if (isCtrlOrMeta && e.shiftKey && (e.key === 'I' || e.key === 'i' || e.key === 'J' || e.key === 'j' || e.key === 'C' || e.key === 'c' || e.keyCode === 73 || e.keyCode === 74 || e.keyCode === 67)) {
            e.preventDefault();
            e.stopPropagation();
            triggerSecurityAlert('DevTools_Shortcut');
            return false;
        }

        // Ctrl + U (View Source)
        if (isCtrlOrMeta && (e.key === 'U' || e.key === 'u' || e.keyCode === 85)) {
            e.preventDefault();
            e.stopPropagation();
            triggerSecurityAlert('ViewSource_Blocked');
            return false;
        }

        // Ctrl + S (Save Page)
        if (isCtrlOrMeta && (e.key === 'S' || e.key === 's' || e.keyCode === 83)) {
            e.preventDefault();
            e.stopPropagation();
            triggerSecurityAlert('SavePage_Blocked');
            return false;
        }
    }, { capture: true });

    // 3. DevTools Open Detection via Screen Docks
    let devtoolsOpen = false;
    const threshold = 160;

    setInterval(function () {
        const widthDiff = window.outerWidth - window.innerWidth > threshold;
        const heightDiff = window.outerHeight - window.innerHeight > threshold;

        if (widthDiff || heightDiff) {
            if (!devtoolsOpen) {
                devtoolsOpen = true;
                triggerSecurityAlert('DevTools_Docked_Opened');
            }
        } else {
            devtoolsOpen = false;
        }
    }, 1500);

    // Initial DOM ready toast injector
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', createSecurityToast);
    } else {
        createSecurityToast();
    }
})();
