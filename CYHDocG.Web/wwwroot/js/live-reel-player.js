/**
 * CYHDocGuru Live Reel Player Engine
 * Real-Time LiveKit WebRTC Video Streaming, SignalR Live Hub, Real-Time Active Viewer Counts & Guest Barriers
 */
(function () {
    'use strict';

    let connection = null;
    let activeDoctorId = null;
    let activeStreamId = null;
    let activeLiveKitRoom = null;
    let guestReelScrolls = 0;
    const MAX_GUEST_SCROLLS = 5;

    // 1. Initialize SignalR Connection
    function initSignalR() {
        if (typeof signalR === 'undefined') {
            console.warn('[LiveReel] SignalR library not loaded.');
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

        // Real-Time Active Viewer Count Listener
        connection.on('UpdateViewerCount', function (streamKey, count) {
            console.log('[LiveReel] Real-time viewer count update:', streamKey, count);
            document.querySelectorAll(`.viewer-count[data-doctor-id="${streamKey}"]`).forEach(el => {
                el.textContent = count;
                el.classList.add('text-red-400', 'font-black');
                setTimeout(() => el.classList.remove('text-red-400', 'font-black'), 400);
            });
        });

        // Real-Time Pinned Banner Listener
        connection.on('ReceivePinnedBanner', function (streamKey, banner) {
            console.log('[LiveReel] Pinned banner received for stream:', streamKey, banner);
            renderPatientPinnedBanner(streamKey, banner);
        });

        connection.on('ClearPinnedBanner', function (streamKey) {
            console.log('[LiveReel] Pinned banner cleared for stream:', streamKey);
            clearPatientPinnedBanner(streamKey);
        });

        // Real-Time Doctor Live Broadcast Status Change
        connection.on('DoctorLiveStatusChanged', function (status) {
            console.log('[LiveReel] DoctorLiveStatusChanged received:', status);
            const card = document.querySelector(`.reel-card[data-doctor-id="${status.doctorId}"]`);
            if (card) {
                if (!status.isLive) {
                    if (card.classList.contains('active-reel')) {
                        disconnectLiveKitStream();
                    }
                    card.style.transition = 'opacity 0.4s ease, transform 0.4s ease';
                    card.style.opacity = '0';
                    card.style.transform = 'scale(0.95)';
                    setTimeout(() => {
                        card.remove();
                        const remaining = document.querySelectorAll('.reel-card');
                        if (remaining.length === 0) {
                            window.location.reload();
                        } else {
                            syncActiveReel();
                        }
                    }, 400);
                } else {
                    card.setAttribute('data-is-live', 'true');
                    const liveBadge = card.querySelector('.doctor-live-badge');
                    if (liveBadge) liveBadge.classList.remove('hidden');
                    if (card.classList.contains('active-reel')) {
                        connectLiveKitStream(status.doctorId, card);
                    }
                }
            } else if (status.isLive) {
                // Doctor just went live and wasn't in list -> reload to show in reels
                setTimeout(() => window.location.reload(), 500);
            }
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

        // Consultation Accepted Listener (1-to-1 switch)
        connection.on('ConsultationAccepted', function (payload) {
            console.log('[LiveReel] ConsultationAccepted received:', payload);
            if (window.handleConsultationAcceptedByDoctor) {
                window.handleConsultationAcceptedByDoctor(payload);
            }
        });

        // Real-Time Comment Moderation Listeners
        connection.on('UserRemovedFromChat', function (data) {
            console.log('[LiveReel] UserRemovedFromChat:', data);
            document.querySelectorAll(`.floating-comment-item[data-user-id="${data.targetUserId}"]`).forEach(el => el.remove());
        });

        connection.on('CommentDeleted', function (commentId) {
            const el = document.getElementById(`floatingComment_${commentId}`);
            if (el) el.remove();
        });

        connection.start().then(() => {
            console.log('[LiveReel] Connected to SignalR Live Hub successfully');
            syncActiveReel();
        }).catch(err => console.error('[LiveReel] SignalR connect error:', err));
    }

    // 2. Sync Active Reel Room & Video Stream
    function syncActiveReel() {
        const activeCard = document.querySelector('.reel-card.active-reel');
        if (!activeCard) return;

        const doctorId = activeCard.getAttribute('data-doctor-id') || activeCard.getAttribute('data-stream-id');
        const isLive = activeCard.getAttribute('data-is-live') === 'true';

        if (doctorId && doctorId !== activeDoctorId) {
            if (connection && connection.state === signalR.HubConnectionState.Connected) {
                if (activeDoctorId) {
                    connection.invoke('LeaveStreamRoom', activeDoctorId.toString()).catch(() => {});
                }
                activeDoctorId = doctorId;
                activeStreamId = activeCard.getAttribute('data-stream-id') || doctorId;
                connection.invoke('JoinStreamRoom', doctorId.toString()).catch(console.error);
            } else {
                activeDoctorId = doctorId;
                activeStreamId = activeCard.getAttribute('data-stream-id') || doctorId;
            }

            // Manage Live Video Streaming
            if (isLive) {
                connectLiveKitStream(doctorId, activeCard);
            } else {
                disconnectLiveKitStream();
            }
        }
    }

    // 3. Connect LiveKit WebRTC Live Stream
    async function connectLiveKitStream(doctorId, card) {
        disconnectLiveKitStream();

        const videoEl = card.querySelector('video.live-stream-video');
        const placeholder = card.querySelector('.live-stream-placeholder');

        try {
            console.log('[LiveReel] Requesting LiveKit subscriber token for doctor:', doctorId);
            const res = await fetch(`/api/livedoctor/subscriber-token?doctorId=${doctorId}`);
            const data = await res.json();

            if (!data.success || !data.token) {
                console.warn('[LiveReel] Subscriber token not available:', data.message);
                return;
            }

            if (!window.LivekitClient) {
                console.warn('[LiveReel] LiveKit client SDK not loaded');
                return;
            }

            activeLiveKitRoom = new LivekitClient.Room({
                adaptiveStream: true,
                dynacast: true
            });

            activeLiveKitRoom.on(LivekitClient.RoomEvent.TrackSubscribed, (track, publication, participant) => {
                console.log('[LiveReel] Track subscribed from doctor studio:', track.kind);
                if (track.kind === 'video') {
                    if (videoEl) {
                        track.attach(videoEl);
                        videoEl.muted = true;
                        videoEl.playsInline = true;
                        videoEl.classList.remove('hidden');
                        if (placeholder) placeholder.classList.add('hidden');
                        videoEl.play().catch(e => console.warn('[LiveReel] Video play suppressed by browser policy:', e));
                    }
                } else if (track.kind === 'audio') {
                    let audioEl = document.getElementById('liveReelAudioTrack');
                    if (!audioEl) {
                        audioEl = document.createElement('audio');
                        audioEl.id = 'liveReelAudioTrack';
                        audioEl.autoplay = true;
                        card.appendChild(audioEl);
                    }
                    track.attach(audioEl);
                    audioEl.play().catch(e => console.warn('[LiveReel] Audio play suppressed:', e));
                }
            });

            activeLiveKitRoom.on(LivekitClient.RoomEvent.TrackUnsubscribed, (track) => {
                track.detach();
            });

            activeLiveKitRoom.on(LivekitClient.RoomEvent.Disconnected, () => {
                console.log('[LiveReel] LiveKit room disconnected');
            });

            await activeLiveKitRoom.connect(data.serverUrl, data.token);
            console.log('[LiveReel] Connected to LiveKit room:', data.roomName);

            // Immediately inspect and attach any already-published tracks from the doctor
            activeLiveKitRoom.remoteParticipants.forEach(participant => {
                participant.trackPublications.forEach(pub => {
                    if (pub.track) {
                        if (pub.kind === 'video') {
                            if (videoEl) {
                                pub.track.attach(videoEl);
                                videoEl.muted = true;
                                videoEl.playsInline = true;
                                videoEl.classList.remove('hidden');
                                if (placeholder) placeholder.classList.add('hidden');
                                videoEl.play().catch(e => console.warn('[LiveReel] Video play suppressed:', e));
                            }
                        } else if (pub.kind === 'audio') {
                            let audioEl = document.getElementById('liveReelAudioTrack');
                            if (!audioEl) {
                                audioEl = document.createElement('audio');
                                audioEl.id = 'liveReelAudioTrack';
                                audioEl.autoplay = true;
                                card.appendChild(audioEl);
                            }
                            pub.track.attach(audioEl);
                            audioEl.play().catch(e => console.warn('[LiveReel] Audio play suppressed:', e));
                        }
                    }
                });
            });
        } catch (err) {
            console.warn('[LiveReel] LiveKit subscriber error:', err);
        }
    }

    function disconnectLiveKitStream() {
        if (activeLiveKitRoom) {
            try {
                activeLiveKitRoom.disconnect();
            } catch (e) {}
            activeLiveKitRoom = null;
        }

        const audioEl = document.getElementById('liveReelAudioTrack');
        if (audioEl) audioEl.remove();

        const activeCard = document.querySelector('.reel-card.active-reel');
        if (activeCard) {
            const videoEl = activeCard.querySelector('video.live-stream-video');
            const placeholder = activeCard.querySelector('.live-stream-placeholder');
            if (videoEl && activeCard.getAttribute('data-is-live') !== 'true') {
                videoEl.classList.add('hidden');
                videoEl.srcObject = null;
                if (placeholder) placeholder.classList.remove('hidden');
            }
        }
    }

    // 4. Audio Mute / Unmute Toggle
    window.toggleReelAudio = function (btn) {
        const activeCard = document.querySelector('.reel-card.active-reel');
        if (!activeCard) return;

        const video = activeCard.querySelector('video.live-stream-video');
        const audioEl = document.getElementById('liveReelAudioTrack');

        let isMuted = true;
        if (video) {
            video.muted = !video.muted;
            isMuted = video.muted;
        }
        if (audioEl) {
            audioEl.muted = isMuted;
        }

        if (btn) {
            if (isMuted) {
                btn.innerHTML = '<i data-lucide="volume-x" class="w-4 h-4"></i>';
                btn.title = "Unmute Audio";
            } else {
                btn.innerHTML = '<i data-lucide="volume-2" class="w-4 h-4 text-emerald-400"></i>';
                btn.title = "Mute Audio";
            }
            if (window.lucide) window.lucide.createIcons();
        }
    };

    // 4.5. Render & Clear Pinned Health Tip / Medicine Banner
    function renderPatientPinnedBanner(streamKey, banner) {
        if (!banner) return;
        const card = document.querySelector(`.reel-card[data-doctor-id="${streamKey}"]`);
        if (!card) return;

        const box = card.querySelector('.pinned-banner-box');
        if (!box) return;

        const iconEl = box.querySelector('.pinned-banner-icon');
        const typeEl = box.querySelector('.pinned-banner-type');
        const medEl = box.querySelector('.pinned-banner-med');
        const textEl = box.querySelector('.pinned-banner-text');

        const icons = { tip: '💡', medicine: '💊', diet: '🥗', warning: '⚠️' };
        const labels = { tip: 'PINNED ADVISORY', medicine: 'MEDICINE RX', diet: 'DIET & NUTRITION', warning: 'URGENT WARNING' };

        if (iconEl) iconEl.textContent = icons[banner.bannerType] || '💡';
        if (typeEl) typeEl.textContent = labels[banner.bannerType] || 'PINNED ADVISORY';
        if (textEl) textEl.textContent = banner.bannerText;

        if (medEl) {
            if (banner.medicineName) {
                medEl.textContent = 'Rx: ' + banner.medicineName;
                medEl.classList.remove('hidden');
            } else {
                medEl.classList.add('hidden');
            }
        }

        box.classList.remove('hidden');
        if (window.lucide) window.lucide.createIcons();
    }

    function clearPatientPinnedBanner(streamKey) {
        const card = document.querySelector(`.reel-card[data-doctor-id="${streamKey}"]`);
        if (!card) return;
        const box = card.querySelector('.pinned-banner-box');
        if (box) box.classList.add('hidden');
    }

    // 5. Append Floating Real-Time Comment (Maintain Latest 5)
    function appendFloatingComment(streamId, comment) {
        const streamContainer = document.querySelector(`.reel-card[data-stream-id="${streamId}"] .floating-comments-box, .reel-card[data-doctor-id="${streamId}"] .floating-comments-box`);
        if (!streamContainer) return;

        const commentEl = document.createElement('div');
        commentEl.className = 'floating-comment-item flex items-center gap-1.5 text-xs text-white bg-black/40 backdrop-blur-md px-2.5 py-1 rounded-xl border border-white/10 animate-fade-in transition-all';
        if (comment.commentId) commentEl.id = `floatingComment_${comment.commentId}`;
        if (comment.userId) commentEl.setAttribute('data-user-id', comment.userId);

        commentEl.innerHTML = `
            <span class="font-bold text-red-400">${escapeHtml(comment.userName)}:</span>
            <span class="text-slate-200">${escapeHtml(comment.message)}</span>
        `;

        streamContainer.appendChild(commentEl);

        while (streamContainer.children.length > 5) {
            streamContainer.removeChild(streamContainer.firstChild);
        }

        streamContainer.scrollTop = streamContainer.scrollHeight;
    }

    // 6. Like UI Update
    function updateStreamLikeUI(streamId, isLiked) {
        const likeBtn = document.querySelector(`.reel-card[data-stream-id="${streamId}"] .reel-like-btn, .reel-card[data-doctor-id="${streamId}"] .reel-like-btn`);
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

    // 7. Guest Interception
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

    // 8. Snap Reel Observer
    function initReelObserver() {
        const reels = document.querySelectorAll('.reel-card');
        if (!reels.length) return;

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    reels.forEach(r => r.classList.remove('active-reel'));
                    entry.target.classList.add('active-reel');
                    syncActiveReel();

                    // Guest Scroll Tracker
                    if (!window.isUserLoggedIn) {
                        const index = parseInt(entry.target.getAttribute('data-index') || '0');
                        if (index >= MAX_GUEST_SCROLLS - 1) {
                            document.getElementById('reelsContainer').style.overflowY = 'hidden';
                            openGuestLoginModal('You have watched 5 live doctor reels. Please sign in to continue uninterrupted healthcare streaming.');
                        }
                    }
                }
            });
        }, { threshold: 0.6 });

        reels.forEach(reel => observer.observe(reel));
    }

    // 9. Countdown Timer for Scheduled Doctors
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

        if (connection && activeDoctorId) {
            try {
                await connection.invoke('SendLiveComment', parseInt(activeDoctorId), message);
            } catch (err) {
                console.error(err);
            }
        }
    };

    // Toggle Stream Like Handler
    window.handleToggleReelLike = async function (streamOrDoctorId) {
        if (!checkGuestInteraction(null, 'like')) return;
        if (connection) {
            try {
                const id = activeDoctorId ? parseInt(activeDoctorId) : parseInt(streamOrDoctorId);
                await connection.invoke('SendLike', id);
            } catch (err) {
                console.error(err);
            }
        }
    };

    // DOM Ready
    document.addEventListener('DOMContentLoaded', function () {
        initSignalR();
        initReelObserver();
        initCountdownTimers();
    });
})();
