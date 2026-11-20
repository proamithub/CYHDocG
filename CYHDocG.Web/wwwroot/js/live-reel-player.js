/**
 * CYHDocGuru Live Reel Player Engine
 * SignalR Real-Time Streaming, Floating Overlay Comments, Guest Limitations & Mobile Reel Gestures
 */
(function () {
    'use strict';

    let connection = null;
    let activeStreamId = null;
    let guestReelScrolls = 0;
    const MAX_GUEST_SCROLLS = 5;

    // 1. Initialize SignalR Connection
    function initSignalR() {
        if (typeof signalR === 'undefined') {
            console.warn('[LiveReel] SignalR library not loaded yet.');
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/live-reel')
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .build();

        // Real-Time Floating Comment Listener
        connection.on('ReceiveNewComment', function (streamId, comment) {
            appendFloatingComment(streamId, comment);
        });

        // Like Count Listener
        connection.on('ReceiveLikeUpdate', function (streamId, isLiked) {
            updateStreamLikeUI(streamId, isLiked);
        });

        // Auth Restriction Listener
        connection.on('AuthRequired', function (msg) {
            openGuestLoginModal(msg);
        });

        connection.on('UserBlockedFromChat', function (msg) {
            alert(msg);
            const input = document.getElementById('reelChatInput');
            if (input) input.disabled = true;
        });

        connection.start().then(() => {
            console.log('[LiveReel] Connected to SignalR Live Hub');
            joinActiveStreamRoom();
        }).catch(err => console.error('[LiveReel] SignalR error:', err));
    }

    function joinActiveStreamRoom() {
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) return;
        const activeCard = document.querySelector('.reel-card.active-reel');
        if (activeCard) {
            const streamId = activeCard.getAttribute('data-stream-id');
            if (streamId && streamId !== activeStreamId) {
                if (activeStreamId) {
                    connection.invoke('LeaveStreamRoom', activeStreamId.toString());
                }
                activeStreamId = streamId;
                connection.invoke('JoinStreamRoom', streamId.toString());
            }
        }
    }

    // 2. Append Floating Real-Time Comment (Maintain Latest 5)
    function appendFloatingComment(streamId, comment) {
        const streamContainer = document.querySelector(`.reel-card[data-stream-id="${streamId}"] .floating-comments-box`);
        if (!streamContainer) return;

        const commentEl = document.createElement('div');
        commentEl.className = 'flex items-center gap-1.5 text-xs text-white bg-black/40 backdrop-blur-md px-2.5 py-1 rounded-xl border border-white/10 animate-fade-in transition-all';
        commentEl.innerHTML = `
            <span class="font-bold text-red-400">${escapeHtml(comment.userName)}:</span>
            <span class="text-slate-200">${escapeHtml(comment.message)}</span>
        `;

        streamContainer.appendChild(commentEl);

        // Keep maximum 5 comments in view
        while (streamContainer.children.length > 5) {
            streamContainer.removeChild(streamContainer.firstChild);
        }

        // Auto scroll to bottom
        streamContainer.scrollTop = streamContainer.scrollHeight;
    }

    // 3. Like UI Update
    function updateStreamLikeUI(streamId, isLiked) {
        const likeBtn = document.querySelector(`.reel-card[data-stream-id="${streamId}"] .reel-like-btn`);
        if (!likeBtn) return;

        const icon = likeBtn.querySelector('svg') || likeBtn.querySelector('i');
        const countSpan = likeBtn.querySelector('.like-count');

        let currentCount = parseInt(countSpan ? countSpan.textContent : '0') || 0;
        if (isLiked) {
            likeBtn.classList.add('text-red-500');
            if (icon) icon.setAttribute('fill', 'currentColor');
            if (countSpan) countSpan.textContent = currentCount + 1;
        } else {
            likeBtn.classList.remove('text-red-500');
            if (icon) icon.setAttribute('fill', 'none');
            if (countSpan) countSpan.textContent = Math.max(0, currentCount - 1);
        }
    }

    // 4. Guest Click Interception
    window.checkGuestInteraction = function (e, actionName) {
        if (!window.isUserLoggedIn) {
            if (e) {
                e.preventDefault();
                e.stopPropagation();
            }
            openGuestLoginModal(`Please sign in to ${actionName || 'interact'} with live doctors.`);
            return false;
        }
        return true;
    };

    window.openGuestLoginModal = function (message) {
        const modal = document.getElementById('guestLoginBarrierModal');
        const msgEl = document.getElementById('guestBarrierMessage');
        if (msgEl && message) msgEl.textContent = message;
        if (modal) modal.classList.remove('hidden');
    };

    window.closeGuestLoginModal = function () {
        const modal = document.getElementById('guestLoginBarrierModal');
        if (modal) modal.classList.add('hidden');
    };

    // 5. Scroll Intersection Observer (Snap Reel Detection & Guest Scroll Wall)
    function initReelObserver() {
        const reels = document.querySelectorAll('.reel-card');
        if (!reels.length) return;

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    reels.forEach(r => r.classList.remove('active-reel'));
                    entry.target.classList.add('active-reel');
                    joinActiveStreamRoom();

                    // Guest Scroll Tracker
                    if (!window.isUserLoggedIn) {
                        const index = parseInt(entry.target.getAttribute('data-index') || '0');
                        if (index >= MAX_GUEST_SCROLLS - 1) {
                            // Lock feed & trigger login popup
                            document.getElementById('reelsContainer').style.overflowY = 'hidden';
                            openGuestLoginModal('You have watched 5 live doctor reels. Please sign in to continue uninterrupted healthcare streaming.');
                        }
                    }
                }
            });
        }, { threshold: 0.6 });

        reels.forEach(reel => observer.observe(reel));
    }

    // 6. Countdown Timer for Scheduled Doctors
    function initCountdownTimers() {
        setInterval(() => {
            document.querySelectorAll('[data-scheduled-time]').forEach(el => {
                const targetTime = new Date(el.getAttribute('data-scheduled-time')).getTime();
                const now = new Date().getTime();
                const diff = targetTime - now;

                if (diff <= 0) {
                    el.textContent = 'Stream Starting Soon...';
                } else {
                    const hours = Math.floor(diff / (1000 * 60 * 60));
                    const mins = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
                    const secs = Math.floor((diff % (1000 * 60)) / 1000);
                    el.textContent = `Live in ${pad(hours)}h : ${pad(mins)}m : ${pad(secs)}s`;
                }
            });
        }, 1000);
    }

    function pad(n) { return n < 10 ? '0' + n : n; }

    function escapeHtml(text) {
        if (!text) return '';
        const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
        return text.replace(/[&<>"']/g, m => map[m]);
    }

    // Send Live Comment Handler
    window.handleSendReelComment = async function (e) {
        if (e) e.preventDefault();
        if (!checkGuestInteraction(e, 'comment')) return;

        const input = document.getElementById('reelChatInput');
        if (!input || !input.value.trim()) return;

        const message = input.value.trim();
        input.value = '';

        if (connection && activeStreamId) {
            try {
                await connection.invoke('SendLiveComment', parseInt(activeStreamId), message);
            } catch (err) {
                console.error(err);
            }
        }
    };

    // Toggle Stream Like Handler
    window.handleToggleReelLike = async function (streamId) {
        if (!checkGuestInteraction(null, 'like')) return;
        if (connection) {
            try {
                await connection.invoke('SendLike', parseInt(streamId));
            } catch (err) {
                console.error(err);
            }
        }
    };

    // DOM Ready Initialization
    document.addEventListener('DOMContentLoaded', function () {
        initSignalR();
        initReelObserver();
        initCountdownTimers();
    });
})();
