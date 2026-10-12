using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using NominationSystem.Models;

namespace NominationSystem.Controllers
{
    /// <summary>Login / Sign up / Google login / Logout / Sodosso verify.</summary>
    [NoCache]
    public class AccountController : Controller
    {
        private readonly AuthRepository repo = new AuthRepository();

        private const string StateCookie = "swf_g_state";
        private const string SesSub = "g.sub";
        private const string SesEmail = "g.email";
        private const string SesName = "g.name";

        private const string GenericLoginError = "ই-মেইল/মোবাইল নম্বর অথবা পাসওয়ার্ড সঠিক নয়।";

        // ================================================================ LOGIN
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (AuthUser.Current != null) return Redirect(SafeReturn(returnUrl));

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            string login = InputNorm.Login(model.Login);
            UserRecord u;

            try
            {
                u = string.IsNullOrEmpty(login) ? null : repo.GetByLogin(login);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                ModelState.AddModelError("", "একটি সমস্যা হয়েছে। দয়া করে আবার চেষ্টা করুন।");
                return View(model);
            }

            if (u == null || !u.IsActive)
            {
                ModelState.AddModelError("", GenericLoginError);
                return View(model);
            }

            if (u.LockMinutes > 0)
            {
                ModelState.AddModelError("", "অনেকবার ভুল পাসওয়ার্ড দেওয়ায় অ্যাকাউন্ট সাময়িকভাবে বন্ধ আছে। প্রায় "
                    + Bn.Digits(u.LockMinutes) + " মিনিট পর আবার চেষ্টা করুন।");
                return View(model);
            }

            if (u.PasswordHash == null)
            {
                ModelState.AddModelError("", "এই অ্যাকাউন্টটি Google দিয়ে খোলা হয়েছে। নিচের “Google দিয়ে লগইন” বাটনটি ব্যবহার করুন।");
                return View(model);
            }

            if (!PasswordHasher.Verify(model.Password, u.PasswordHash, u.PasswordSalt, u.PwdIterations))
            {
                repo.LoginResult(u.UserId, false);
                ModelState.AddModelError("", GenericLoginError);
                return View(model);
            }

            repo.LoginResult(u.UserId, true);
            SignIn(u, model.RememberMe);

            return Redirect(SafeReturn(model.ReturnUrl));
        }

