using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using NominationSystem.Models;

namespace NominationSystem.Controllers
{
    /// <summary>
    /// Admin pages and JSON must never come from the browser cache,
    /// otherwise the list shows an old status after Approve / Reject.
    /// </summary>
    public class NoCacheAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            var cache = filterContext.HttpContext.Response.Cache;
            cache.SetCacheability(HttpCacheability.NoCache);
            cache.SetNoStore();
            cache.SetExpires(DateTime.UtcNow.AddYears(-1));
            cache.SetValidUntilExpires(false);
            cache.SetRevalidation(HttpCacheRevalidation.AllCaches);

            base.OnResultExecuting(filterContext);
        }
    }

    [NoCache]
    public class AdminController : Controller
    {
        private readonly AdminNominationRepository repo = new AdminNominationRepository();

        // /Admin  ->  dashboard
        public ActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        // ---------------------------------------------------------------- Dashboard (empty for now)
        [HttpGet]
        public ActionResult Dashboard()
        {
            return View();
        }

        // ---------------------------------------------------------------- List page
        [HttpGet]
        public ActionResult Nominations()
        {
            ViewBag.Posts = Bn.Posts;
            return View();
        }

        // JSON for infinite scroll (10 rows per call), search, sort, filter
        [HttpGet]
        public ActionResult NominationList(string q, string post, string status, string date,
                                           string sort, string dir, int skip = 0, int take = 10)
        {
            if (take > 200) take = 200;   // list endpoint is page-by-page only
            var query = BuildQuery(q, post, status, date, sort, dir, skip, take);

            Response.Cache.SetCacheability(
    System.Web.HttpCacheability.NoCache
);

            Response.Cache.SetNoStore();
            Response.Cache.SetRevalidation(
                System.Web.HttpCacheRevalidation.AllCaches
            );

            try
            {
                var page = repo.GetPage(query);

                var items = page.Items.Select(x => new
                {
                    id = x.NominationId,
                    applicationNo = x.ApplicationNo,
                    dateText = Bn.SubmittedText(x.ApplicationDate, x.CreatedAt),
                    name = x.CandidateName,
                    memberNo = x.MemberNo,
                    designation = x.Designation,
                    organizationId = x.OrganizationId,
                    mobile = x.Mobile,
                    email = x.Email,
                    post = x.DesiredPost,
                    status = x.Status,
                    statusText = NominationStatus.Text(x.Status)
                }).ToList();

                return Json(new { success = true, total = page.Total, items = items },
                            JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                Response.StatusCode = 500;
                return Json(new { success = false, message = "তালিকা লোড করা যায়নি।" },
                            JsonRequestBehavior.AllowGet);
            }
        }

        // ---------------------------------------------------------------- View page
        [HttpGet]
        public ActionResult Details(int id)
        {
            var model = repo.GetDetail(id);
            if (model == null) return HttpNotFound();
            return View(model);
        }

        // ---------------------------------------------------------------- Approve / Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int id, string status)
        {
            if (status != NominationStatus.Approved && status != NominationStatus.Rejected)
            {
                return Json(new { success = false, message = "অবৈধ স্ট্যাটাস।" });
            }

            try
            {
                // No login system yet, so a fixed name is stored.
                bool ok = repo.UpdateStatus(id, status, "Super Admin");
                if (!ok)
                {
                    return Json(new { success = false, message = "আবেদনটি পাওয়া যায়নি।" });
                }

                return Json(new
                {
                    success = true,
                    status = status,
                    statusText = NominationStatus.Text(status),
                    detailText = NominationStatus.DetailText(status),
                    cssClass = NominationStatus.CssClass(status)
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return Json(new { success = false, message = "স্ট্যাটাস আপডেট করা যায়নি। আবার চেষ্টা করুন।" });
            }
        }

        // ---------------------------------------------------------------- Print (new window)
        [HttpGet]
        public ActionResult Print(int id)
        {
            var model = repo.GetDetail(id);
            if (model == null) return HttpNotFound();
            return View(model);
        }

        // Summary print of the currently filtered list
        [HttpGet]
        public ActionResult PrintSummary(string q, string post, string status, string date,
                                         string sort, string dir)
        {
            var query = BuildQuery(q, post, status, date, sort, dir, 0, 100000);
            var page = repo.GetPage(query);

            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(query.Search)) parts.Add("সার্চ: " + query.Search);
            if (!string.IsNullOrEmpty(query.Post)) parts.Add("পদ: " + query.Post);
            if (!string.IsNullOrEmpty(query.Status)) parts.Add("স্ট্যাটাস: " + NominationStatus.Text(query.Status));
            if (query.Date.HasValue) parts.Add("তারিখ: " + Bn.DateText(query.Date.Value));
            ViewBag.FilterText = parts.Count == 0 ? "সকল আবেদন" : string.Join(" | ", parts);

            return View(page);
        }

        // ---------------------------------------------------------------- CSV export
        [HttpGet]
        public ActionResult ExportCsv(string q, string post, string status, string date,
                                      string sort, string dir)
        {
            var query = BuildQuery(q, post, status, date, sort, dir, 0, 100000);
            var page = repo.GetPage(query);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", new[]
            {
                "ক্রমিক", "আবেদন নং", "আবেদনের তারিখ", "প্রার্থীর নাম", "সদস্য নং",
                "সংস্থার আইডি", "সংস্থার পদবী", "মোবাইল", "ই-মেইল", "প্রার্থিতার পদ", "স্ট্যাটাস"
            }.Select(Csv)));

            int n = 1;
            foreach (var x in page.Items)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    n.ToString(),
                    x.ApplicationNo,
                    x.ApplicationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    x.CandidateName,
                    x.MemberNo,
                    x.OrganizationId,
                    x.Designation,
                    x.Mobile,
                    x.Email,
                    x.DesiredPost,
                    NominationStatus.Text(x.Status)
                }.Select(Csv)));
                n++;
            }

            // UTF-8 with BOM so Excel shows Bangla correctly.
            var bytes = new UTF8Encoding(true).GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();

            string fileName = "nominations-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".csv";
            return File(bytes, "text/csv", fileName);
        }

        // ================================================================ helpers
        private static NominationListQuery BuildQuery(string q, string post, string status, string date,
                                                      string sort, string dir, int skip, int take)
        {
            var query = new NominationListQuery();

            q = (q ?? "").Trim();
            if (q.Length > 100) q = q.Substring(0, 100);
            query.Search = q;

            query.Post = Bn.Posts.Contains(post ?? "") ? post : null;
            query.Status = NominationStatus.IsValid(status) ? status : null;

            DateTime d;
            if (DateTime.TryParseExact(date ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out d))
            {
                query.Date = d;
            }

            query.Sort = string.IsNullOrEmpty(sort) ? "date" : sort;
            query.Dir = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
            query.Skip = skip < 0 ? 0 : skip;
            query.Take = take < 1 ? 10 : (take > 100000 ? 100000 : take);

            return query;
        }

        // Escape a CSV cell (+ protect against Excel formula injection).
        private static string Csv(string value)
        {
            value = value ?? "";
            if (value.Length > 0 && (value[0] == '=' || value[0] == '+' || value[0] == '-' || value[0] == '@'))
            {
                value = "'" + value;
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
