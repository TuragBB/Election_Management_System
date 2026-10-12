using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NominationSystem.Models;

namespace NominationSystem.Controllers
{
    /// <summary>
    /// Shudhu login kora SODOSSO (Member account) dhukte parbe.
    ///  - login na korle  -> Login page
    ///  - login ase kintu Voter account -> "sodosso verify" page
    /// </summary>
    public class MemberOnlyAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (!base.AuthorizeCore(httpContext)) return false;

            CurrentUser u = AuthUser.From(httpContext.User);
            return u != null && u.IsMember;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new
                {
                    controller = "Account",
                    action = "MemberRequired",
                    returnUrl = filterContext.HttpContext.Request.RawUrl
                }));
                return;
            }

            base.HandleUnauthorizedRequest(filterContext);
        }
    }
}
