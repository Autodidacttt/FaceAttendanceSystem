using System.Web.Mvc;

namespace FaceAttendanceSystem.Controllers
{
    /// <summary>Entry point: sends each signed-in user to the dashboard for their role.</summary>
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");
            if (User.IsInRole("Teacher")) return RedirectToAction("Dashboard", "Teacher");
            if (User.IsInRole("Student")) return RedirectToAction("Dashboard", "Student");
            return RedirectToAction("Login", "Account");
        }
    }
}
