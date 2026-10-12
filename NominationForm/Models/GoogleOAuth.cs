using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NominationSystem.Models
{
    public class GoogleProfile
    {
        public string Sub { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public bool EmailVerified { get; set; }
    }

    /// <summary>
    /// Google OAuth 2.0 (Authorization Code flow). Kono extra NuGet package lage na.
    /// Fast rakhar jonno: sudhu 1ta server-to-server call (code -> id_token). Alada userinfo call nai.
    /// </summary>
    public static class GoogleOAuth
    {
        // ekta HttpClient shobar jonno (connection reuse = fast)
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        private static string Cfg(string key)
        {
            return (ConfigurationManager.AppSettings[key] ?? "").Trim();
        }

        public static string ClientId { get { return Cfg("Google.ClientId"); } }
        private static string ClientSecret { get { return Cfg("Google.ClientSecret"); } }
        public static string ConfiguredRedirectUri { get { return Cfg("Google.RedirectUri"); } }

        /// <summary>Key na boshale Google button lukiye thake, baki login system thik moto cholbe.</summary>
        public static bool IsConfigured
        {
            get { return ClientId.Length > 0 && ClientSecret.Length > 0; }
        }

        public static string BuildAuthUrl(string redirectUri, string state)
        {
            return "https://accounts.google.com/o/oauth2/v2/auth" +
                   "?client_id=" + Uri.EscapeDataString(ClientId) +
                   "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                   "&response_type=code" +
                   "&scope=" + Uri.EscapeDataString("openid email profile") +
                   "&state=" + Uri.EscapeDataString(state) +
                   "&prompt=select_account";
        }

        /// <summary>code -> Google er kach theke id_token niye profile ber kore. Fail hole null.</summary>
        public static async Task<GoogleProfile> ExchangeCodeAsync(string code, string redirectUri)
        {
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
            });

            using (HttpResponseMessage resp = await Http.PostAsync("https://oauth2.googleapis.com/token", form).ConfigureAwait(false))
            {
                if (!resp.IsSuccessStatusCode) return null;

                string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                JObject o = JObject.Parse(json);

                return ParseIdToken((string)o["id_token"]);
            }
        }

        // id_token ta Google er token endpoint theke TLS diye shorashori pawa, tai signature
        // abar verify kora lage na (Google er official doc). Shudhu aud / iss / exp check kora hoy.
        private static GoogleProfile ParseIdToken(string idToken)
        {
            if (string.IsNullOrEmpty(idToken)) return null;

            string[] parts = idToken.Split('.');
            if (parts.Length != 3) return null;

            string b64 = parts[1].Replace('-', '+').Replace('_', '/');
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
            }

            JObject p = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b64)));

            string aud = (string)p["aud"];
            string iss = (string)p["iss"];
            long exp = (long?)p["exp"] ?? 0;
            long now = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            if (aud != ClientId) return null;
            if (iss != "https://accounts.google.com" && iss != "accounts.google.com") return null;
            if (exp < now) return null;

            string sub = (string)p["sub"];
            if (string.IsNullOrEmpty(sub)) return null;

            return new GoogleProfile
            {
                Sub = sub,
                Email = (string)p["email"],
                Name = (string)p["name"],
                EmailVerified = (bool?)p["email_verified"] ?? false
            };
        }
    }
}