        // ================================================================ REGISTER (sign up)
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Register(string returnUrl, string type)
        {
            if (AuthUser.Current != null) return Redirect(SafeReturn(returnUrl));

            return View(new RegisterViewModel
            {
                ReturnUrl = returnUrl,
                AccountType = type == AccountTypes.Member ? AccountTypes.Member : AccountTypes.Voter
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            model.AccountType = model.AccountType == AccountTypes.Member ? AccountTypes.Member : AccountTypes.Voter;
            model.FullName = InputNorm.Text(model.FullName);
            model.Email = InputNorm.Email(model.Email);
            model.Mobile = InputNorm.Mobile(model.Mobile);
            model.MemberNo = InputNorm.Text(InputNorm.AsciiDigits(model.MemberNo));

            if (!string.IsNullOrEmpty(model.Mobile) && !InputNorm.IsValidMobile(model.Mobile))
            {
                ModelState.AddModelError("Mobile", "সঠিক বাংলাদেশি মোবাইল নম্বর দিন (যেমন: ০১৭XXXXXXXX)।");
            }

            if (model.AccountType == AccountTypes.Member && string.IsNullOrEmpty(model.MemberNo))
            {
                ModelState.AddModelError("MemberNo", "সদস্য হিসেবে অ্যাকাউন্ট খুলতে সদস্য নম্বর লিখতে হবে।");
            }

            if (!ModelState.IsValid) return View(model);

            try
            {
                UserRecord u = repo.Register(
                    model.FullName, model.Email, model.Mobile, model.Password, null,
                    model.AccountType, model.AccountType == AccountTypes.Member ? model.MemberNo : null);

                if (u == null)
                {
                    ModelState.AddModelError("", "অ্যাকাউন্ট খোলা যায়নি। আবার চেষ্টা করুন।");
                    return View(model);
                }

                SignIn(u, false);
                return Redirect(SafeReturn(model.ReturnUrl));
            }
            catch (AuthException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                ModelState.AddModelError("", "একটি সমস্যা হয়েছে। দয়া করে আবার চেষ্টা করুন।");
                return View(model);
            }
        }

        // ================================================================ GOOGLE (OAuth 2.0)
        [HttpGet]
        [AllowAnonymous]
        public ActionResult GoogleLogin(string returnUrl)
        {
            if (!GoogleOAuth.IsConfigured) return RedirectToAction("Login", new { returnUrl });

            string token = RandomToken();
            string ret = (!string.IsNullOrEmpty(returnUrl) && returnUrl.Length <= 300 && Url.IsLocalUrl(returnUrl)) ? returnUrl : "";

            Response.Cookies.Add(new HttpCookie(StateCookie, token)
            {
                HttpOnly = true,
                Secure = Request.IsSecureConnection,
                Expires = DateTime.Now.AddMinutes(10)
            });

            // state = <random>.<returnUrl (base64url)>
            string state = token + "." + B64Enc(ret);
            return Redirect(GoogleOAuth.BuildAuthUrl(RedirectUri(), state));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> GoogleCallback(string code, string state, string error)
        {
            HttpCookie sc = Request.Cookies[StateCookie];
            string cookieToken = sc == null ? null : sc.Value;

            // state cookie ek bar e use hoy
            Response.Cookies.Add(new HttpCookie(StateCookie, "") { HttpOnly = true, Expires = DateTime.Now.AddDays(-1) });

            string[] sp = (state ?? "").Split(new[] { '.' }, 2);

            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || sp.Length != 2 ||
                string.IsNullOrEmpty(cookieToken) || !SameText(cookieToken, sp[0]))
            {
                return LoginWithError("Google দিয়ে লগইন সম্পন্ন করা যায়নি। আবার চেষ্টা করুন।");
            }

            string ret = B64Dec(sp[1]);

            GoogleProfile gp = null;
            try
            {
                gp = await GoogleOAuth.ExchangeCodeAsync(code, RedirectUri());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
            }

            if (gp == null || !gp.EmailVerified || string.IsNullOrEmpty(gp.Email))
            {
                return LoginWithError("Google অ্যাকাউন্ট যাচাই করা যায়নি। আবার চেষ্টা করুন।");
            }

            string email = InputNorm.Email(gp.Email);
            UserRecord u;

            try
            {
                u = repo.GoogleLogin(gp.Sub, email);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return LoginWithError("একটি সমস্যা হয়েছে। দয়া করে আবার চেষ্টা করুন।");
            }

            if (u != null)
            {
                if (!u.IsActive) return LoginWithError("এই অ্যাকাউন্টটি বর্তমানে বন্ধ আছে।");

                repo.LoginResult(u.UserId, true);
                SignIn(u, true);
                return Redirect(SafeReturn(ret));
            }

            // notun manush: baki tothyo (mobile + voter/member) nite hobe
            Session[SesSub] = gp.Sub;
            Session[SesEmail] = email;
            Session[SesName] = gp.Name;

            return RedirectToAction("GoogleComplete", new { returnUrl = ret });
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult GoogleComplete(string returnUrl)
        {
            string sub = Session[SesSub] as string;
            if (string.IsNullOrEmpty(sub)) return RedirectToAction("Login", new { returnUrl });

            return View(new GoogleCompleteViewModel
            {
                FullName = Session[SesName] as string,
                Email = Session[SesEmail] as string,
                AccountType = AccountTypes.Voter,
                ReturnUrl = returnUrl
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult GoogleComplete(GoogleCompleteViewModel model)
        {
            string sub = Session[SesSub] as string;
            string email = Session[SesEmail] as string;

            if (string.IsNullOrEmpty(sub)) return RedirectToAction("Login", new { returnUrl = model.ReturnUrl });

            model.Email = email;   // email Google er, user er pathano na
            model.AccountType = model.AccountType == AccountTypes.Member ? AccountTypes.Member : AccountTypes.Voter;
            model.FullName = InputNorm.Text(model.FullName);
            model.Mobile = InputNorm.Mobile(model.Mobile);
            model.MemberNo = InputNorm.Text(InputNorm.AsciiDigits(model.MemberNo));

            if (!string.IsNullOrEmpty(model.Mobile) && !InputNorm.IsValidMobile(model.Mobile))
            {
                ModelState.AddModelError("Mobile", "সঠিক বাংলাদেশি মোবাইল নম্বর দিন (যেমন: ০১৭XXXXXXXX)।");
            }

            if (model.AccountType == AccountTypes.Member && string.IsNullOrEmpty(model.MemberNo))
            {
                ModelState.AddModelError("MemberNo", "সদস্য হিসেবে অ্যাকাউন্ট খুলতে সদস্য নম্বর লিখতে হবে।");
            }

            if (!ModelState.IsValid) return View(model);

            try
            {
                UserRecord u = repo.Register(
                    model.FullName, email, model.Mobile, null, sub,
                    model.AccountType, model.AccountType == AccountTypes.Member ? model.MemberNo : null);

                if (u == null)
                {
                    ModelState.AddModelError("", "অ্যাকাউন্ট খোলা যায়নি। আবার চেষ্টা করুন।");
                    return View(model);
                }

                SignIn(u, true);   // SignIn Session clear kore, tai Google tothyo-o muche jay
                return Redirect(SafeReturn(model.ReturnUrl));
            }
            catch (AuthException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                ModelState.AddModelError("", "একটি সমস্যা হয়েছে। দয়া করে আবার চেষ্টা করুন।");
                return View(model);
            }
        }

        // ================================================================ MEMBER VERIFY (Voter -> Member)
        [HttpGet]
        [Authorize]
        public ActionResult MemberRequired(string returnUrl)
        {
            CurrentUser cu = AuthUser.Current;
            if (cu != null && cu.IsMember) return Redirect(SafeReturn(returnUrl, Url.Action("Index", "Nomination")));

            return View(new MemberUpgradeViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult MemberRequired(MemberUpgradeViewModel model)
        {
            CurrentUser cu = AuthUser.Current;
            if (cu == null) return RedirectToAction("Login");

            model.MemberNo = InputNorm.Text(InputNorm.AsciiDigits(model.MemberNo));
            if (!ModelState.IsValid) return View(model);

            try
            {
                UserRecord u = repo.UpgradeToMember(cu.UserId, model.MemberNo);
                if (u == null)
                {
                    ModelState.AddModelError("", "সদস্য যাচাই করা যায়নি। আবার চেষ্টা করুন।");
                    return View(model);
                }

                FormsIdentity fi = User.Identity as FormsIdentity;
                bool remember = fi != null && fi.Ticket != null && fi.Ticket.IsPersistent;

                SignIn(u, remember);
                return Redirect(SafeReturn(model.ReturnUrl, Url.Action("Index", "Nomination")));
            }
            catch (AuthException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                ModelState.AddModelError("", "একটি সমস্যা হয়েছে। দয়া করে আবার চেষ্টা করুন।");
                return View(model);
            }
        }

        // ================================================================ LOGOUT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();

            return RedirectToAction("Login");
        }

        // ================================================================ helpers
        private void SignIn(UserRecord u, bool remember)
        {
            // age er user er session data (jemon vote receipt) jate onno jon na dekhe
            Session.Clear();

            string name = (u.FullName ?? "").Replace("|", " ");
            string data = u.UserId + "|" + u.AccountType + "|" + name;

            DateTime now = DateTime.Now;
            DateTime expires = remember ? now.AddDays(14) : now.Add(FormsAuthentication.Timeout);

            var ticket = new FormsAuthenticationTicket(
                1, u.UserId.ToString(), now, expires, remember, data, FormsAuthentication.FormsCookiePath);

            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true,
                Secure = FormsAuthentication.RequireSSL,
                Path = FormsAuthentication.FormsCookiePath
            };

            if (remember) cookie.Expires = expires;

            Response.Cookies.Add(cookie);
        }

        private string SafeReturn(string returnUrl)
        {
            return SafeReturn(returnUrl, Url.Action("Index", "Vote"));
        }

        private string SafeReturn(string returnUrl, string fallback)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return returnUrl;
            return fallback;
        }

        private ActionResult LoginWithError(string message)
        {
            TempData["AuthMsg"] = message;
            return RedirectToAction("Login");
        }

        private string RedirectUri()
        {
            string cfg = GoogleOAuth.ConfiguredRedirectUri;
            if (!string.IsNullOrEmpty(cfg)) return cfg;

            return Url.Action("GoogleCallback", "Account", (object)null, Request.Url.Scheme);
        }

        private static string RandomToken()
        {
            byte[] b = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(b);
            }
            return BitConverter.ToString(b).Replace("-", "");
        }

        private static bool SameText(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static string B64Enc(string s)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string B64Dec(string s)
        {
            try
            {
                string b = (s ?? "").Replace('-', '+').Replace('_', '/');
                switch (b.Length % 4)
                {
                    case 2: b += "=="; break;
                    case 3: b += "="; break;
                }
                return Encoding.UTF8.GetString(Convert.FromBase64String(b));
            }
            catch (FormatException)
            {
                return "";
            }
        }
    }
}
