/*
 * Face enrollment page.
 * Captures N descriptor samples, only when exactly one clear face is in view, then posts them to the server.
 * No images leave the browser - only the 128-number descriptors.
 */
(function (window, document) {
    'use strict';

    var root = document.getElementById('enroll-app');
    if (!root || !window.FaceApp) return;

    var FaceApp = window.FaceApp;
    var cfg = {
        studentId: parseInt(root.getAttribute('data-student-id'), 10),
        saveUrl: root.getAttribute('data-save-url'),
        modelsUrl: root.getAttribute('data-models-url'),
        required: parseInt(root.getAttribute('data-required-samples'), 10) || 5,
        minScore: 0.6,          // detector confidence needed for a sample (TinyFaceDetector scores good faces ~0.6-0.9)
        minFaceFraction: 0.18,  // face box width / frame width (too small = too far away)
        captureGapMs: 700,      // time between samples, so they differ a little
        maxSpread: 0.6          // samples further apart than this are probably different people
    };

    var el = {
        video: document.getElementById('enrollVideo'),
        canvas: document.getElementById('enrollCanvas'),
        status: document.getElementById('enrollStatus'),
        hint: document.getElementById('enrollHint'),
        start: document.getElementById('btnStart'),
        capture: document.getElementById('btnCapture'),
        reset: document.getElementById('btnReset'),
        save: document.getElementById('btnSave'),
        stop: document.getElementById('btnStop'),
        dots: document.getElementById('sampleDots'),
        existing: document.getElementById('existingCount'),
        result: document.getElementById('enrollResult')
    };

    var camera = new FaceApp.Camera(el.video, el.canvas);
    var running = false, capturing = false, busy = false, saving = false;
    var lastRun = 0, lastCapture = 0;
    var samples = [];

    function renderDots() {
        var html = '';
        for (var i = 0; i < cfg.required; i++) {
            html += '<span class="' + (i < samples.length ? 'done' : '') + '"></span>';
        }
        el.dots.innerHTML = html;
        el.dots.setAttribute('aria-label', samples.length + ' of ' + cfg.required + ' samples captured');
        el.save.disabled = samples.length < cfg.required || saving;
        el.reset.disabled = samples.length === 0 && !capturing;
    }

    function showResult(text, kind) {
        el.result.className = 'alert mt-3 alert-' + (kind === 'ok' ? 'success' : kind === 'error' ? 'danger' : 'info');
        el.result.textContent = text;
        el.result.hidden = false;
    }

    async function start() {
        el.start.disabled = true;
        el.result.hidden = true;
        try {
            FaceApp.setStatus(el.status, 'Loading face recognition models (first time can take a few seconds)…');
            var backend = await FaceApp.loadModels(cfg.modelsUrl);
            FaceApp.setStatus(el.status, 'Starting camera…');
            await camera.start();
            running = true;
            el.capture.disabled = false;
            el.stop.disabled = false;
            FaceApp.setStatus(el.status, 'Camera running (' + backend + ').', 'ok');
            window.requestAnimationFrame(loop);
        } catch (err) {
            console.error(err);
            FaceApp.setStatus(el.status, FaceApp.Camera.friendlyError(err), 'error');
            el.start.disabled = false;
        }
    }

    function stop() {
        running = false;
        capturing = false;
        camera.stop();
        el.start.disabled = false;
        el.capture.disabled = true;
        el.stop.disabled = true;
        FaceApp.setStatus(el.status, 'Camera stopped.');
        FaceApp.setStatus(el.hint, '');
        renderDots();
    }

    async function loop(now) {
        if (!running) return;
        if (!busy && now - lastRun >= 150) {
            busy = true;
            lastRun = now;
            try {
                await detect();
            } catch (err) {
                console.error(err);
            }
            busy = false;
        }
        window.requestAnimationFrame(loop);
    }

    async function detect() {
        var results = await window.faceapi
            .detectAllFaces(el.video, FaceApp.detectorOptions())
            .withFaceLandmarks()
            .withFaceDescriptors();
        if (!running) return;

        camera.clear();

        if (results.length === 0) {
            FaceApp.setStatus(el.hint, 'No face detected. Look straight at the camera.', 'warn');
            return;
        }

        if (results.length > 1) {
            results.forEach(function (r) { camera.drawBox(r.detection.box, '#dc3545', 'One person only'); });
            FaceApp.setStatus(el.hint, 'More than one face in view. Only the student being enrolled should be in frame.', 'warn');
            return;
        }

        var r = results[0];
        var box = r.detection.box;
        var score = r.detection.score;
        var bigEnough = box.width >= el.video.videoWidth * cfg.minFaceFraction;
        var good = score >= cfg.minScore && bigEnough;

        camera.drawBox(box, good ? '#198754' : '#ffc107', Math.round(score * 100) + '%');

        if (!good) {
            FaceApp.setStatus(el.hint, !bigEnough
                ? 'Move a little closer to the camera.'
                : 'Face not clear enough. Face the light and hold still.', 'warn');
            return;
        }

        if (!capturing) {
            FaceApp.setStatus(el.hint, samples.length >= cfg.required
                ? 'All samples captured. Click "Save enrollment".'
                : 'Face detected. Click "Capture samples" to begin.', 'ok');
            return;
        }

        FaceApp.setStatus(el.hint, 'Hold still… capturing (turn your head slightly between samples).', 'ok');

        if (Date.now() - lastCapture < cfg.captureGapMs) return;

        if (samples.length > 0 && FaceApp.euclidean(samples[0].descriptor, r.descriptor) > cfg.maxSpread) {
            samples = [];
            renderDots();
            FaceApp.setStatus(el.hint, 'That looks like a different face, so the samples were reset.', 'error');
            return;
        }

        samples.push({ descriptor: r.descriptor, score: score });
        lastCapture = Date.now();
        renderDots();

        if (samples.length >= cfg.required) {
            capturing = false;
            el.capture.disabled = false;
            FaceApp.setStatus(el.hint, 'All samples captured. Click "Save enrollment".', 'ok');
        }
    }

    function beginCapture() {
        if (!running) return;
        samples = [];
        capturing = true;
        lastCapture = 0;
        el.capture.disabled = true;
        el.result.hidden = true;
        renderDots();
    }

    function reset() {
        samples = [];
        capturing = false;
        el.capture.disabled = !running;
        renderDots();
    }

    function save(force) {
        if (samples.length < cfg.required || saving) return Promise.resolve();
        saving = true;
        renderDots();

        var payload = {
            studentId: cfg.studentId,
            force: !!force,
            samples: samples.map(function (s) {
                return { descriptor: FaceApp.descriptorToBase64(s.descriptor), score: Math.round(s.score * 10000) / 10000 };
            })
        };

        return FaceApp.postJson(cfg.saveUrl, payload)
            .then(function (res) {
                samples = [];
                el.existing.textContent = res.total;
                showResult('Saved ' + res.added + ' samples. This student now has ' + res.total + ' face samples and can be recognised.', 'ok');
            })
            .catch(function (err) {
                if (err.status === 409 && err.body && err.body.duplicate && !force) {
                    if (window.confirm(err.message + '\n\nSave these samples anyway?')) {
                        saving = false;
                        return save(true);
                    }
                }
                showResult(err.message, 'error');
            })
            .then(function () {
                saving = false;
                renderDots();
            });
    }

    el.start.addEventListener('click', start);
    el.stop.addEventListener('click', stop);
    el.capture.addEventListener('click', beginCapture);
    el.reset.addEventListener('click', reset);
    el.save.addEventListener('click', function () { save(false); });
    window.addEventListener('pagehide', function () { camera.stop(); });

    renderDots();
})(window, document);
