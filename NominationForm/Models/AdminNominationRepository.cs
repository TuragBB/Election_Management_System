using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Linq;

namespace NominationSystem.Models
{
    /// <summary>
    /// Admin side data access (list / detail / status update).
    /// Plain ADO.NET, so the existing EF model (.edmx) does not need any change.
    /// </summary>
    public class AdminNominationRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["NominationDB"].ConnectionString;

        // Whitelist : UI sort key -> real column (prevents SQL injection in ORDER BY)
        private static readonly Dictionary<string, string> SortColumns =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "serial", "NominationId" },
                { "date", "CreatedAt" },
                { "name", "CandidateName" },
                { "post", "DesiredPost" },
                { "status", "Status" }
            };

        public NominationPage GetPage(NominationListQuery q)
        {
            var page = new NominationPage();

            List<SqlParameter> filterParams;
            string where = BuildWhere(q, out filterParams);

            string column;
            if (!SortColumns.TryGetValue(q.Sort ?? "", out column))
            {
                column = "CreatedAt";
            }

            string dir = string.Equals(q.Dir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
            string order = column == "NominationId"
                ? "NominationId " + dir
                : column + " " + dir + ", NominationId DESC";

            int skip = q.Skip < 0 ? 0 : q.Skip;
            int take = q.Take <= 0 ? 10 : q.Take;

            using (var con = new SqlConnection(connectionString))
            {
                con.Open();

                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Nominations" + where, con))
                {
                    AddParams(cmd, filterParams);
                    page.Total = Convert.ToInt32(cmd.ExecuteScalar());
                }

                string sql =
                    "SELECT NominationId, ApplicationNo, ApplicationDate, CreatedAt, CandidateName, " +
                    "MemberNo, OrganizationId, Designation, Mobile, Email, DesiredPost, Status " +
                    "FROM dbo.Nominations" + where +
                    " ORDER BY " + order +
                    " OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";

                using (var cmd = new SqlCommand(sql, con))
                {
                    AddParams(cmd, filterParams);
                    cmd.Parameters.Add("@skip", SqlDbType.Int).Value = skip;
                    cmd.Parameters.Add("@take", SqlDbType.Int).Value = take;

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            page.Items.Add(new NominationListItem
                            {
                                NominationId = Convert.ToInt32(r["NominationId"]),
                                ApplicationNo = Str(r, "ApplicationNo"),
                                ApplicationDate = Convert.ToDateTime(r["ApplicationDate"]),
                                CreatedAt = Convert.ToDateTime(r["CreatedAt"]),
                                CandidateName = Str(r, "CandidateName"),
                                MemberNo = Str(r, "MemberNo"),
                                OrganizationId = Str(r, "OrganizationId"),
                                Designation = Str(r, "Designation"),
                                Mobile = Str(r, "Mobile"),
                                Email = Str(r, "Email"),
                                DesiredPost = Str(r, "DesiredPost"),
                                Status = Str(r, "Status")
                            });
                        }
                    }
                }
            }

            return page;
        }

        public NominationDetail GetDetail(int id)
        {
            using (var con = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand("SELECT * FROM dbo.Nominations WHERE NominationId = @id", con))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                con.Open();

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;

                    return new NominationDetail
                    {
                        NominationId = Convert.ToInt32(r["NominationId"]),
                        ApplicationNo = Str(r, "ApplicationNo"),
                        ApplicationDate = Convert.ToDateTime(r["ApplicationDate"]),
                        CreatedAt = Convert.ToDateTime(r["CreatedAt"]),

                        CandidateName = Str(r, "CandidateName"),
                        MemberNo = Str(r, "MemberNo"),
                        OrganizationId = Str(r, "OrganizationId"),
                        Designation = Str(r, "Designation"),
                        FatherOrHusbandName = Str(r, "FatherOrHusbandName"),
                        MotherName = Str(r, "MotherName"),
                        DateOfBirth = Convert.ToDateTime(r["DateOfBirth"]),
                        NID = Str(r, "NID"),
                        PresentAddress = Str(r, "PresentAddress"),
                        PermanentAddress = Str(r, "PermanentAddress"),
                        Mobile = Str(r, "Mobile"),
                        Email = Str(r, "Email"),
                        DesiredPost = Str(r, "DesiredPost"),

                        MembershipDate = Convert.ToDateTime(r["MembershipDate"]),
                        IsMembershipActive = Convert.ToBoolean(r["IsMembershipActive"]),
                        IsSavingsUpdated = Convert.ToBoolean(r["IsSavingsUpdated"]),
                        IsLoanUpdated = Convert.ToBoolean(r["IsLoanUpdated"]),
                        IsEligible = Convert.ToBoolean(r["IsEligible"]),
                        CandidateDeclarationAccepted = Convert.ToBoolean(r["CandidateDeclarationAccepted"]),

                        ProposerName = Str(r, "ProposerName"),
                        ProposerMemberNo = Str(r, "ProposerMemberNo"),
                        ProposerPost = Str(r, "ProposerPost"),
                        ProposerMobile = Str(r, "ProposerMobile"),
                        ProposerDate = Convert.ToDateTime(r["ProposerDate"]),

                        SupporterName = Str(r, "SupporterName"),
                        SupporterMemberNo = Str(r, "SupporterMemberNo"),
                        SupporterMobile = Str(r, "SupporterMobile"),
                        SupporterDate = Convert.ToDateTime(r["SupporterDate"]),

                        Status = Str(r, "Status")
                    };
                }
            }
        }

        /// <summary>Returns true if a row was updated.</summary>
        public bool UpdateStatus(int id, string status, string updatedBy)
        {
            const string sql =
                "UPDATE dbo.Nominations " +
                "SET Status = @status, StatusUpdatedAt = SYSDATETIME(), StatusUpdatedBy = @by " +
                "WHERE NominationId = @id";

            using (var con = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.Add("@status", SqlDbType.NVarChar, 20).Value = status;
                cmd.Parameters.Add("@by", SqlDbType.NVarChar, 100).Value = (object)updatedBy ?? DBNull.Value;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;

                con.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // ------------------------------------------------------------------
        private static string BuildWhere(
    NominationListQuery q,
    out List<SqlParameter> ps)
        {
            ps = new List<SqlParameter>();

            var sb = new StringBuilder(
                " WHERE 1 = 1"
            );

            /* =========================================================
               SEARCH

               Example database name:
               মোঃ আরিফুল ইসলাম

               Search:
               মোঃ ইসলাম

               Tokens:
               মোঃ
               ইসলাম

               Both tokens must exist somewhere in the
               same record.

               Each token can match:
               CandidateName
               MemberNo
               OrganizationId
               Designation
               Mobile
               Email
               ApplicationNo
               ========================================================= */

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                string rawSearch = q.Search.Trim();

                /*
                 * Treat these as separators:
                 * space
                 * comma ,
                 * Bengali comma ،
                 * full-width comma ，
                 * Bengali danda-like separator ।
                 * semicolon ;
                 */

                rawSearch = rawSearch
                    .Replace(",", " ")
                    .Replace("،", " ")
                    .Replace("，", " ")
                    .Replace(";", " ")
                    .Replace("؛", " ")
                    .Replace("।", " ");

                string[] tokens =
                    rawSearch.Split(
                        new[] { ' ', '\t', '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries
                    );

                /*
                 * Limit number of tokens so a huge search string
                 * cannot generate an unnecessarily huge SQL query.
                 */
                int maxTokens = Math.Min(tokens.Length, 10);

                for (int i = 0; i < maxTokens; i++)
                {
                    string token = tokens[i];

                    if (string.IsNullOrWhiteSpace(token))
                    {
                        continue;
                    }

                    /*
                     * Escape SQL LIKE wildcard characters.
                     */
                    token = token
                        .Replace("\\", "\\\\")
                        .Replace("%", "\\%")
                        .Replace("_", "\\_")
                        .Replace("[", "\\[");

                    string parameterName =
                        "@search" + i;

                    /*
                     * Every token must match at least one field.
                     *
                     * Example:
                     *
                     * token 1 = মোঃ
                     * token 2 = ইসলাম
                     *
                     * becomes:
                     *
                     * AND (
                     *   CandidateName LIKE '%মোঃ%'
                     *   OR MemberNo LIKE '%মোঃ%'
                     *   ...
                     * )
                     *
                     * AND (
                     *   CandidateName LIKE '%ইসলাম%'
                     *   OR MemberNo LIKE '%ইসলাম%'
                     *   ...
                     * )
                     */

                    sb.Append(
                        " AND (" +
                        "CandidateName LIKE " +
                        parameterName +
                        " ESCAPE '\\' " +

                        " OR MemberNo LIKE " +
                        parameterName +
                        " ESCAPE '\\' " +

                        " OR OrganizationId LIKE " +
                        parameterName +
                        " ESCAPE '\\' " +

                        " OR Designation LIKE " +
                        parameterName +
                        " ESCAPE '\\' " +

                        " OR Mobile LIKE " +
                        parameterName +
                        " ESCAPE '\\' " +

                        " OR Email LIKE " +
                        parameterName +
                        " ESCAPE '\\' " +

                        " OR ApplicationNo LIKE " +
                        parameterName +
                        " ESCAPE '\\'" +

                        ")"
                    );

                    var p =
                        new SqlParameter(
                            parameterName,
                            SqlDbType.NVarChar,
                            210
                        );

                    p.Value = "%" + token + "%";

                    ps.Add(p);
                }
            }

            /* =========================================================
               POST FILTER
               ========================================================= */

            if (!string.IsNullOrWhiteSpace(q.Post))
            {
                sb.Append(
                    " AND DesiredPost = @post"
                );

                var p =
                    new SqlParameter(
                        "@post",
                        SqlDbType.NVarChar,
                        100
                    );

                p.Value = q.Post;

                ps.Add(p);
            }

            /* =========================================================
               STATUS FILTER
               ========================================================= */

            if (!string.IsNullOrWhiteSpace(q.Status))
            {
                sb.Append(
                    " AND Status = @statusFilter"
                );

                var p =
                    new SqlParameter(
                        "@statusFilter",
                        SqlDbType.NVarChar,
                        20
                    );

                p.Value = q.Status;

                ps.Add(p);
            }

            /* =========================================================
               DATE FILTER
               ========================================================= */

            if (q.Date.HasValue)
            {
                sb.Append(
                    " AND ApplicationDate = @date"
                );

                var p =
                    new SqlParameter(
                        "@date",
                        SqlDbType.Date
                    );

                p.Value = q.Date.Value.Date;

                ps.Add(p);
            }

            return sb.ToString();
        }

        private static string EscapeLike(string value)
        {
            return (value ?? "")
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_")
                .Replace("[", "\\[");
        }

        // A SqlParameter object can belong to only one command, so clone it for each command.
        private static void AddParams(SqlCommand cmd, List<SqlParameter> source)
        {
            foreach (var p in source)
            {
                var copy = new SqlParameter(p.ParameterName, p.SqlDbType, p.Size);
                copy.Value = p.Value;
                cmd.Parameters.Add(copy);
            }
        }

        private static string Str(IDataRecord r, string column)
        {
            int i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i));
        }
    }
}

