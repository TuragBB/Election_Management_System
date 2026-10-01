using System;
using System.Collections.Generic;

namespace NominationSystem.Models
{
    /// <summary>List page filter + sort + paging parameters.</summary>
    public class NominationListQuery
    {
        public string Search { get; set; }
        public string Post { get; set; }
        public string Status { get; set; }
        public DateTime? Date { get; set; }
        public string Sort { get; set; }
        public string Dir { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }

    public class NominationListItem
    {
        public int NominationId { get; set; }
        public string ApplicationNo { get; set; }
        public DateTime ApplicationDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CandidateName { get; set; }
        public string MemberNo { get; set; }
        public string OrganizationId { get; set; }
        public string Designation { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }
        public string DesiredPost { get; set; }
        public string Status { get; set; }
    }

    public class NominationPage
    {
        public NominationPage()
        {
            Items = new List<NominationListItem>();
        }

        public List<NominationListItem> Items { get; set; }
        public int Total { get; set; }
    }

    /// <summary>Full nomination record (detail + print pages).</summary>
    public class NominationDetail
    {
        public int NominationId { get; set; }
        public string ApplicationNo { get; set; }
        public DateTime ApplicationDate { get; set; }
        public DateTime CreatedAt { get; set; }

        public string CandidateName { get; set; }
        public string MemberNo { get; set; }
        public string OrganizationId { get; set; }
        public string Designation { get; set; }
        public string FatherOrHusbandName { get; set; }
        public string MotherName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string NID { get; set; }
        public string PresentAddress { get; set; }
        public string PermanentAddress { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }
        public string DesiredPost { get; set; }

        public DateTime MembershipDate { get; set; }
        public bool IsMembershipActive { get; set; }
        public bool IsSavingsUpdated { get; set; }
        public bool IsLoanUpdated { get; set; }
        public bool IsEligible { get; set; }
        public bool CandidateDeclarationAccepted { get; set; }

        public string ProposerName { get; set; }
        public string ProposerMemberNo { get; set; }
        public string ProposerPost { get; set; }
        public string ProposerMobile { get; set; }
        public DateTime ProposerDate { get; set; }

        public string SupporterName { get; set; }
        public string SupporterMemberNo { get; set; }
        public string SupporterMobile { get; set; }
        public DateTime SupporterDate { get; set; }

        public string Status { get; set; }
    }

    public static class NominationStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";

        public static bool IsValid(string s)
        {
            return s == Pending || s == Approved || s == Rejected;
        }

        /// <summary>Short text (list badge).</summary>
        public static string Text(string s)
        {
            switch (s)
            {
                case Approved: return "অনুমোদিত";
                case Rejected: return "বাতিল";
                default: return "পেন্ডিং";
            }
        }

        /// <summary>Long text (detail page badge).</summary>
        public static string DetailText(string s)
        {
            switch (s)
            {
                case Approved: return "অনুমোদিত";
                case Rejected: return "বাতিল";
                default: return "পেন্ডিং (যাচাই প্রক্রিয়াধীন)";
            }
        }

        public static string CssClass(string s)
        {
            switch (s)
            {
                case Approved: return "st-approved";
                case Rejected: return "st-rejected";
                default: return "st-pending";
            }
        }
    }
}
