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
- [x] Seed: admin / teacher1 / S001 (+ a few demo students and subjects), PBKDF2-hashed at seed time, MustChangePassword = true, threshold 0.5
- [x] `database update` creates and seeds LocalDB
- [x] Build ✔

## Phase 3 — Auth, infrastructure, layout
- [x] Services/PasswordHasher (PBKDF2-SHA256, 100k iterations, 16-byte salt, 32-byte hash, constant-time compare)
- [x] AccountController: login (one page, redirects by role), POST logout, change password, forced change on first login
- [x] Lockout: 15 min after 5 failed logins
- [x] FormsAuthenticationTicket (user id + role) → `Application_PostAuthenticateRequest` → GenericPrincipal
- [x] Infrastructure: RoleAuthorize (403 for a signed-in user with the wrong role), AjaxValidateAntiForgery (header token), AuditLogger, MustChangePassword filter
- [x] Global filters: Authorize, RequireHttps, HandleError
- [x] `_Layout` with role-aware navbar, `_ViewStart`, `site.css`, 403/404/500 pages
- [x] Build ✔

## Phase 4 — Admin
- [ ] Dashboard: totals, today's attendance %, 5 students with the lowest attendance
- [ ] CRUD Students / Teachers / Subjects (temp password shown once, MustChangePassword)
- [ ] Teacher↔Subject assignment
- [ ] Settings: match threshold 0.35–0.60
- [ ] Build ✔

## Phase 5 — Face enrollment
- [ ] face-api.js + models (tinyFaceDetector, faceLandmark68Net, faceRecognitionNet) stored locally
- [ ] Scripts/app/camera.js, enroll.js: live box, capture only when exactly 1 face with a good score, 5 samples
- [ ] EnrollmentController: page, `POST /Enrollment/Save` (AJAX + anti-forgery), count, delete (audited)
- [ ] Server-side descriptor validation (128 finite floats, ≤ 20 faces)
- [ ] Build ✔

## Phase 6 — Teacher: sessions and live attendance
- [ ] Teacher dashboard: my subjects, today's sessions
- [ ] SessionController: start (own subjects only), end, summary (present/absent)
- [ ] Services/FaceMatcher: Euclidean distance, threshold, margin ≥ 0.05 over second-best student
- [ ] Services/AttendanceService: candidate cache per session, duplicate rule, manual override (audited)
- [ ] `POST /Attendance/Recognize` → marked / already / unknown
- [ ] live-attendance.js: ~1.5 s loop, labelled overlay boxes, present list + counter, liveness (≥ 3 consecutive frames + blink/head-turn movement)
- [ ] Optional snapshot saved to `App_Data/snapshots/`
- [ ] History: filter by subject, date, roll no.; session detail
- [ ] Build ✔

## Phase 7 — Student portal
- [ ] Dashboard: overall % and per-subject % (< 75% in red), recent entries
- [ ] Profile: face enrolled yes/no (read-only)
- [ ] Build ✔

## Phase 8 — Reports
- [ ] Services/ReportService: by subject, by student, by date range
- [ ] CSV export
- [ ] Build ✔

## Phase 9 — Tests
- [ ] FaceAttendanceSystem.Tests (MSTest, net472, classic csproj) added to solution
- [ ] PasswordHasher, FaceMatcher, duplicate-attendance rule
- [ ] vstest.console passes ✔

## Phase 10 — Verify and document
- [ ] Clean Debug + Release build
- [ ] Smoke test on IIS Express (HTTPS): log in as each role, cross-role pages return 403
- [ ] README: prerequisites, first run, demo accounts, how enrollment and recognition work, threshold tuning, liveness limits, troubleshooting, screenshot placeholders

## Phase 11 — GitHub
- [ ] `git status` check: no secrets, .mdf files, snapshots or reference files
- [ ] `gh auth status` (gh must be installed + logged in by you)
- [ ] `gh repo create Autodidacttt/FaceAttendanceSystem --public --source . --remote origin --push` (never force-push)
- [ ] Summary: what works, manual tests, known limitations
