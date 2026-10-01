using System;

namespace NominationSystem.Models
{
    /// <summary>Bangla digits / date helpers used by admin views and controller.</summary>
    public static class Bn
    {
        public static readonly string[] Posts =
        {
            "সভাপতি",
            "সহ-সভাপতি",
            "সাধারণ সম্পাদক",
            "যুগ্ম সম্পাদক",
            "কোষাধ্যক্ষ",
            "সহকারী কোষাধ্যক্ষ",
            "সম্মানিত সদস্য"
        };

        private static readonly string[] Months =
        {
            "জানুয়ারি", "ফেব্রুয়ারি", "মার্চ", "এপ্রিল", "মে", "জুন",
            "জুলাই", "আগস্ট", "সেপ্টেম্বর", "অক্টোবর", "নভেম্বর", "ডিসেম্বর"
        };

        public static string Digits(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            char[] map = { '০', '১', '২', '৩', '৪', '৫', '৬', '৭', '৮', '৯' };
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] >= '0' && chars[i] <= '9')
                {
                    chars[i] = map[chars[i] - '0'];
                }
            }
            return new string(chars);
        }

        public static string Digits(int value)
        {
            return Digits(value.ToString());
        }

        /// <summary>12 মে, 2015 (Bangla digits)</summary>
        public static string DateText(DateTime d)
        {
            return Digits(d.Day) + " " + Months[d.Month - 1] + ", " + Digits(d.Year);
        }

        /// <summary>10:18 পূর্বাহ্ন</summary>
        public static string TimeText(DateTime d)
        {
            int hour = d.Hour % 12;
            if (hour == 0) hour = 12;
            string suffix = d.Hour < 12 ? "পূর্বাহ্ন" : "অপরাহ্ন";
            return Digits(hour.ToString("00")) + ":" + Digits(d.Minute.ToString("00")) + " " + suffix;
        }

        public static string DateTimeText(DateTime d)
        {
            return DateText(d) + ", " + TimeText(d);
        }

        /// <summary>Application date + time of submission.</summary>
        public static string SubmittedText(DateTime applicationDate, DateTime createdAt)
        {
            return DateText(applicationDate) + ", " + TimeText(createdAt);
        }

        /// <summary>dd/MM/yyyy in Bangla digits (used in print form).</summary>
        public static string ShortDate(DateTime d)
        {
            return Digits(d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>"১৪ বছর ৮ মাস"</summary>
        public static string Duration(DateTime from, DateTime to)
        {
            int months = (to.Year - from.Year) * 12 + (to.Month - from.Month);
            if (to.Day < from.Day) months--;
            if (months < 0) months = 0;

            int y = months / 12;
            int m = months % 12;

            if (y > 0 && m > 0) return Digits(y) + " বছর " + Digits(m) + " মাস";
            if (y > 0) return Digits(y) + " বছর";
            return Digits(m) + " মাস";
        }

        /// <summary>"৩৯ বছর"</summary>
        public static string Age(DateTime dob)
        {
            DateTime today = DateTime.Today;
            int age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age)) age--;
            if (age < 0) age = 0;
            return Digits(age) + " বছর";
        }

        public static string PostEnglish(string post)
        {
            switch (post)
            {
                case "সভাপতি": return "President";
                case "সহ-সভাপতি": return "Vice President";
                case "সাধারণ সম্পাদক": return "General Secretary";
                case "যুগ্ম সম্পাদক": return "Joint Secretary";
                case "কোষাধ্যক্ষ": return "Treasurer";
                case "সহকারী কোষাধ্যক্ষ": return "Assistant Treasurer";
                case "সম্মানিত সদস্য": return "Honorable Member";
                default: return "";
            }
        }

        public static string PostWithEnglish(string post)
        {
            string en = PostEnglish(post);
            return string.IsNullOrEmpty(en) ? post : post + " (" + en + ")";
        }
    }
}
