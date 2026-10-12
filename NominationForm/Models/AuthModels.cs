using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Security;

namespace NominationSystem.Models
{
    public static class AccountTypes
    {
        public const string Voter = "Voter";
        public const string Member = "Member";
    }

    /// <summary>Login cookie theke pawa user (database hit lage na - tai onek fast).</summary>
    public class CurrentUser
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string AccountType { get; set; }

        public bool IsMember
        {
            get { return AccountType == AccountTypes.Member; }
        }
    }

    public static class AuthUser
    {
        /// <summary>Ekhon je login kora ase (na thakle null). Views / controller shob jaygay use kora jay.</summary>
        public static CurrentUser Current
        {
            get
            {
                HttpContext ctx = HttpContext.Current;
                return ctx == null ? null : From(ctx.User);
            }
        }

        public static CurrentUser From(IPrincipal user)
        {
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated) return null;

            FormsIdentity fi = user.Identity as FormsIdentity;
            if (fi == null || fi.Ticket == null) return null;

            string[] p = (fi.Ticket.UserData ?? "").Split(new[] { '|' }, 3);
            int id;
            if (p.Length < 3 || !int.TryParse(p[0], out id)) return null;

            return new CurrentUser { UserId = id, AccountType = p[1], Name = p[2] };
        }
    }

    /// <summary>User er input ke ek rokom format e ana (Bangla digit, +88, space etc).</summary>
    public static class InputNorm
    {
        public static string AsciiDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            char[] c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] >= '\u09E6' && c[i] <= '\u09EF') c[i] = (char)('0' + (c[i] - '\u09E6'));
            }
            return new string(c);
        }

        /// <summary>+8801712345678 / 8801712345678 / ০১৭১২-৩৪৫৬৭৮  =>  01712345678</summary>
        public static string Mobile(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            s = AsciiDigits(s.Trim());
            s = Regex.Replace(s, @"[\s\-\(\)]", "");

            if (s.StartsWith("+88")) s = s.Substring(3);
            else if (s.StartsWith("88") && s.Length == 13) s = s.Substring(2);

            return s;
        }

        public static bool IsValidMobile(string normalized)
        {
            return !string.IsNullOrEmpty(normalized) && Regex.IsMatch(normalized, @"^01[3-9][0-9]{8}$");
        }

        public static string Email(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant();
        }

        public static string Text(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : Regex.Replace(s.Trim(), @"\s+", " ");
        }

        public static string Login(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Contains("@") ? s.ToLowerInvariant() : Mobile(s);
        }
    }

    // ===================================================================== view models
    public class LoginViewModel
    {
        [Required(ErrorMessage = "ই-মেইল অথবা মোবাইল নম্বর লিখুন।")]
        [StringLength(256)]
        public string Login { get; set; }

        [Required(ErrorMessage = "পাসওয়ার্ড লিখুন।")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "আপনার পূর্ণ নাম লিখুন।")]
        [StringLength(200, ErrorMessage = "নাম সর্বোচ্চ ২০০ অক্ষরের হতে পারবে।")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "ই-মেইল ঠিকানা লিখুন।")]
        [EmailAddress(ErrorMessage = "সঠিক ই-মেইল ঠিকানা দিন।")]
        [StringLength(256)]
        public string Email { get; set; }

        [Required(ErrorMessage = "মোবাইল নম্বর লিখুন।")]
        [StringLength(30)]
        public string Mobile { get; set; }

        [Required(ErrorMessage = "পাসওয়ার্ড দিন।")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "পাসওয়ার্ড কমপক্ষে ৮ অক্ষরের হতে হবে।")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*[0-9]).+$", ErrorMessage = "পাসওয়ার্ডে অন্তত একটি ইংরেজি অক্ষর ও একটি সংখ্যা থাকতে হবে।")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "পাসওয়ার্ডটি আবার লিখুন।")]
        [Compare("Password", ErrorMessage = "দুটি পাসওয়ার্ড মেলেনি।")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }

        public string AccountType { get; set; }

        [StringLength(50, ErrorMessage = "সদস্য নম্বর সর্বোচ্চ ৫০ অক্ষরের হতে পারবে।")]
        public string MemberNo { get; set; }

        public string ReturnUrl { get; set; }
    }

    /// <summary>Google diye prothom bar ashle - baki tothyo nite.</summary>
    public class GoogleCompleteViewModel
    {
        [Required(ErrorMessage = "আপনার পূর্ণ নাম লিখুন।")]
        [StringLength(200)]
        public string FullName { get; set; }

        public string Email { get; set; }

        [Required(ErrorMessage = "মোবাইল নম্বর লিখুন।")]
        [StringLength(30)]
        public string Mobile { get; set; }

        public string AccountType { get; set; }

        [StringLength(50)]
        public string MemberNo { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class MemberUpgradeViewModel
    {
        [Required(ErrorMessage = "সদস্য নম্বর লিখুন।")]
        [StringLength(50)]
        public string MemberNo { get; set; }

        public string ReturnUrl { get; set; }
    }
}
