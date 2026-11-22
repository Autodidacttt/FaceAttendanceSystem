# FaceAttend: face recognition student attendance

FaceAttend is an ASP.NET MVC 5 web app for taking class attendance with a webcam. Teachers start a session and point the camera at the class. Students who are recognised are marked present, and each student is counted once. Admins manage students, teachers and subjects. Students can see their own attendance.

Face detection and recognition run **in the browser** with [face-api.js](https://github.com/vladmandic/face-api) (models are served locally; nothing is sent to third parties). The server only receives 128-number face descriptors. It matches them against enrolled students and records attendance.

| | |
|---|---|
| Stack | ASP.NET MVC 5.2.9, .NET Framework 4.7.2, Entity Framework 6.4.4 (Code First), SQL Server LocalDB, Bootstrap 5, face-api.js 1.7.15 (@vladmandic) |
| Roles | Admin, Teacher, Student (one sign-in page; each role gets its own dashboard) |
| Tests | MSTest (37 unit tests) |

---

## Screenshots

> _Placeholders: add your own captures to `docs/screenshots/`._

| Screen | File |
|---|---|
| Sign in | `docs/screenshots/login.png` |
| Admin dashboard | `docs/screenshots/admin-dashboard.png` |
| Face enrollment | `docs/screenshots/enrollment.png` |
| Live attendance | `docs/screenshots/live-attendance.png` |
| Session summary | `docs/screenshots/session-detail.png` |
| Student dashboard | `docs/screenshots/student-dashboard.png` |

---

## Prerequisites

- Windows 10/11
- **Visual Studio 2022 or 2026** with the *ASP.NET and web development* workload. This includes IIS Express, SQL Server Express **LocalDB** and the .NET Framework 4.7.2 targeting pack.
- A webcam and a recent **Chrome, Edge or Firefox**.
- Internet access the first time you build, so NuGet can restore packages.

## First run

1. Clone the repository and open `FaceAttendanceSystem.slnx` in Visual Studio.
2. Build the solution (**Ctrl+Shift+B**). NuGet restores the packages into `packages/`.
3. Press **F5**. The site opens at **https://localhost:44357/**.
   If prompted, trust the IIS Express development certificate. The camera only works on HTTPS or localhost.
4. On the first request the app creates the `FaceAttendanceDB` database on `(LocalDB)\MSSQLLocalDB`. It applies the EF migrations and seeds demo data (`MigrateDatabaseToLatestVersion`).

To create the database by hand instead:

```powershell
cd FaceAttendanceSystem
..\packages\EntityFramework.6.4.4\tools\net45\any\ef6.exe database update `
  --assembly "$PWD\bin\FaceAttendanceSystem.dll" --project-dir "$PWD\" `
  --config "$PWD\Web.config" --data-dir "$PWD\App_Data" `
  --migrations-config FaceAttendanceSystem.Migrations.Configuration
```

Command-line build and tests (from the repository root):

```powershell
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe"
& $msbuild FaceAttendanceSystem.slnx -t:restore -p:RestorePackagesConfig=true
& $msbuild FaceAttendanceSystem.slnx -p:Configuration=Debug
$vstest = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -find "**\TestPlatform\vstest.console.exe"
& $vstest FaceAttendanceSystem.Tests\bin\Debug\FaceAttendanceSystem.Tests.dll
```

## Demo accounts

| Role | Username | Password |
|---|---|---|
| Admin | `admin` | `Admin@123` |
| Teacher | `teacher1` | `Teacher@123` |
| Student | `S001` | `Student@123` |

Students `S002`–`S005` also exist, with password `Student@123`. `teacher1` teaches CS101 and CS102. All the students are in *Computer Science, semester 1*.

Signing in takes every role straight to its dashboard; nobody is forced to change their password. Anyone can change theirs at any time from **Change password** in the account menu (top right). Debug builds show these credentials on the sign-in page; Release builds do not. Change or remove the demo accounts before any real use.

## A typical walkthrough

1. **Admin**: sign in as `admin`. Add students, teachers and subjects, then assign subjects to teachers (**Assignments**). New accounts get a random password that is shown once; give it to the user, who can change it later if they want.
2. **Enroll faces**: as admin (any student) or as a teacher (students in your classes), open **Face enrollment**, pick a student, start the camera and capture 5 samples.
3. **Teacher**: sign in as `teacher1`. Click **Start session** on a subject, then start the camera. Students look at the camera and blink or turn their head slightly. Recognised students appear in the present list straight away.
4. Correct anyone manually ("Mark present" / "Undo"), then **End session** to see the summary.
5. **Reports**: by subject, by student or by date range, with CSV export.
6. **Student**: sign in as `S001` to see overall and per-subject attendance. Anything below 75% is shown in red.

---

## How it works

### Rosters

A subject's **roster** is every *active* student whose department and semester match the subject's. Sessions, recognition candidates, absences and percentages are all based on the roster. If you change a student's semester, their history is recalculated against their new class.

### Face enrollment

- The browser detects faces with **TinyFaceDetector** and finds 68 landmarks. It then computes a 128-value **descriptor** with **FaceRecognitionNet**.
- A sample is captured only when **exactly one face** is in view, its detector score is **at least 0.6**, and the face is large enough (close to the camera). Samples are taken about 0.7 s apart, and 5 are needed.
- If a new sample is far (distance > 0.6) from the first one, the capture resets, because it probably shows a different person.
- `POST /Enrollment/Save` (AJAX with an anti-forgery header) validates every descriptor on the server. Each must be exactly 128 finite float32 values, base64-encoded, and each student can have at most 20 samples.
- If the new samples are very close (distance < 0.4) to **another** student's samples, the server warns that this may be a duplicate enrollment. The operator can confirm and save anyway.
- **No images are stored for enrollment**, only descriptors (512 bytes per sample).

### Live recognition

1. The page detects faces about 5 times a second and **tracks** each face across frames.
2. A face is sent to the server only after it passes the **liveness check** (see below).
3. About every 1.5 s, the page posts the descriptors of live, unresolved faces to `POST /Attendance/Recognize`.
4. The server loads the session's **candidates**: roster students with enrolled descriptors, cached in memory per session and refreshed when enrollments change. `FaceMatcher` then:
   - takes each student's distance as the **Euclidean distance to their closest sample**;
   - accepts the best student only if **distance ≤ threshold** (default 0.50) **and** the next-best *student* is at least **0.05 further away**, so that ambiguous matches are rejected.
5. Results come back as `marked`, `already`, `unknown`, `ambiguous` or `manual_absent`. Boxes are labelled green (present), red (unknown) or amber (still checking).

**Duplicate rule:** a unique index on `(ClassSessionId, StudentId)` and `AttendanceRules.DecideFaceMark` ensure one record per student per session. Recognising someone again returns `already`. A teacher's manual *absent* is never overridden by the camera. Face marking stops when the session ends.

**Optional snapshots:** if "Save a small face snapshot" is ticked, a JPEG of about 10 KB is stored in `App_Data/snapshots/<session>/<student>.jpg`. Only the session's teacher and admins can view it, and git ignores it.

### Tuning the match threshold

**Admin → Settings** controls the threshold (0.35–0.60):

| Threshold | Effect |
|---|---|
| 0.35–0.45 | Stricter: fewer wrong matches, but more "Unknown" results in poor light or at odd angles. |
| **0.50** (default) | A good balance for webcams in normal indoor light. |
| 0.55–0.60 | More forgiving, but a higher risk of marking the wrong student, especially for look-alikes. |

Tips: good, even lighting matters more than the threshold. Enroll in the same room and light where classes take place. If one student is often "Unknown", delete their samples and enroll again.

### Liveness limits

A face counts as live after **3 consecutive frames** plus natural movement: an **eye blink** (the eye aspect ratio drops at least 22%) **or** a **head turn** (the nose shifts relative to the eyes).

This stops the casual cheat of holding up a printed photo or a still image on a phone. It is a **deterrent, not real anti-spoofing**:

- A video of the student, or a photo that someone tilts or moves skilfully, can pass.
- The check runs in the browser, so a technically skilled user who controls the teacher's machine could bypass it.
- Very still students, or students with glasses that hide their eyes, may need to turn their head slightly.

For high-stakes use, keep a teacher present, keep manual review in place, or add depth/IR liveness hardware.

---

## Security

- **Passwords:** PBKDF2-HMAC-SHA256 with 100,000 iterations, a 16-byte random salt and a 32-byte hash, compared in constant time. Passwords generated for new accounts and resets are random and shown once. Changing a password is optional and available to every role (it needs the current password).
- **Lockout:** 5 failed sign-ins lock the account for 15 minutes. Unknown usernames cost the same hashing time.
- **Sessions:** forms authentication with an encrypted ticket and an HttpOnly cookie, 30-minute sliding expiry; Release builds require SSL. Every request re-checks the account against the database, so deactivated users and role changes take effect at once.
- **Authorisation:** every action requires sign-in. Role filters return **403** to signed-in users with the wrong role. Teachers can only reach their own subjects, sessions, students and reports.
- **CSRF:** every form post is validated, and AJAX posts send the token in the `RequestVerificationToken` header.
- **Audit log:** sign-ins, failures, lockouts, password changes and resets, all create/update/delete actions, enrollments, attendance overrides, settings changes and exports.
- **Headers:** `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `Permissions-Policy` (camera on this site only). The MVC, ASP.NET and X-Powered-By version headers are removed.
- **CSV exports** neutralise spreadsheet formulas (`=`, `+`, `-`, `@`).

## Project layout

```
FaceAttendanceSystem/
  Controllers/      Account, Admin, Students, Teachers, Subjects, Assignments, Settings, Audit,
                    Enrollment, Teacher, Session, Attendance (JSON), Student, Reports, Error
  Data/             AppDbContext (EF6, unique indexes)
  Migrations/       Initial migration + idempotent seed (Configuration.cs)
  Models/           Entities
  ViewModels/       Form and page models
  Services/         PasswordHasher, FaceMatcher, FaceDescriptor, FaceCandidateCache,
                    AttendanceService, AttendanceRules, ReportService, SettingsService, ...
  Infrastructure/   AppPrincipal, RoleAuthorize, AjaxValidateAntiForgeryToken, AccountStatusFilter,
                    AuditLogger, Csv, paging, view helpers
  Scripts/app/      common.js, camera.js, enroll.js, liveness.js, live-attendance.js
  Scripts/lib/      face-api.js (MIT)
  Content/face-models/  tinyFaceDetector, faceLandmark68Net, faceRecognitionNet weights
FaceAttendanceSystem.Tests/  MSTest: PasswordHasher, FaceMatcher, FaceDescriptor, duplicate rule, CSV
```

## Troubleshooting

| Problem | Fix |
|---|---|
| "The camera only works over HTTPS" | Open **https**://localhost:44357/, not the http port. Browsers block the camera on insecure origins. |
| Camera permission denied | Click the camera icon in the address bar and allow it, then reload. On Windows, also check *Settings → Privacy → Camera*. |
| "The camera is in use by another application" | Close Teams, Zoom or other camera apps (or other tabs using the camera). |
| Models take long to load / page is slow | The first load downloads about 7 MB of model weights (cached afterwards). Without WebGL the page falls back to the much slower CPU backend; enable hardware acceleration in the browser. |
| 404 for `/Content/face-models/*.bin` | IIS must serve `.bin` files. `Web.config` adds the MIME type; on full IIS, make sure *Static Content* is installed. |
| Students often "Unknown" | Improve lighting, re-enroll in the classroom, or raise the threshold slightly (e.g. 0.55). |
| Wrong student recognised | Lower the threshold (e.g. 0.45), re-enroll both students, and check the session in **History** to correct it. |
| Database errors on first run | Check that LocalDB works with `sqllocaldb info MSSQLLocalDB`, then create the database by hand (see *First run*). To start over, delete the database with `sqllocaldb` / SSMS and run again. |
| Demo password "invalid" | Someone may have changed it from *Change password*; ask an admin to reset it (the new password is shown once). After 5 failures the account locks for 15 minutes; a reset also clears the lock. |
| Opening http://localhost:60322 redirects to a dead https URL | The site forces HTTPS and IIS Express only listens on port 44357. Use https://localhost:44357/. |

## Known limitations

- Liveness is a heuristic (see *Liveness limits*).
- Rosters come from department + semester. There is no per-student subject enrolment (electives).
- A deactivated student's past records stay in history, but they no longer count towards roster sizes.
- Recognition relies on the teacher's browser. Very large classes are best handled a few students at a time in front of the camera.
- Dates are stored in UTC and shown in the server's local time zone.

## Licence

face-api.js is MIT-licensed (`Scripts/lib/face-api/LICENSE.txt`).
