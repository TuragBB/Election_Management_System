
using NominationForm.Models;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace NominationSystem.Models
{
    public class NominationRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["NominationDB"]
            .ConnectionString;

        public int Insert(NominationViewModel m)
        {
            using (SqlConnection con =
                   new SqlConnection(connectionString))
            using (SqlCommand cmd =
                   new SqlCommand("dbo.Nomination_Insert", con))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                Add(cmd, "@ApplicationNo", m.ApplicationNo, 30);
                Add(cmd, "@ApplicationDate", m.ApplicationDate, 0);
                Add(cmd, "@CandidateName", m.CandidateName, 200);
                Add(cmd, "@MemberNo", m.MemberNo, 50);
                Add(cmd, "@OrganizationId", m.OrganizationId, 50);
                Add(cmd, "@Designation", m.Designation, 150);
                Add(cmd, "@FatherOrHusbandName", m.FatherOrHusbandName, 200);
                Add(cmd, "@MotherName", m.MotherName, 200);
                Add(cmd, "@DateOfBirth", m.DateOfBirth, 0);
                Add(cmd, "@NID", m.NID, 30);
                Add(cmd, "@PresentAddress", m.PresentAddress, 500);
                Add(cmd, "@PermanentAddress", m.PermanentAddress, 500);
                Add(cmd, "@Mobile", m.Mobile, 20);
                Add(cmd, "@Email", m.Email, 150);
                Add(cmd, "@DesiredPost", m.DesiredPost, 100);
                Add(cmd, "@MembershipDate", m.MembershipDate, 0);

                Add(cmd, "@IsMembershipActive", m.IsMembershipActive, 0);
                Add(cmd, "@IsSavingsUpdated", m.IsSavingsUpdated, 0);
                Add(cmd, "@IsLoanUpdated", m.IsLoanUpdated, 0);
                Add(cmd, "@IsEligible", m.IsEligible, 0);
                Add(cmd, "@CandidateDeclarationAccepted",
                    m.CandidateDeclarationAccepted, 0);

                Add(cmd, "@ProposerName", m.ProposerName, 200);
                Add(cmd, "@ProposerMemberNo", m.ProposerMemberNo, 50);
                Add(cmd, "@ProposerPost", m.ProposerPost, 100);
                Add(cmd, "@ProposerMobile", m.ProposerMobile, 20);
                Add(cmd, "@ProposerDate", m.ProposerDate, 0);

                Add(cmd, "@SupporterName", m.SupporterName, 200);
                Add(cmd, "@SupporterMemberNo", m.SupporterMemberNo, 50);
                Add(cmd, "@SupporterMobile", m.SupporterMobile, 20);
                Add(cmd, "@SupporterDate", m.SupporterDate, 0);

                con.Open();
                int newId = Convert.ToInt32(cmd.ExecuteScalar());

                SavePhoto(con, newId, m.Photo);
                StampElection(con, newId);

                return newId;
            }
        }

        // Notun mononoyon ta sobcheye notun toiri kora election er sathe jure dey.
        // Kono karone fail korleo mononoyon jama deya atkabe na.
        private static void StampElection(SqlConnection con, int id)
        {
            try
            {
                const string sql =
                    "UPDATE dbo.Nominations " +
                    "SET ElectionId = (SELECT TOP 1 ElectionId FROM dbo.Elections ORDER BY ElectionId DESC) " +
                    "WHERE NominationId = @Id AND ElectionId IS NULL";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
            }
        }

        // Saves the uploaded photo (the stored procedure has no photo parameter).
        private void SavePhoto(SqlConnection con, int id,
                               System.Web.HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength <= 0) return;

            byte[] bytes;
            using (var ms = new System.IO.MemoryStream())
            {
                if (file.InputStream.CanSeek) file.InputStream.Position = 0;
                file.InputStream.CopyTo(ms);
                bytes = ms.ToArray();
            }

            if (bytes.Length == 0) return;

            string contentType = (file.ContentType ?? "").Trim().ToLowerInvariant();
            if (contentType != "image/png") contentType = "image/jpeg";

            string fileName = System.IO.Path.GetFileName(file.FileName ?? "");
            if (fileName.Length > 255)
                fileName = fileName.Substring(fileName.Length - 255);

            const string sql =
                "UPDATE dbo.Nominations " +
                "SET Photo = @Photo, PhotoContentType = @ContentType, PhotoFileName = @FileName " +
                "WHERE NominationId = @Id";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.Add("@Photo", SqlDbType.VarBinary, -1).Value = bytes;
                cmd.Parameters.Add("@ContentType", SqlDbType.NVarChar, 100).Value = contentType;
                cmd.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = fileName;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                cmd.ExecuteNonQuery();
            }
        }


        private void Add(SqlCommand cmd, string name,
                         object value, int size)
        {
            SqlParameter p;

            if (name.EndsWith("Date") ||
                name == "@ApplicationDate" ||
                name == "@DateOfBirth" ||
                name == "@MembershipDate")
            {
                p = cmd.Parameters.Add(name, SqlDbType.Date);
            }
            else if (name == "@IsMembershipActive" ||
                     name == "@IsSavingsUpdated" ||
                     name == "@IsLoanUpdated" ||
                     name == "@IsEligible" ||
                     name == "@CandidateDeclarationAccepted")
            {
                p = cmd.Parameters.Add(name, SqlDbType.Bit);
            }
            else
            {
                p = cmd.Parameters.Add(name, SqlDbType.NVarChar, size);
            }

            p.Value = value ?? DBNull.Value;
        }

        public Nomination GetById(int id)
        {
            using (var db = new NominationDBEntities())
            {
                return db.Nominations
                         .FirstOrDefault(x => x.NominationId == id);
            }
        }
    }
}