using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;

namespace NominationSystem.Models
{
    public class NominationViewModel
    {
        public int NominationId { get; set; }

        public string ApplicationNo { get; set; }

        [Required(ErrorMessage = "আবেদনের তারিখ দিন।")]
        [DataType(DataType.Date)]
        public DateTime ApplicationDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "প্রার্থীর নাম লিখুন।")]
        [StringLength(200)]
        public string CandidateName { get; set; }

        [Required(ErrorMessage = "সদস্য নম্বর লিখুন।")]
        [StringLength(50)]
        public string MemberNo { get; set; }

        [Required(ErrorMessage = "সংস্থার আইডি নম্বর লিখুন।")]
        [StringLength(50)]
        public string OrganizationId { get; set; }

        [Required(ErrorMessage = "সংস্থার পদবী লিখুন।")]
        [StringLength(150)]
        public string Designation { get; set; }

        [Required(ErrorMessage = "পিতার/স্বামীর নাম লিখুন।")]
        [StringLength(200)]
        public string FatherOrHusbandName { get; set; }

        [Required(ErrorMessage = "মাতার নাম লিখুন।")]
        [StringLength(200)]
        public string MotherName { get; set; }

        [Required(ErrorMessage = "জন্মতারিখ দিন।")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Required(ErrorMessage = "জাতীয় পরিচয়পত্র নম্বর লিখুন।")]
        [StringLength(30)]
        public string NID { get; set; }

        [Required(ErrorMessage = "বর্তমান ঠিকানা লিখুন।")]
        [StringLength(500)]
        public string PresentAddress { get; set; }

        [Required(ErrorMessage = "স্থায়ী ঠিকানা লিখুন।")]
        [StringLength(500)]
        public string PermanentAddress { get; set; }

        [Required(ErrorMessage = "মোবাইল নম্বর লিখুন।")]
        [RegularExpression(@"^(?:\+?88)?01[3-9]\d{8}$",
            ErrorMessage = "সঠিক বাংলাদেশি মোবাইল নম্বর দিন।")]
        public string Mobile { get; set; }

        [EmailAddress(ErrorMessage = "সঠিক ই-মেইল দিন।")]
        public string Email { get; set; }

        [Display(Name = "প্রার্থীর ছবি")]
        public HttpPostedFileBase Photo { get; set; }

        [Required(ErrorMessage = "নির্বাচনী পদ নির্বাচন করুন।")]
        public string DesiredPost { get; set; }

        [Required(ErrorMessage = "সদস্য হওয়ার তারিখ দিন।")]
        [DataType(DataType.Date)]
        public DateTime? MembershipDate { get; set; }

        [Required(ErrorMessage = "সদস্যপদের অবস্থা নির্বাচন করুন।")]
        public bool? IsMembershipActive { get; set; }

        [Required(ErrorMessage = "সঞ্চয়ের অবস্থা নির্বাচন করুন।")]
        public bool? IsSavingsUpdated { get; set; }

        [Required(ErrorMessage = "ঋণের অবস্থা নির্বাচন করুন।")]
        public bool? IsLoanUpdated { get; set; }

        [Required(ErrorMessage = "যোগ্যতার তথ্য নির্বাচন করুন।")]
        public bool? IsEligible { get; set; }

        [MustBeTrue(ErrorMessage = "প্রার্থীর ঘোষণা মেনে সম্মতি দিতে হবে।")]
        public bool CandidateDeclarationAccepted { get; set; }

        [Required(ErrorMessage = "প্রস্তাবকারীর নাম লিখুন।")]
        public string ProposerName { get; set; }

        [Required(ErrorMessage = "প্রস্তাবকারীর সদস্য নম্বর লিখুন।")]
        public string ProposerMemberNo { get; set; }

        [Required(ErrorMessage = "প্রস্তাবিত পদ লিখুন।")]
        public string ProposerPost { get; set; }

        [Required(ErrorMessage = "প্রস্তাবকারীর মোবাইল নম্বর দিন।")]
        [RegularExpression(@"^(?:\+?88)?01[3-9]\d{8}$",
            ErrorMessage = "প্রস্তাবকারীর সঠিক মোবাইল নম্বর দিন।")]
        public string ProposerMobile { get; set; }

        [Required(ErrorMessage = "প্রস্তাবকারীর তারিখ দিন।")]
        [DataType(DataType.Date)]
        public DateTime? ProposerDate { get; set; }

        [Required(ErrorMessage = "সমর্থনকারীর নাম লিখুন।")]
        public string SupporterName { get; set; }

        [Required(ErrorMessage = "সমর্থনকারীর সদস্য নম্বর লিখুন।")]
        public string SupporterMemberNo { get; set; }

        [Required(ErrorMessage = "সমর্থনকারীর মোবাইল নম্বর দিন।")]
        [RegularExpression(@"^(?:\+?88)?01[3-9]\d{8}$",
            ErrorMessage = "সমর্থনকারীর সঠিক মোবাইল নম্বর দিন।")]
        public string SupporterMobile { get; set; }

        [Required(ErrorMessage = "সমর্থনকারীর তারিখ দিন।")]
        [DataType(DataType.Date)]
        public DateTime? SupporterDate { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class MustBeTrueAttribute : ValidationAttribute, IClientValidatable
    {
        public override bool IsValid(object value)
        {
            return value is bool && (bool)value;
        }

        public IEnumerable<ModelClientValidationRule> GetClientValidationRules(
            ModelMetadata metadata,
            ControllerContext context)
        {
            yield return new ModelClientValidationRule
            {
                ErrorMessage = FormatErrorMessage(metadata.GetDisplayName()),
                ValidationType = "mustbetrue"
            };
        }
    }
}