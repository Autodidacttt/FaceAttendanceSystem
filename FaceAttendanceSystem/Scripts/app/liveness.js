/*
 * Face tracking + simple liveness heuristics for the live attendance page.
 *
 * Each detected face is matched to a "track" from the previous frames by box overlap (IoU).
 * A track counts as live once it has been seen in at least MIN_FRAMES consecutive frames AND
 * shows natural movement: an eye blink (eye aspect ratio dips) or a head turn (nose shifts
 * relative to the eyes). A printed photo or a phone held still shows neither.
 *
 * Limits: this is a deterrent, not anti-spoofing. A video replay of the student, or someone
 * moving a photo cleverly, can pass. See README "Liveness limits".
 */
(function (window) {
    'use strict';

    var FaceApp = window.FaceApp = window.FaceApp || {};

    var MIN_FRAMES = 3;          // consecutive frames before a face is trusted
    var IOU_MATCH = 0.3;         // box overlap needed to continue a track
    var TRACK_TIMEOUT_MS = 1500; // drop tracks not seen for this long
    var HISTORY = 25;            // per-track samples kept for movement checks
    var BLINK_DROP = 0.22;       // EAR must dip by 22% below the open-eye level
    var MIN_OPEN_EAR = 0.2;      // ignore blink checks when the eyes never look open
    var TURN_RANGE = 0.12;       // head-yaw ratio change needed for "turned"

    function iou(a, b) {
        var x1 = Math.max(a.x, b.x), y1 = Math.max(a.y, b.y);
        var x2 = Math.min(a.x + a.width, b.x + b.width), y2 = Math.min(a.y + a.height, b.y + b.height);
        var inter = Math.max(0, x2 - x1) * Math.max(0, y2 - y1);
        var union = a.width * a.height + b.width * b.height - inter;
        return union > 0 ? inter / union : 0;
    }

    function dist(p, q) {
        return Math.hypot(p.x - q.x, p.y - q.y);
    }

    /** Eye aspect ratio from the 6 eye landmarks (Soukupova & Cech). Small when the eye is closed. */
    function eyeAspectRatio(eye) {
        var horizontal = dist(eye[0], eye[3]);
        if (horizontal === 0) return 0;
        return (dist(eye[1], eye[5]) + dist(eye[2], eye[4])) / (2 * horizontal);
    }

    function centre(points) {
        var x = 0, y = 0;
        points.forEach(function (p) { x += p.x; y += p.y; });
        return { x: x / points.length, y: y / points.length };
    }

    /** Horizontal nose offset from the eye midpoint, relative to eye distance (~0 when facing the camera). */
    function yawRatio(landmarks) {
        var left = centre(landmarks.getLeftEye());
        var right = centre(landmarks.getRightEye());
        var nose = landmarks.getNose()[3]; // nose tip area
        var eyeDistance = dist(left, right);
        if (eyeDistance === 0) return 0;
        return (nose.x - (left.x + right.x) / 2) / eyeDistance;
    }

    var nextId = 1;

    function Tracker() {
        this.tracks = [];
    }

    /**
     * Updates tracks with this frame's detections (face-api results with landmarks + descriptors).
     * Returns the list of tracks seen in this frame.
     */
    Tracker.prototype.update = function (detections, now) {
        var self = this;
        var unmatched = this.tracks.slice();
        var seen = [];

        detections.forEach(function (det) {
            var box = det.detection.box;
            var best = null, bestIou = IOU_MATCH;
            unmatched.forEach(function (t) {
                var o = iou(t.box, box);
                if (o >= bestIou) { bestIou = o; best = t; }
            });

            var track;
            if (best) {
                track = best;
                unmatched.splice(unmatched.indexOf(best), 1);
                track.frames = (now - track.lastSeen <= TRACK_TIMEOUT_MS) ? track.frames + 1 : 1;
            } else {
                track = {
                    id: 't' + (nextId++),
                    frames: 1,
                    ear: [],
                    yaw: [],
                    blinked: false,
                    turned: false,
                    status: 'tracking',  // tracking | waiting | sending | marked | already | unknown | ambiguous | manual_absent
                    label: '',
                    lastSent: 0,
                    attempts: 0
                };
                self.tracks.push(track);
            }

            track.box = box;
            track.score = det.detection.score;
            track.descriptor = det.descriptor;
            track.lastSeen = now;

            var lm = det.landmarks;
            var ear = (eyeAspectRatio(lm.getLeftEye()) + eyeAspectRatio(lm.getRightEye())) / 2;
            track.ear.push(ear);
            track.yaw.push(yawRatio(lm));
            if (track.ear.length > HISTORY) track.ear.shift();
            if (track.yaw.length > HISTORY) track.yaw.shift();

            if (!track.blinked) {
                var open = Math.max.apply(null, track.ear);
                if (open >= MIN_OPEN_EAR && ear <= open * (1 - BLINK_DROP)) track.blinked = true;
            }
            if (!track.turned) {
                var range = Math.max.apply(null, track.yaw) - Math.min.apply(null, track.yaw);
                if (range >= TURN_RANGE) track.turned = true;
            }

            seen.push(track);
        });

        // Forget faces that left the frame.
        this.tracks = this.tracks.filter(function (t) { return now - t.lastSeen <= TRACK_TIMEOUT_MS; });
        return seen;
    };

    Tracker.isLive = function (track) {
        return track.frames >= MIN_FRAMES && (track.blinked || track.turned);
    };

    Tracker.MIN_FRAMES = MIN_FRAMES;
    FaceApp.Tracker = Tracker;
})(window);
