using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NominationSystem.Models;

namespace NominationSystem.Controllers
{
    [Authorize]
    public class VoteController : Controller
    {
        private readonly VotingRepository repo = new VotingRepository();

        // Session keys used between "Submit" and "Confirmation"
        private const string KeyReceipt = "Vote.ReceiptNo";
        private const string KeyReceiptAt = "Vote.ReceiptAt";
        private const string KeyElectionYear = "Vote.ElectionYear";

        // ================================================================ Ballot page
        // /Vote  or  /Vote/Index
        [HttpGet]
        [NoCache]
        public ActionResult Index()
        {
            BallotViewModel model;

            try
            {
                model = repo.GetBallot();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                model = new BallotViewModel();   // shows the "no active election" card
            }

            model.Voter = GetCurrentVoter();

            if (model.Voter == null) return ForceLogin();

            if (model.HasElection && model.Voter.VoterId.HasValue)
            {
                if (repo.HasVoted(model.ElectionId, model.Voter.VoterId.Value))
                {
                    // already voted: show the old receipt if still in session, otherwise a message
                    if (Session[KeyReceipt] != null) return RedirectToAction("Confirmation");
                    model.AlreadyVoted = true;
                }
            }

            return View(model);
        }

        // ================================================================ Submit (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [NoCache]
        public ActionResult Submit(int electionId, int[] candidateIds)
        {
            if (candidateIds == null || candidateIds.Length == 0 || candidateIds.Length > 100)
            {
                return Json(new { success = false, message = "কোনো ভোট নির্বাচন করা হয়নি।" });
            }

            try
            {
                var voter = GetCurrentVoter();

                if (voter == null || !voter.VoterId.HasValue)
                {
                    return Json(new { success = false, message = "লগইন সেশন শেষ হয়ে গেছে। আবার লগইন করুন।" });
                }

                var result = repo.Submit(electionId, voter.VoterId, candidateIds);

                if (!result.Success)
                {
                    return Json(new { success = false, message = result.Message });
                }

                Session[KeyReceipt] = result.ReceiptNo;
                Session[KeyReceiptAt] = result.SubmittedAt;

                return Json(new
                {
                    success = true,
                    redirectUrl = Url.Action("Confirmation", "Vote")
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return Json(new { success = false, message = "একটি সমস্যা হয়েছে। দয়া করে আবার চেষ্টা করুন।" });
            }
        }

        // ================================================================ Confirmation page
        [HttpGet]
        [NoCache]
        public ActionResult Confirmation()
        {
            string receipt = Session[KeyReceipt] as string;

            if (string.IsNullOrEmpty(receipt))
            {
                return RedirectToAction("Index");
            }

            DateTime at = Session[KeyReceiptAt] is DateTime ? (DateTime)Session[KeyReceiptAt] : DateTime.Now;

            ViewBag.ReceiptNo = receipt;
            ViewBag.SubmittedAt = at;
            var cv = GetCurrentVoter();

            if (cv == null) return ForceLogin();
            ViewBag.Voter = cv;

            return View();
        }

        // ================================================================ Candidate photo (cached by browser)
        [HttpGet]
        public ActionResult Photo(int id)
        {
            byte[] data;
            string contentType;

            if (!repo.GetPhoto(id, out data, out contentType))
            {
                return HttpNotFound();
            }

            contentType = (contentType ?? "").Trim().ToLowerInvariant();
            if (contentType != "image/png") contentType = "image/jpeg";

            Response.Cache.SetCacheability(HttpCacheability.Public);
            Response.Cache.SetMaxAge(TimeSpan.FromHours(12));
            Response.Cache.SetExpires(DateTime.UtcNow.AddHours(12));

            return File(data, contentType);
        }

        // ================================================================ Current voter
        // TODO (voter account module): replace this method with the real logged-in voter.
        //   - set VoterId to the real voter's id  -> double voting is blocked automatically
        //     (dbo.VoterParticipation has UNIQUE(ElectionId, VoterId)).
        //   - while VoterId is null the system runs in DEMO mode and the same browser can vote many times.
        private VoterInfo GetCurrentVoter()
        {
            var cu = AuthUser.Current;
            if (cu == null) return null;
            return new AuthRepository().GetVoterInfo(cu.UserId);
        }

        private ActionResult ForceLogin()
        {
            System.Web.Security.FormsAuthentication.SignOut();
            return RedirectToAction("Login", "Account", new { returnUrl = Request.RawUrl });
        }
    }
}
