using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.SqlClient;
using NominationSystem.Models;

namespace NominationSystem.Controllers
{
    [MemberOnly]
    public class NominationController : Controller
    {
        private readonly NominationRepository repository = new NominationRepository();

        private static readonly string[] AllowedPosts =
        {
            "সভাপতি",
            "সহ-সভাপতি",
            "সাধারণ সম্পাদক",
            "যুগ্ম সম্পাদক",
            "কোষাধ্যক্ষ",
            "সহকারী কোষাধ্যক্ষ",
            "সম্মানিত সদস্য"
        };

        [HttpGet]
        public ActionResult Index()
        {
            var m = new NominationViewModel { ApplicationDate = DateTime.Today };
            var me = AuthUser.Current;
            var u = new AuthRepository().GetById(me.UserId);
            if (u != null)
            {
                m.CandidateName = u.FullName;
                m.MemberNo = u.MemberNo;
                m.Mobile = u.Mobile;
                m.Email = u.Email;
            }
            return View(m);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(NominationViewModel model)
        {
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            model.ApplicationDate = DateTime.Today;

            var meUser = new AuthRepository().GetById(AuthUser.Current.UserId);
            if (meUser == null || string.IsNullOrEmpty(meUser.MemberNo))
            {
                return RedirectToAction("MemberRequired", "Account");
            }
            model.MemberNo = meUser.MemberNo;
            ModelState.Remove("MemberNo");

            TrimModelStrings(model);

            if (!AllowedPosts.Contains(model.DesiredPost))
            {
                ModelState.AddModelError(
                    "DesiredPost",
                    "সঠিক নির্বাচনী পদ নির্বাচন করুন।"
                );
            }

            if (model.DateOfBirth.HasValue &&
                model.DateOfBirth.Value.Date >= DateTime.Today)
            {
                ModelState.AddModelError(
                    "DateOfBirth",
                    "জন্মতারিখ আজকের বা ভবিষ্যতের হতে পারবে না।"
                );
            }

            if (model.MembershipDate.HasValue &&
                model.MembershipDate.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    "MembershipDate",
                    "সদস্য হওয়ার তারিখ ভবিষ্যতের হতে পারে না।"
                );
            }

            if (model.ProposerDate.HasValue &&
                model.ProposerDate.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    "ProposerDate",
                    "প্রস্তাবকারীর তারিখ ভবিষ্যতের হতে পারে না।"
                );
            }

            if (model.SupporterDate.HasValue &&
                model.SupporterDate.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    "SupporterDate",
                    "সমর্থনকারীর তারিখ ভবিষ্যতের হতে পারে না।"
                );
            }

            ValidatePhoto(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                model.ApplicationNo =
                    "SWF-2027-" +
                    Guid.NewGuid()
                        .ToString("N")
                        .Substring(0, 10)
                        .ToUpperInvariant();

                int id = repository.Insert(model);

                Session["SubmittedNominationId"] = id;

                TempData["NominationId"] = id;
                TempData["ApplicationNo"] = model.ApplicationNo;
                TempData["ApplicationDate"] = model.ApplicationDate;

                return RedirectToAction("Confirmation");
            }
            catch (SqlException ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());

                ModelState.AddModelError(
                    "",
                    "ডেটাবেজে তথ্য সংরক্ষণ করা যায়নি। দয়া করে আবার চেষ্টা করুন।"
                );

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());

                ModelState.AddModelError(
                    "",
                    "একটি সমস্যা হয়েছে। দয়া করে আবার চেষ্টা করুন।"
                );

                return View(model);
            }
        }

        [HttpGet]
        public ActionResult Confirmation()
        {
            if (TempData["NominationId"] == null ||
                TempData["ApplicationNo"] == null)
            {
                return RedirectToAction("Index");
            }

            int id = Convert.ToInt32(
                TempData["NominationId"]
            );

            string appNo = Convert.ToString(
                TempData["ApplicationNo"]
            );

            DateTime applicationDate = DateTime.Today;

            if (TempData["ApplicationDate"] is DateTime)
            {
                applicationDate =
                    (DateTime)TempData["ApplicationDate"];
            }

            ViewBag.NominationId = id;
            ViewBag.ApplicationNo = appNo;
            ViewBag.ApplicationDate = applicationDate;

            TempData.Keep("NominationId");
            TempData.Keep("ApplicationNo");
            TempData.Keep("ApplicationDate");

            return View();
        }

        private void ValidatePhoto(NominationViewModel model)
        {
            if (model.Photo == null ||
                model.Photo.ContentLength <= 0)
            {
                return;
            }

            const int maxBytes = 2 * 1024 * 1024;

            string extension =
                System.IO.Path.GetExtension(
                    model.Photo.FileName
                );

            string[] allowedExtensions =
            {
                ".jpg",
                ".jpeg",
                ".png"
            };

            if (string.IsNullOrWhiteSpace(extension) ||
                !allowedExtensions.Contains(
                    extension.ToLowerInvariant()
                ))
            {
                ModelState.AddModelError(
                    "Photo",
                    "শুধুমাত্র JPG, JPEG অথবা PNG ছবি আপলোড করুন।"
                );

                return;
            }

            if (model.Photo.ContentLength > maxBytes)
            {
                ModelState.AddModelError(
                    "Photo",
                    "ছবির আকার সর্বোচ্চ ২ MB হতে পারবে।"
                );

                return;
            }

            string contentType =
                (model.Photo.ContentType ?? "")
                .ToLowerInvariant();

            if (contentType != "image/jpeg" &&
                contentType != "image/png")
            {
                ModelState.AddModelError(
                    "Photo",
                    "শুধুমাত্র JPG, JPEG অথবা PNG ছবি আপলোড করুন।"
                );
            }
        }

        private static void TrimModelStrings(
            NominationViewModel model)
        {
            model.CandidateName =
                Clean(model.CandidateName);

            model.MemberNo =
                Clean(model.MemberNo);

            model.OrganizationId =
                Clean(model.OrganizationId);

            model.Designation =
                Clean(model.Designation);

            model.FatherOrHusbandName =
                Clean(model.FatherOrHusbandName);

            model.MotherName =
                Clean(model.MotherName);

            model.NID =
                Clean(model.NID);

            model.PresentAddress =
                Clean(model.PresentAddress);

            model.PermanentAddress =
                Clean(model.PermanentAddress);

            model.Mobile =
                Clean(model.Mobile);

            model.Email =
                Clean(model.Email);

            model.DesiredPost =
                Clean(model.DesiredPost);

            model.ProposerName =
                Clean(model.ProposerName);

            model.ProposerMemberNo =
                Clean(model.ProposerMemberNo);

            model.ProposerPost =
                Clean(model.ProposerPost);

            model.ProposerMobile =
                Clean(model.ProposerMobile);

            model.SupporterName =
                Clean(model.SupporterName);

            model.SupporterMemberNo =
                Clean(model.SupporterMemberNo);

            model.SupporterMobile =
                Clean(model.SupporterMobile);
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? value
                : value.Trim();
        }
    }
}