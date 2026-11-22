/*
 * Live attendance page.
 * - Detects faces continuously (~4-6 fps) and tracks them across frames (liveness.js).
 * - Every ~1.5 s, sends descriptors of live, not-yet-resolved faces to POST /Attendance/Recognize.
 * - Labels each face box (green = present, red = unknown, amber = checking) and keeps the present list in sync.
 */
(function (window, document) {
    'use strict';

    var root = document.getElementById('live-app');
    if (!root || !window.FaceApp) return;

    var FaceApp = window.FaceApp;
    var cfg = {
        sessionId: parseInt(root.getAttribute('data-session-id'), 10),
        recognizeUrl: root.getAttribute('data-recognize-url'),
        overrideUrl: root.getAttribute('data-override-url'),
        statusUrl: root.getAttribute('data-status-url'),
        detailUrl: root.getAttribute('data-detail-url'),
        modelsUrl: root.getAttribute('data-models-url'),
        rosterSize: parseInt(root.getAttribute('data-roster-size'), 10) || 0,
        detectEveryMs: 200,
        recognizeEveryMs: 1500,
        unknownRetryMs: 3000,
        maxUnknownAttempts: 5,
        refreshEveryMs: 15000,
        maxFacesPerRequest: 10
    };

    var el = {
        video: document.getElementById('liveVideo'),
        canvas: document.getElementById('liveCanvas'),
        status: document.getElementById('liveStatus'),
        start: document.getElementById('btnStart'),
        pause: document.getElementById('btnPause'),
        snapshots: document.getElementById('chkSnapshots'),
        presentCount: document.getElementById('presentCount'),
        presentList: document.getElementById('presentList'),
        presentEmpty: document.getElementById('presentEmpty'),
        absentList: document.getElementById('absentList'),
        absentCount: document.getElementById('absentCount'),
        log: document.getElementById('recognitionLog')
    };

    var camera = new FaceApp.Camera(el.video, el.canvas);
    var tracker = new FaceApp.Tracker();
    var running = false, detecting = false, sending = false;
    var lastDetect = 0, lastSend = 0;
    var resolvedStudents = {}; // studentId -> name, for students already present

    // ---------- UI helpers ----------

    function setPresentCount(n) {
        el.presentCount.textContent = n;
    }

    function log(text, kind) {
        var li = document.createElement('li');
        li.className = 'small ' + ({ ok: 'text-success', warn: 'text-warning-emphasis', error: 'text-danger' }[kind] || 'text-muted');
        li.textContent = new Date().toLocaleTimeString() + ' - ' + text;
        el.log.insertBefore(li, el.log.firstChild);
        while (el.log.children.length > 30) el.log.removeChild(el.log.lastChild);
    }

    function rosterRow(studentId) {
        return el.absentList.querySelector('[data-student-id="' + studentId + '"]');
    }

    function addPresent(p, flash) {
        if (el.presentList.querySelector('[data-student-id="' + p.studentId + '"]')) return;

        var li = document.createElement('li');
        li.className = 'list-group-item d-flex justify-content-between align-items-center' + (flash ? ' flash' : '');
        li.setAttribute('data-student-id', p.studentId);
        li.innerHTML =
            '<div><div class="fw-semibold">' + FaceApp.escapeHtml(p.name) + '</div>' +
            '<div class="small text-muted">' + FaceApp.escapeHtml(p.rollNumber) + ' &middot; ' + FaceApp.escapeHtml(p.markedAt || '') +
            (p.method === 'manual' ? ' &middot; manual' : '') + '</div></div>' +
            '<button type="button" class="btn btn-sm btn-link text-danger p-0" data-action="absent" title="Mark absent">Undo</button>';
        el.presentList.insertBefore(li, el.presentList.firstChild);
        el.presentEmpty.hidden = true;

        var row = rosterRow(p.studentId);
        if (row) row.hidden = true;
        resolvedStudents[p.studentId] = p.name;
        updateAbsentCount();
    }

    function removePresent(studentId) {
        var li = el.presentList.querySelector('[data-student-id="' + studentId + '"]');
        if (li) li.parentNode.removeChild(li);
        el.presentEmpty.hidden = el.presentList.children.length > 0;
        var row = rosterRow(studentId);
        if (row) row.hidden = false;
        delete resolvedStudents[studentId];
        updateAbsentCount();
    }

    function updateAbsentCount() {
        var visible = el.absentList.querySelectorAll('[data-student-id]:not([hidden])').length;
        el.absentCount.textContent = visible;
    }

    // ---------- camera + detection loop ----------

    async function start() {
        el.start.disabled = true;
        try {
            FaceApp.setStatus(el.status, 'Loading face recognition models (first time can take a few seconds)…');
            var backend = await FaceApp.loadModels(cfg.modelsUrl);
            FaceApp.setStatus(el.status, 'Starting camera…');
            await camera.start();
            running = true;
            el.pause.disabled = false;
            el.start.hidden = true;
            el.pause.hidden = false;
            FaceApp.setStatus(el.status, 'Recognising (' + backend + '). Students should look at the camera and blink or turn their head slightly.', 'ok');
            window.requestAnimationFrame(loop);
        } catch (err) {
            console.error(err);
            FaceApp.setStatus(el.status, FaceApp.Camera.friendlyError(err), 'error');
            el.start.disabled = false;
        }
    }

    function pause() {
        running = false;
        camera.stop();
        el.start.hidden = false;
        el.start.disabled = false;
        el.start.textContent = 'Resume camera';
        el.pause.hidden = true;
        FaceApp.setStatus(el.status, 'Camera paused.');
    }

    async function loop(now) {
        if (!running) return;
        if (!detecting && now - lastDetect >= cfg.detectEveryMs) {
            detecting = true;
            lastDetect = now;
            try {
                await detect();
            } catch (err) {
                console.error(err);
            }
            detecting = false;
        }
        if (!sending && Date.now() - lastSend >= cfg.recognizeEveryMs) {
            lastSend = Date.now();
            recognize();
        }
        window.requestAnimationFrame(loop);
    }

    async function detect() {
        var detections = await window.faceapi
            .detectAllFaces(el.video, FaceApp.detectorOptions())
            .withFaceLandmarks()
            .withFaceDescriptors();
        if (!running) return;

        var tracks = tracker.update(detections, Date.now());
        camera.clear();
        tracks.forEach(function (t) {
            var color, label;
            switch (t.status) {
                case 'marked':
                case 'already':
                    color = '#198754'; label = t.label; break;
                case 'unknown':
                    color = '#dc3545'; label = 'Unknown'; break;
                case 'ambiguous':
                    color = '#dc3545'; label = 'Not sure - look straight'; break;
                case 'manual_absent':
                    color = '#6c757d'; label = t.label + ' (marked absent)'; break;
                case 'sending':
                    color = '#0d6efd'; label = 'Recognising…'; break;
                default:
                    color = '#ffc107';
                    label = FaceApp.Tracker.isLive(t) ? 'Recognising…' : 'Checking liveness - blink';
            }
            camera.drawBox(t.box, color, label);
        });
    }

    function needsRecognition(t, now) {
        if (!FaceApp.Tracker.isLive(t) || !t.descriptor) return false;
        if (t.status === 'sending' || t.status === 'marked' || t.status === 'already' || t.status === 'manual_absent') return false;
        if (t.status === 'unknown' || t.status === 'ambiguous') {
            return t.attempts < cfg.maxUnknownAttempts && now - t.lastSent >= cfg.unknownRetryMs;
        }
        return true;
    }

    function recognize() {
        var now = Date.now();
        var batch = tracker.tracks.filter(function (t) { return now - t.lastSeen < 500 && needsRecognition(t, now); })
            .slice(0, cfg.maxFacesPerRequest);
        if (batch.length === 0) return;

        var saveSnapshots = !!(el.snapshots && el.snapshots.checked);
        var faces = batch.map(function (t) {
            t.status = 'sending';
            t.lastSent = now;
            t.attempts++;
            return {
                trackId: t.id,
                descriptor: FaceApp.descriptorToBase64(t.descriptor),
                snapshot: saveSnapshots ? camera.captureFace(t.box) : null
            };
        });

        sending = true;
        FaceApp.postJson(cfg.recognizeUrl, { sessionId: cfg.sessionId, saveSnapshots: saveSnapshots, faces: faces })
            .then(function (res) {
                res.results.forEach(function (r) {
                    var track = tracker.tracks.filter(function (t) { return t.id === r.trackId; })[0];
                    if (track) {
                        track.status = r.status === 'invalid' ? 'unknown' : r.status;
                        track.label = r.name ? r.name + ' (' + r.rollNumber + ')' : '';
                    }
                    if (r.status === 'marked') {
                        addPresent({ studentId: r.studentId, name: r.name, rollNumber: r.rollNumber, markedAt: r.markedAt, method: 'face' }, true);
                        log(r.name + ' (' + r.rollNumber + ') marked present, distance ' + r.distance, 'ok');
                    } else if (r.status === 'already' && !resolvedStudents[r.studentId]) {
                        addPresent({ studentId: r.studentId, name: r.name, rollNumber: r.rollNumber, markedAt: r.markedAt, method: 'face' }, false);
                    } else if (r.status === 'manual_absent') {
                        log(r.name + ' was recognised but is marked absent manually; use "Mark present" to change it.', 'warn');
                    }
                });
                setPresentCount(res.presentCount);
            })
            .catch(function (err) {
                batch.forEach(function (t) { if (t.status === 'sending') t.status = 'tracking'; });
                log(err.message, 'error');
                if (err.status === 409) {
                    pause();
                    FaceApp.setStatus(el.status, 'This session has ended.', 'error');
                } else if (err.status === 401 || err.status === 403) {
                    pause();
                    FaceApp.setStatus(el.status, err.message, 'error');
                }
            })
            .then(function () { sending = false; });
    }

    // ---------- manual overrides ----------

    function override(studentId, present) {
        return FaceApp.postJson(cfg.overrideUrl, { sessionId: cfg.sessionId, studentId: studentId, present: present })
            .then(function (res) {
                if (present) {
                    addPresent({ studentId: res.studentId, name: res.name, rollNumber: res.rollNumber, markedAt: res.markedAt, method: 'manual' }, true);
                    log(res.name + ' marked present manually.', 'ok');
                } else {
                    removePresent(res.studentId);
                    // Let the camera pick this student up again only if the teacher marks them present later.
                    tracker.tracks.forEach(function (t) { if (t.label && t.label.indexOf('(' + res.rollNumber + ')') > 0) t.status = 'manual_absent'; });
                    log(res.name + ' marked absent manually.', 'warn');
                }
                setPresentCount(res.presentCount);
            })
            .catch(function (err) { log(err.message, 'error'); });
    }

    el.presentList.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-action="absent"]');
        if (!btn) return;
        var id = parseInt(btn.closest('[data-student-id]').getAttribute('data-student-id'), 10);
        if (window.confirm('Mark this student absent?')) override(id, false);
    });

    el.absentList.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-action="present"]');
        if (!btn) return;
        var id = parseInt(btn.closest('[data-student-id]').getAttribute('data-student-id'), 10);
        btn.disabled = true;
        override(id, true).then(function () { btn.disabled = false; });
    });

    // ---------- periodic refresh (keeps the list right if another tab changed it) ----------

    function refresh() {
        fetch(cfg.statusUrl, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (res) { return res.ok ? res.json() : null; })
            .then(function (data) {
                if (!data) return;
                if (!data.active) {
                    window.location.href = cfg.detailUrl;
                    return;
                }
                var ids = {};
                data.present.forEach(function (p) {
                    ids[p.studentId] = true;
                    addPresent(p, false);
                });
                Array.prototype.slice.call(el.presentList.children).forEach(function (li) {
                    var id = li.getAttribute('data-student-id');
                    if (!ids[id]) removePresent(id);
                });
                setPresentCount(data.presentCount);
            })
            .catch(function () { /* offline for a moment; try again next time */ });
    }

    el.start.addEventListener('click', start);
    el.pause.addEventListener('click', pause);
    window.addEventListener('pagehide', function () { camera.stop(); });
    window.setInterval(refresh, cfg.refreshEveryMs);

    // Initial present list is rendered by the server into data attributes.
    Array.prototype.slice.call(document.querySelectorAll('#initialPresent [data-student-id]')).forEach(function (n) {
        addPresent({
            studentId: parseInt(n.getAttribute('data-student-id'), 10),
            name: n.getAttribute('data-name'),
            rollNumber: n.getAttribute('data-roll'),
            markedAt: n.getAttribute('data-time'),
            method: n.getAttribute('data-method')
        }, false);
    });
    updateAbsentCount();
})(window, document);
