using System;
using System.Data.Entity;
using System.Threading;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using FaceAttendanceSystem.Data;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            // Creates/upgrades the LocalDB database and runs the idempotent seed on first use.
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<AppDbContext, Migrations.Configuration>());

            MvcHandler.DisableMvcResponseHeader = true;
            // X-Frame-Options is sent for every response from Web.config, so don't add a second one.
            AntiForgeryConfig.SuppressXFrameOptionsHeader = true;

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        /// <summary>Turns the forms-auth ticket into an AppPrincipal (user id + role) for this request.</summary>
        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            var identity = Context.User?.Identity as FormsIdentity;
            if (identity == null || !identity.IsAuthenticated) return;

            int userId;
            UserRole role;
            string fullName;
            if (!AppPrincipal.TryParseUserData(identity.Ticket.UserData, out userId, out role, out fullName))
            {
                // Unknown/old ticket format: treat as anonymous and drop the cookie.
                FormsAuthentication.SignOut();
                Context.User = null;
                return;
            }

            var principal = new AppPrincipal(identity, userId, role, fullName);
            Context.User = principal;
            Thread.CurrentPrincipal = principal;
        }
    }
}
