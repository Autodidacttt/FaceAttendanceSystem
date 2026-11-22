# PROGRESS — Face Recognition Student Attendance System

ASP.NET MVC 5.2.9 · .NET Framework 4.7.2 · EF6 Code First · LocalDB `FaceAttendanceDB` · face-api.js (@vladmandic) · Bootstrap 5

Build after every phase with VS 2026 MSBuild (found via vswhere). Commit after every phase (local only until Phase 11).

## Phase 0 — Prep
- [x] `git init -b main`, `.gitignore` (VS template + `_reference/`, `named _reference/`, `App_Data/snapshots/`, `*.mdf`, `*.ldf`, `*.user`)
- [x] PROGRESS.md

## Phase 1 — Scaffold
- [x] Add EntityFramework 6.4.4 (packages.config) and restore via MSBuild
- [x] Remove template Home/About/Contact. Create folders: Data, Models, ViewModels, Services, Infrastructure, Scripts/app
- [x] Web.config: `FaceAttendanceDB` connection string (Integrated Security), forms auth (HttpOnly, sliding 30 min), customErrors. Release transform: `requireSSL`, customErrors On
- [x] Build ✔

## Phase 2 — Data layer
- [x] Entities: User, Student, Teacher, Subject, TeacherSubject, FaceEnrollment, ClassSession, Attendance, AuditLog, AppSetting (threshold)
- [x] User also gets `FailedLoginCount` + `LockoutEndUtc` (needed for lockout)
- [x] AppDbContext with unique indexes (Username, Email, RollNumber, EmployeeNumber, Subject.Code, Attendance(ClassSessionId, StudentId))
- [x] Migrations/Configuration + `Initial` migration (ef6.exe)
- [x] Seed: admin / teacher1 / S001 (+ a few demo students and subjects), PBKDF2-hashed at seed time, MustChangePassword = false (forced change removed later), threshold 0.5
- [x] `database update` creates and seeds LocalDB
- [x] Build ✔

## Phase 3 — Auth, infrastructure, layout
- [x] Services/PasswordHasher (PBKDF2-SHA256, 100k iterations, 16-byte salt, 32-byte hash, constant-time compare)
- [x] AccountController: login (one page, redirects by role), POST logout, change password (voluntary; the forced first-login change was removed)
- [x] Lockout: 15 min after 5 failed logins
- [x] FormsAuthenticationTicket (user id + role) → `Application_PostAuthenticateRequest` → GenericPrincipal
- [x] Infrastructure: RoleAuthorize (403 for a signed-in user with the wrong role), AjaxValidateAntiForgery (header token), AuditLogger, account-status filter (signs out deactivated accounts)
- [x] Global filters: Authorize, RequireHttps, HandleError
- [x] `_Layout` with role-aware navbar, `_ViewStart`, `site.css`, 403/404/500 pages
- [x] Build ✔

## Phase 4 — Admin
- [x] Dashboard: totals, today's attendance %, 5 students with the lowest attendance
- [x] CRUD Students / Teachers / Subjects (random password shown once, no forced change)
- [x] Teacher↔Subject assignment
- [x] Settings: match threshold 0.35–0.60
- [x] Build ✔

## Phase 5 — Face enrollment
- [x] face-api.js + models (tinyFaceDetector, faceLandmark68Net, faceRecognitionNet) stored locally
- [x] Scripts/app/camera.js, enroll.js: live box, capture only when exactly 1 face with a good score, 5 samples
- [x] EnrollmentController: page, `POST /Enrollment/Save` (AJAX + anti-forgery), count, delete (audited)
- [x] Server-side descriptor validation (128 finite floats, ≤ 20 faces)
- [x] Build ✔

## Phase 6 — Teacher: sessions and live attendance
- [x] Teacher dashboard: my subjects, today's sessions
- [x] SessionController: start (own subjects only), end, summary (present/absent)
- [x] Services/FaceMatcher: Euclidean distance, threshold, margin ≥ 0.05 over second-best student
- [x] Services/AttendanceService: candidate cache per session, duplicate rule, manual override (audited)
- [x] `POST /Attendance/Recognize` → marked / already / unknown
- [x] live-attendance.js: ~1.5 s loop, labelled overlay boxes, present list + counter, liveness (≥ 3 consecutive frames + blink/head-turn movement)
- [x] Optional snapshot saved to `App_Data/snapshots/`
- [x] History: filter by subject, date, roll no.; session detail
- [x] Build ✔

## Phase 7 — Student portal
- [x] Dashboard: overall % and per-subject % (< 75% in red), recent entries
- [x] Profile: face enrolled yes/no (read-only)
- [x] Build ✔

## Phase 8 — Reports
- [x] Services/ReportService: by subject, by student, by date range
- [x] CSV export
- [x] Build ✔

## Phase 9 — Tests
- [x] FaceAttendanceSystem.Tests (MSTest, net472, classic csproj) added to solution
- [x] PasswordHasher, FaceMatcher, duplicate-attendance rule
- [x] vstest.console passes ✔ (37 tests)

## Phase 10 — Verify and document
- [x] Clean Debug + Release build
- [x] Smoke test on IIS Express (HTTPS): log in as each role, cross-role pages return 403
- [x] README: prerequisites, first run, demo accounts, how enrollment and recognition work, threshold tuning, liveness limits, troubleshooting, screenshot placeholders
- [x] Extra: headless-Edge run with a fake camera: real enroll → live recognition, unknown face stays unknown, still photo blocked by liveness

## Phase 11 — GitHub
- [x] `git status` check: no secrets, .mdf files, snapshots or reference files
- [ ] `gh auth status` (gh must be installed + logged in by you) — **blocked 2026-11-22: gh reports "not logged into any GitHub hosts" from Claude's shells**
- [ ] `gh repo create Autodidacttt/FaceAttendanceSystem --public --source . --remote origin --push` (never force-push)
- [ ] Summary: what works, manual tests, known limitations

## Change log
- 2026-11-22: removed the forced password change. Nobody is redirected after sign-in; seed, new accounts and resets use MustChangePassword = false; migration `NoForcedPasswordChange` clears the flag on existing rows. The Change password page stays in the account menu for every role.
