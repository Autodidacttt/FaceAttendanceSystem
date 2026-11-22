/* Shared helpers for the face pages: AJAX with anti-forgery header, model loading, descriptor encoding. */
(function (window) {
    'use strict';

    var FaceApp = window.FaceApp = window.FaceApp || {};

    /** Anti-forgery token rendered on the page by @Html.AntiForgeryToken(). */
    FaceApp.token = function () {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    };

    /** POST JSON; resolves with the parsed body, rejects with an Error carrying .status and .body. */
    FaceApp.postJson = function (url, data) {
        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                'RequestVerificationToken': FaceApp.token()
            },
            body: JSON.stringify(data)
        }).then(function (res) {
            return res.text().then(function (text) {
                var body = null;
                try { body = text ? JSON.parse(text) : null; } catch (e) { body = null; }
                if (!res.ok) {
                    var message = (body && (body.message || body.error)) || ('Request failed (HTTP ' + res.status + ')');
                    if (res.status === 401) message = 'Your session has expired. Please sign in again.';
                    var err = new Error(message);
                    err.status = res.status;
                    err.body = body;
                    throw err;
                }
                return body;
            });
        });
    };

    /** Float32Array(128) -> base64 of its little-endian bytes (what the server expects). */
    FaceApp.descriptorToBase64 = function (descriptor) {
        var f32 = descriptor instanceof Float32Array ? descriptor : new Float32Array(descriptor);
        var bytes = new Uint8Array(f32.buffer, f32.byteOffset, f32.byteLength);
        var binary = '';
        for (var i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
        return window.btoa(binary);
    };

    FaceApp.euclidean = function (a, b) {
        var sum = 0;
        for (var i = 0; i < a.length; i++) {
            var d = a[i] - b[i];
            sum += d * d;
        }
        return Math.sqrt(sum);
    };

    var modelsPromise = null;

    /** Loads the three face-api.js networks once per page (WebGL backend, CPU fallback). */
    FaceApp.loadModels = function (modelsUrl) {
        if (modelsPromise) return modelsPromise;
        if (!window.faceapi) return Promise.reject(new Error('face-api.js failed to load.'));

        modelsPromise = (async function () {
            var tf = window.faceapi.tf;
            try {
                var ok = await tf.setBackend('webgl');
                if (!ok) await tf.setBackend('cpu');
            } catch (e) {
                await tf.setBackend('cpu');
            }
            await tf.ready();
            await Promise.all([
                window.faceapi.nets.tinyFaceDetector.loadFromUri(modelsUrl),
                window.faceapi.nets.faceLandmark68Net.loadFromUri(modelsUrl),
                window.faceapi.nets.faceRecognitionNet.loadFromUri(modelsUrl)
            ]);
            return tf.getBackend();
        })();

        modelsPromise.catch(function () { modelsPromise = null; });
        return modelsPromise;
    };

    FaceApp.detectorOptions = function () {
        return new window.faceapi.TinyFaceDetectorOptions({ inputSize: 416, scoreThreshold: 0.5 });
    };

    FaceApp.escapeHtml = function (s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    };

    /** Sets a Bootstrap-coloured status line. kind: info | ok | warn | error */
    FaceApp.setStatus = function (el, text, kind) {
        if (!el) return;
        var cls = { info: 'text-muted', ok: 'text-success', warn: 'text-warning-emphasis', error: 'text-danger' }[kind || 'info'];
        el.className = 'camera-status small ' + cls;
        el.textContent = text;
    };
})(window);
