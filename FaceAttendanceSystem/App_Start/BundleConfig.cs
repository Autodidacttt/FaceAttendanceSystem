using System.Web.Optimization;

namespace FaceAttendanceSystem
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                "~/Scripts/jquery.validate.js",
                "~/Scripts/jquery.validate.unobtrusive.js"));

            // bootstrap.bundle includes Popper (needed for dropdowns).
            bundles.Add(new Bundle("~/bundles/bootstrap").Include(
                "~/Scripts/bootstrap.bundle.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                "~/Content/bootstrap.css",
                "~/Content/Site.css"));

            // face-api.js is large and already minified, so it is served as-is (no bundling).
            bundles.Add(new Bundle("~/bundles/face-enroll").Include(
                "~/Scripts/app/common.js",
                "~/Scripts/app/camera.js",
                "~/Scripts/app/enroll.js"));

            bundles.Add(new Bundle("~/bundles/face-live").Include(
                "~/Scripts/app/common.js",
                "~/Scripts/app/camera.js",
                "~/Scripts/app/liveness.js",
                "~/Scripts/app/live-attendance.js"));
        }
    }
}
