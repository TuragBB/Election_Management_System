using System;
using System.Globalization;
using System.Web.Mvc;
using NominationSystem.Models;

namespace NominationSystem.Controllers
{
    // NOTE: AdminController.cs te "public class AdminController" ke "public partial class AdminController" kore nin.
    //       [NoCache] attribute ta class-level, tai ei partial part ereo apply hobe.
    public partial class AdminController
    {
        private readonly ElectionSettingsRepository settingsRepo = new ElectionSettingsRepository();

        // ---------------------------------------------------------------- Election settings page
        // /Admin/ElectionSettings              -> bortoman election
        // /Admin/ElectionSettings?id=5         -> 5 number election edit
        // /Admin/ElectionSettings?mode=new     -> notun election toiri
        [HttpGet]
        public ActionResult ElectionSettings(int? id, string mode)
        {
            ElectionSettingsViewModel model;

            try
            {
                if (string.Equals(mode, "new", StringComparison.OrdinalIgnoreCase))
                {
                    model = settingsRepo.GetNew();
                }
                else
                {
                    model = settingsRepo.Get(id);

                    // kono election-i nai : sorasori "notun election" form
                    if (!model.HasElection)
                    {
                        model = settingsRepo.GetNew();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                model = new ElectionSettingsViewModel();
                ViewBag.LoadError = ElectionSettingsRepository.DescribeError(ex);
            }

            return View(model);
        }

        // ---------------------------------------------------------------- Save / Create (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ElectionSettingsSave(int electionId, string title, string term, string committee,
                                                 string note, string startAt, string endAt)
        {
            DateTime? start;
            DateTime? end;

            if (!TryParseLocalDateTime(startAt, out start) || !TryParseLocalDateTime(endAt, out end))
            {
                return Json(new { success = false, message = "তারিখ ও সময়ের ফরম্যাট সঠিক নয়।" });
            }

            try
            {
                var input = new ElectionSettingsInput
                {
                    ElectionId = electionId,
                    Title = title,
                    Term = term,
                    Committee = committee,
                    Note = note,
                    StartAt = start,
                    EndAt = end
                };

                // No login system yet, so a fixed name is stored (same as UpdateStatus).
                var result = settingsRepo.Save(input, "Super Admin");

                if (!result.Success)
                {
                    return Json(new { success = false, message = result.Message });
                }

                string redirectUrl = null;
                if (result.Created)
                {
                    redirectUrl = Url.Action("ElectionSettings", "Admin", new { id = result.State.ElectionId });
                }

                return Json(new
                {
                    success = true,
                    created = result.Created,
                    redirectUrl = redirectUrl,
                    message = result.Message,
                    state = SettingsState(result.State)
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return Json(new { success = false, message = "সেটিংস সংরক্ষণ করা যায়নি। আবার চেষ্টা করুন।" });
            }
        }

        // ---------------------------------------------------------------- Emergency close (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ElectionEmergencyClose(int electionId)
        {
            try
            {
                var result = settingsRepo.EmergencyClose(electionId, "Super Admin");

                if (!result.Success)
                {
                    return Json(new { success = false, message = result.Message });
                }

                return Json(new { success = true, message = result.Message, state = SettingsState(result.State) });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return Json(new { success = false, message = "ভোটগ্রহণ বন্ধ করা যায়নি। আবার চেষ্টা করুন।" });
            }
        }

        // ---------------------------------------------------------------- helpers
        private static object SettingsState(ElectionSettingsViewModel m)
        {
            return new
            {
                electionId = m.ElectionId,
                status = m.Status,
                isOpen = m.IsOpen,
                phase = m.Phase,
                phaseText = m.PhaseText,
                title = m.Title,
                term = m.TermText,
                committee = m.CommitteeName,
                note = m.EligibilityNote,
                startValue = m.StartValue,
                endValue = m.EndValue
            };
        }

        /// <summary>"2027-03-15T09:00" (datetime-local) -> DateTime. Khali hole null (thik ache).</summary>
        private static bool TryParseLocalDateTime(string value, out DateTime? result)
        {
            result = null;
            value = (value ?? "").Trim();
            if (value.Length == 0) return true;

            DateTime d;
            string[] formats = { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss" };

            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            {
                result = d;
                return true;
            }

            return false;
        }
    }
}
