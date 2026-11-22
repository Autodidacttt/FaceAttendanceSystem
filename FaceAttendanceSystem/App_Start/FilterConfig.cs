using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;

namespace FaceAttendanceSystem
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new RequireHttpsAttribute());
            // Every action requires a signed-in user unless marked [AllowAnonymous].
            filters.Add(new RoleAuthorizeAttribute());
            filters.Add(new AccountStatusFilter());
        }
    }
}
