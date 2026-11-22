using System.Globalization;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Services;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    [RoleAuthorize("Admin")]
    public class SettingsController : BaseController
    {
        public ActionResult Index() => RedirectToAction("Settings");

        [HttpGet]
        public ActionResult Settings()
        {
            return View(new SettingsViewModel { MatchThreshold = SettingsService.GetMatchThreshold(Db) });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Settings(SettingsViewModel model)
        {
            if (model.MatchThreshold < SettingsService.MinMatchThreshold ||
                model.MatchThreshold > SettingsService.MaxMatchThreshold)
            {
                ModelState.AddModelError(nameof(model.MatchThreshold), "The threshold must be between 0.35 and 0.60.");
            }
            if (!ModelState.IsValid) return View(model);

            var old = SettingsService.GetMatchThreshold(Db);
            SettingsService.SetMatchThreshold(Db, model.MatchThreshold);
            Audit("SettingChanged", "AppSetting", "MatchThreshold",
                string.Format(CultureInfo.InvariantCulture, "{0:0.00} -> {1:0.00}", old, model.MatchThreshold));
            Db.SaveChanges();
            SettingsService.InvalidateCache();

            Flash("Settings saved.");
            return RedirectToAction("Settings");
        }
    }
}
