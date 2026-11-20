/**
 * CYHDocGuru Dual Synchronized Consultation Timer Engine
 * Syncs real-time clock and live billing between Doctor & User
 */
(function () {
    'use strict';

    let timerInterval = null;
    let consultationStartTime = null;
    let ratePerMinute = 0;
    let isPaidConsultation = false;

    window.startSynchronizedTimer = function (startedAtIso, rate, isPaid) {
        clearInterval(timerInterval);

        consultationStartTime = startedAtIso ? new Date(startedAtIso).getTime() : Date.now();
        ratePerMinute = rate || 0;
        isPaidConsultation = !!isPaid;

        const timerContainer = document.getElementById('consultationTimerContainer');
        if (timerContainer) timerContainer.classList.remove('hidden');

        updateTimerUI();
        timerInterval = setInterval(updateTimerUI, 1000);
    };

    window.stopSynchronizedTimer = function () {
        clearInterval(timerInterval);
    };

    function updateTimerUI() {
        if (!consultationStartTime) return;

        const now = Date.now();
        const elapsedMs = Math.max(0, now - consultationStartTime);
        const totalSeconds = Math.floor(elapsedMs / 1000);

        const hours = Math.floor(totalSeconds / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const seconds = totalSeconds % 60;

        const clockDisplay = document.getElementById('consultationClock');
        if (clockDisplay) {
            clockDisplay.textContent = `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
        }

        // Live billing accrued calculation
        const billDisplay = document.getElementById('consultationAccruedBill');
        if (billDisplay) {
            if (isPaidConsultation) {
                const billedMinutes = Math.max(1, Math.ceil(totalSeconds / 60));
                const accrued = billedMinutes * ratePerMinute;
                billDisplay.textContent = `₹${accrued.toFixed(2)} (₹${ratePerMinute}/min)`;
            } else {
                billDisplay.textContent = 'FREE Consultation';
            }
        }
    }

    function pad(n) {
        return n < 10 ? '0' + n : n;
    }
})();
