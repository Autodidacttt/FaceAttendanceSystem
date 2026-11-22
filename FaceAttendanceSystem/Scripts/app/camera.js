/* Webcam wrapper: starts/stops the camera and draws labelled boxes on a mirrored overlay canvas. */
(function (window) {
    'use strict';

    var FaceApp = window.FaceApp = window.FaceApp || {};

    function Camera(video, canvas) {
        this.video = video;
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        this.stream = null;
    }

    Camera.friendlyError = function (err) {
        var name = err && err.name;
        if (!window.isSecureContext) return 'The camera only works over HTTPS (or on localhost). Open the site with https://.';
        if (name === 'NotAllowedError' || name === 'SecurityError') return 'Camera permission was denied. Allow camera access in the browser\'s address bar and try again.';
        if (name === 'NotFoundError' || name === 'OverconstrainedError') return 'No camera was found. Connect a webcam and try again.';
        if (name === 'NotReadableError' || name === 'AbortError') return 'The camera is in use by another application. Close it and try again.';
        return (err && err.message) || 'The camera could not be started.';
    };

    Camera.prototype.start = async function () {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            throw new Error('This browser cannot access the camera. Use a recent Chrome, Edge or Firefox over HTTPS.');
        }

        this.stream = await navigator.mediaDevices.getUserMedia({
            video: { width: { ideal: 640 }, height: { ideal: 480 }, facingMode: 'user' },
            audio: false
        });

        var video = this.video;
        video.srcObject = this.stream;
        await new Promise(function (resolve) {
            if (video.readyState >= 1) return resolve();
            video.onloadedmetadata = function () { resolve(); };
        });
        await video.play();

        this.canvas.width = video.videoWidth || 640;
        this.canvas.height = video.videoHeight || 480;
    };

    Camera.prototype.stop = function () {
        if (this.stream) {
            this.stream.getTracks().forEach(function (t) { t.stop(); });
            this.stream = null;
        }
        this.video.srcObject = null;
        this.clear();
    };

    Camera.prototype.isRunning = function () {
        return !!this.stream;
    };

    Camera.prototype.clear = function () {
        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
    };

    /**
     * Draws a box (detector coordinates) with an optional label.
     * The video is mirrored with CSS, so x is mirrored here while text stays readable.
     */
    Camera.prototype.drawBox = function (box, color, label) {
        var ctx = this.ctx;
        var x = this.canvas.width - box.x - box.width;

        ctx.lineWidth = 3;
        ctx.strokeStyle = color;
        ctx.strokeRect(x, box.y, box.width, box.height);

        if (label) {
            ctx.font = '600 15px system-ui, -apple-system, "Segoe UI", sans-serif';
            var padding = 5;
            var textWidth = ctx.measureText(label).width;
            var labelY = box.y > 26 ? box.y - 24 : box.y + box.height + 2;
            ctx.fillStyle = color;
            ctx.fillRect(x - 1.5, labelY, textWidth + padding * 2, 22);
            ctx.fillStyle = '#fff';
            ctx.fillText(label, x - 1.5 + padding, labelY + 16);
        }
    };

    /** JPEG data URL of the face region (un-mirrored), for optional attendance snapshots. */
    Camera.prototype.captureFace = function (box, maxSize) {
        var size = maxSize || 160;
        var pad = 0.25;
        var sx = Math.max(0, box.x - box.width * pad);
        var sy = Math.max(0, box.y - box.height * pad);
        var sw = Math.min(this.video.videoWidth - sx, box.width * (1 + 2 * pad));
        var sh = Math.min(this.video.videoHeight - sy, box.height * (1 + 2 * pad));
        var scale = Math.min(1, size / Math.max(sw, sh));

        var c = document.createElement('canvas');
        c.width = Math.round(sw * scale);
        c.height = Math.round(sh * scale);
        c.getContext('2d').drawImage(this.video, sx, sy, sw, sh, 0, 0, c.width, c.height);
        return c.toDataURL('image/jpeg', 0.8);
    };

    FaceApp.Camera = Camera;
})(window);
