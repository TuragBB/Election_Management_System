using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace NominationSystem.Models
{
    // ============================================================ view model
    /// <summary>Election list e ekta election er sankhipto tothyo (dropdown er jonno).</summary>
    public class ElectionListItem
    {
        public int ElectionId { get; set; }
        public int ElectionYear { get; set; }
        public string Title { get; set; }
        public string Phase { get; set; }
        public string PhaseText { get; set; }
    }

    /// <summary>Election settings page er data + current voting phase.</summary>
    public class ElectionSettingsViewModel
    {
        public bool HasElection { get; set; }

        /// <summary>true = "notun election toiri" mode (ekhono DB te save hoyni)</summary>
        public bool IsNew { get; set; }

        public int ElectionId { get; set; }
        public int ElectionYear { get; set; }
        public string Title { get; set; }
        public string TermText { get; set; }
        public string CommitteeName { get; set; }
        public string EligibilityNote { get; set; }
        public string Status { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime DbNow { get; set; }

        /// <summary>none | draft | closed | done | scheduled | running | ended</summary>
        public string Phase { get; set; }
        public string PhaseText { get; set; }

        // ---- summary numbers (page er upor er stat card)
        public int PostCount { get; set; }
        public int PendingNominations { get; set; }
        public int ApprovedNominations { get; set; }
        public int CandidateCount { get; set; }
        public int BallotCount { get; set; }

        public List<ElectionListItem> Elections { get; set; }

        public bool IsOpen
        {
            get { return string.Equals(Status, "Open", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>value for &lt;input type="datetime-local"&gt;</summary>
        public string StartValue
        {
            get { return StartAt.HasValue ? StartAt.Value.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture) : ""; }
        }

        public string EndValue
        {
            get { return EndAt.HasValue ? EndAt.Value.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture) : ""; }
        }

        public ElectionSettingsViewModel()
        {
            Phase = "none";
            PhaseText = "কোনো নির্বাচন সংরক্ষিত নেই";
            Elections = new List<ElectionListItem>();
        }
    }

    public class ElectionSettingsInput
    {
        public int ElectionId { get; set; }
        public string Title { get; set; }
        public string Term { get; set; }
        public string Committee { get; set; }
        public string Note { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
    }

    public class ElectionSettingsResult
    {
        public bool Success { get; set; }
        public bool Created { get; set; }
        public string Message { get; set; }
        public ElectionSettingsViewModel State { get; set; }
    }

    // ============================================================ repository
    /// <summary>Election settings data access (plain ADO.NET, VotingRepository er moto).</summary>
    public class ElectionSettingsRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["NominationDB"].ConnectionString;

        private const string StatusOpen = "Open";
        private const string StatusClosed = "Closed";

        // ------------------------------------------------------------------ read
        /// <summary>Current election: Open ta age, na thakle sobcheye notun ta.</summary>
        public ElectionSettingsViewModel GetCurrent()
        {
            return Get(null);
        }

        /// <summary>Nirdishto election (id) - na pele current election.</summary>
        public ElectionSettingsViewModel Get(int? electionId)
        {
            using (var con = new SqlConnection(connectionString))
            {
                con.Open();

                var m = Load(con, null, electionId, false);

                if (!m.HasElection && electionId.HasValue)
                {
                    m = Load(con, null, null, false);
                }

                FillExtras(con, m);
                return m;
            }
        }

        /// <summary>Khali form - "notun election toiri" mode.</summary>
        public ElectionSettingsViewModel GetNew()
        {
            using (var con = new SqlConnection(connectionString))
            {
                con.Open();

                var m = new ElectionSettingsViewModel
                {
                    IsNew = true,
                    Phase = "draft",
                    PhaseText = "নতুন নির্বাচন • এখনো সংরক্ষণ করা হয়নি"
                };

                using (var cmd = new SqlCommand("SELECT SYSDATETIME()", con))
                {
                    m.DbNow = Convert.ToDateTime(cmd.ExecuteScalar());
                }

                FillExtras(con, m);
                return m;
            }
        }

        private static ElectionSettingsViewModel Load(SqlConnection con, SqlTransaction tx, int? electionId, bool lockRow)
        {
            string sql =
                "SELECT TOP 1 ElectionId, ElectionYear, Title, Status, StartAt, EndAt, " +
                "       TermText, CommitteeName, EligibilityNote, SYSDATETIME() AS DbNow " +
                "FROM dbo.Elections " + (lockRow ? "WITH (UPDLOCK, ROWLOCK) " : "") +
                "WHERE (@id IS NULL OR ElectionId = @id) " +
                "ORDER BY CASE WHEN Status = N'Open' THEN 0 ELSE 1 END, ElectionYear DESC, ElectionId DESC";

            var m = new ElectionSettingsViewModel();

            using (var cmd = new SqlCommand(sql, con, tx))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value =
                    electionId.HasValue ? (object)electionId.Value : DBNull.Value;

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        m.DbNow = DateTime.Now;
                        return m;     // HasElection = false
                    }

                    m.HasElection = true;
                    m.ElectionId = Convert.ToInt32(r["ElectionId"]);
                    m.ElectionYear = Convert.ToInt32(r["ElectionYear"]);
                    m.Title = Convert.ToString(r["Title"]);
                    m.Status = Convert.ToString(r["Status"]);
                    m.StartAt = r["StartAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["StartAt"]);
                    m.EndAt = r["EndAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["EndAt"]);
                    m.TermText = r["TermText"] == DBNull.Value ? "" : Convert.ToString(r["TermText"]);
                    m.CommitteeName = r["CommitteeName"] == DBNull.Value ? "" : Convert.ToString(r["CommitteeName"]);
                    m.EligibilityNote = r["EligibilityNote"] == DBNull.Value ? "" : Convert.ToString(r["EligibilityNote"]);
                    m.DbNow = Convert.ToDateTime(r["DbNow"]);
                }
            }

            ApplyPhase(m);
            return m;
        }

        /// <summary>Election list (dropdown) + stat number gulo. Sudhu page load e lage.</summary>
        private static void FillExtras(SqlConnection con, ElectionSettingsViewModel m)
        {
            // ---- sob election
            m.Elections = new List<ElectionListItem>();

            const string listSql =
                "SELECT ElectionId, ElectionYear, Title, Status, StartAt, EndAt " +
                "FROM dbo.Elections ORDER BY ElectionYear DESC, ElectionId DESC";

            using (var cmd = new SqlCommand(listSql, con))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var tmp = new ElectionSettingsViewModel
                    {
                        HasElection = true,
                        Status = Convert.ToString(r["Status"]),
                        StartAt = r["StartAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["StartAt"]),
                        EndAt = r["EndAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["EndAt"]),
                        DbNow = m.DbNow
                    };
                    ApplyPhase(tmp);

                    m.Elections.Add(new ElectionListItem
                    {
                        ElectionId = Convert.ToInt32(r["ElectionId"]),
                        ElectionYear = Convert.ToInt32(r["ElectionYear"]),
                        Title = Convert.ToString(r["Title"]),
                        Phase = tmp.Phase,
                        PhaseText = tmp.PhaseText
                    });
                }
            }

            if (!m.HasElection) return;

            // ---- election er sankhya (pod / candidate / ballot)
            const string statSql =
                "SELECT " +
                "  (SELECT COUNT(*) FROM dbo.ElectionPosts WHERE ElectionId = @e) AS Posts, " +
                "  (SELECT COUNT(*) FROM dbo.ElectionCandidates WHERE ElectionId = @e AND IsActive = 1) AS Cands, " +
                "  (SELECT COUNT(*) FROM dbo.Ballots WHERE ElectionId = @e) AS Ballots";

            using (var cmd = new SqlCommand(statSql, con))
            {
                cmd.Parameters.Add("@e", SqlDbType.Int).Value = m.ElectionId;
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        m.PostCount = Convert.ToInt32(r["Posts"]);
                        m.CandidateCount = Convert.ToInt32(r["Cands"]);
                        m.BallotCount = Convert.ToInt32(r["Ballots"]);
                    }
                }
            }

            // ---- mononoyon abedon (Nominations.ElectionId column lagbe - Election_Upgrade.sql dile thakbe)
            try
            {
                const string nomSql =
                    "SELECT " +
                    "  SUM(CASE WHEN Status = N'Approved' THEN 1 ELSE 0 END) AS Approved, " +
                    "  SUM(CASE WHEN Status = N'Pending'  THEN 1 ELSE 0 END) AS Pending " +
                    "FROM dbo.Nominations WHERE ElectionId = @e";

                using (var cmd = new SqlCommand(nomSql, con))
                {
                    cmd.Parameters.Add("@e", SqlDbType.Int).Value = m.ElectionId;
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            m.ApprovedNominations = r["Approved"] == DBNull.Value ? 0 : Convert.ToInt32(r["Approved"]);
                            m.PendingNominations = r["Pending"] == DBNull.Value ? 0 : Convert.ToInt32(r["Pending"]);
                        }
                    }
                }
            }
            catch (SqlException)
            {
                // column ekhono nai - stat 0 thakbe, page thik moto load hobe
            }
        }

        /// <summary>Phase SQL Server er ghori (DbNow) diye dhora hoy - VotingRepository er rule ei same.</summary>
        private static void ApplyPhase(ElectionSettingsViewModel m)
        {
            if (!m.HasElection)
            {
                m.Phase = "none";
                m.PhaseText = "কোনো নির্বাচন সংরক্ষিত নেই";
            }
            else if (!m.IsOpen)
            {
                if (!m.StartAt.HasValue || !m.EndAt.HasValue)
                {
                    m.Phase = "draft";
                    m.PhaseText = "খসড়া • সময়সূচি নির্ধারণ বাকি";
                }
                else if (m.DbNow > m.EndAt.Value)
                {
                    m.Phase = "done";
                    m.PhaseText = "ভোটগ্রহণ সম্পন্ন";
                }
                else
                {
                    m.Phase = "closed";
                    m.PhaseText = "ভোটগ্রহণ বন্ধ আছে";
                }
            }
            else if (m.StartAt.HasValue && m.DbNow < m.StartAt.Value)
            {
                m.Phase = "scheduled";
                m.PhaseText = "চালু আছে • শুরুর অপেক্ষায়";
            }
            else if (m.EndAt.HasValue && m.DbNow > m.EndAt.Value)
            {
                m.Phase = "ended";
                m.PhaseText = "ভোটগ্রহণের সময় শেষ";
            }
            else
            {
                m.Phase = "running";
                m.PhaseText = "ভোটগ্রহণ চলছে";
            }
        }

        // ------------------------------------------------------------------ save
        /// <summary>
        /// Settings save. ElectionId = 0 hole notun election toiri, na hole update.
        /// Shuru ebong shesh somoy dile ballot nijei nirdharito somoy onujayi chalu / bondho hoy.
        /// </summary>
        public ElectionSettingsResult Save(ElectionSettingsInput input, string updatedBy)
        {
            string error = Normalize(input);
            if (error != null) return Fail(error);

            int? parsedYear = ExtractYear(input.Term);

            try
            {
                using (var con = new SqlConnection(connectionString))
                {
                    con.Open();

                    using (var tx = con.BeginTransaction())
                    {
                        int id = input.ElectionId;
                        bool created = false;
                        ElectionSettingsViewModel existing = null;

                        DateTime dbNow;
                        using (var cmd = new SqlCommand("SELECT SYSDATETIME()", con, tx))
                        {
                            dbNow = Convert.ToDateTime(cmd.ExecuteScalar());
                        }

                        if (id > 0)
                        {
                            existing = Load(con, tx, id, true);
                            if (!existing.HasElection)
                            {
                                tx.Rollback();
                                return Fail("নির্বাচনটি খুঁজে পাওয়া যায়নি। পেজটি রিফ্রেশ করুন।");
                            }

                            const string updateSql =
                                "UPDATE dbo.Elections SET " +
                                "  Title = @title, " +
                                "  ElectionYear = ISNULL(@year, ElectionYear), " +
                                "  TermText = @term, CommitteeName = @committee, EligibilityNote = @note, " +
                                "  StartAt = @start, EndAt = @end, " +
                                "  UpdatedAt = SYSDATETIME(), UpdatedBy = @by " +
                                "WHERE ElectionId = @id";

                            using (var cmd = new SqlCommand(updateSql, con, tx))
                            {
                                AddCommonParams(cmd, input, parsedYear, updatedBy);
                                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // notun election : shuru te Closed, niche somoysuchi onujayi Open hobe
                            const string insertSql =
                                "INSERT INTO dbo.Elections " +
                                "  (ElectionYear, Title, ShortTitle, Status, StartAt, EndAt, CreatedAt, " +
                                "   TermText, CommitteeName, EligibilityNote, UpdatedAt, UpdatedBy) " +
                                "VALUES " +
                                "  (ISNULL(@year, @fallbackYear), @title, @shortTitle, N'Closed', @start, @end, SYSDATETIME(), " +
                                "   @term, @committee, @note, SYSDATETIME(), @by); " +
                                "SELECT CAST(SCOPE_IDENTITY() AS int);";

                            using (var cmd = new SqlCommand(insertSql, con, tx))
                            {
                                AddCommonParams(cmd, input, parsedYear, updatedBy);

                                int fallbackYear = input.StartAt.HasValue ? input.StartAt.Value.Year : dbNow.Year;
                                cmd.Parameters.Add("@fallbackYear", SqlDbType.Int).Value = fallbackYear;
                                cmd.Parameters.Add("@shortTitle", SqlDbType.NVarChar, 100).Value = Truncate(input.Title, 100);

                                id = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            created = true;
                            CopyPosts(con, tx, id);

                            // kono election chhara jama porha mononoyon gulo ei election er hoye jabe
                            using (var cmd = new SqlCommand(
                                "UPDATE dbo.Nominations SET ElectionId = @id WHERE ElectionId IS NULL", con, tx))
                            {
                                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // ---------- vote chalu / bondho : somoysuchi theke nijei thik hoy
                        bool hasSchedule = input.StartAt.HasValue && input.EndAt.HasValue;
                        bool existingOpen = existing != null && existing.IsOpen;
                        bool datesChanged = existing == null
                            || !SameMinute(existing.StartAt, input.StartAt)
                            || !SameMinute(existing.EndAt, input.EndAt);

                        string newStatus;
                        if (!hasSchedule)
                        {
                            newStatus = StatusClosed;                     // somoy chhara vote chalu thakte pare na
                        }
                        else if (input.EndAt.Value <= dbNow)
                        {
                            newStatus = existingOpen ? StatusOpen : StatusClosed;   // somoy ager - notun kore chalu hobe na
                        }
                        else if (existing == null || existingOpen || datesChanged)
                        {
                            newStatus = StatusOpen;
                        }
                        else
                        {
                            newStatus = StatusClosed;                     // joruri bondho kora election, somoy bodlayni
                        }

                        if (newStatus == StatusOpen)
                        {
                            // onno election ekhon cholche ki ?
                            string runningTitle = null;
                            using (var cmd = new SqlCommand(
                                "SELECT TOP 1 Title FROM dbo.Elections " +
                                "WHERE Status = N'Open' AND ElectionId <> @id " +
                                "  AND (StartAt IS NULL OR StartAt <= SYSDATETIME()) " +
                                "  AND (EndAt IS NULL OR EndAt >= SYSDATETIME())", con, tx))
                            {
                                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                                object o = cmd.ExecuteScalar();
                                if (o != null && o != DBNull.Value) runningTitle = Convert.ToString(o);
                            }

                            if (runningTitle != null)
                            {
                                tx.Rollback();
                                return Fail("\"" + runningTitle + "\" নির্বাচনের ভোটগ্রহণ এখন চলছে। " +
                                            "আগে সেটি জরুরি বন্ধ করুন, অথবা সেটির সময় শেষ হওয়ার পর সংরক্ষণ করুন।");
                            }

                            // ballot page TOP 1 Open election dhore, tai ekshathe ekta-i Open thakbe
                            using (var cmd = new SqlCommand(
                                "UPDATE dbo.Elections SET Status = N'Closed' WHERE Status = N'Open' AND ElectionId <> @id", con, tx))
                            {
                                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                                cmd.ExecuteNonQuery();
                            }
                        }

                        using (var cmd = new SqlCommand(
                            "UPDATE dbo.Elections SET Status = @st WHERE ElectionId = @id", con, tx))
                        {
                            cmd.Parameters.Add("@st", SqlDbType.NVarChar, 20).Value = newStatus;
                            cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                            cmd.ExecuteNonQuery();
                        }

                        var state = Load(con, tx, id, false);
                        tx.Commit();

                        ClearResultsCache(id);

                        string msg;
                        if (created) msg = "নতুন নির্বাচন তৈরি করা হয়েছে।";
                        else msg = "নির্বাচনের সেটিংস সফলভাবে সংরক্ষণ করা হয়েছে।";

                        if (state.IsOpen) msg += " নির্ধারিত সময় অনুযায়ী ভোটগ্রহণ চলবে।";

                        return new ElectionSettingsResult
                        {
                            Success = true,
                            Created = created,
                            Message = msg,
                            State = state
                        };
                    }
                }
            }
            catch (SqlException ex)
            {
                // 2601 / 2627 = unique constraint (same year e arekta election ache)
                if (ex.Number == 2601 || ex.Number == 2627)
                {
                    return Fail("এই বছরের একটি নির্বাচন আগে থেকেই আছে। বছর ও মেয়াদ পরিবর্তন করে আবার চেষ্টা করুন।");
                }

                if (ex.Number == 207 || ex.Number == 208)
                {
                    return Fail("ডাটাবেস আপডেট করা হয়নি। প্রথমে Election_Upgrade.sql স্ক্রিপ্টটি NominationDB তে চালান।");
                }

                throw;
            }
        }

        // ------------------------------------------------------------------ emergency close
        /// <summary>Joruri obosthay vote sathe sathe bondho (Status = Closed).</summary>
        public ElectionSettingsResult EmergencyClose(int electionId, string updatedBy)
        {
            if (electionId <= 0)
            {
                return Fail("আগে নির্বাচনের তথ্য ও সময়সূচি সংরক্ষণ করুন।");
            }

            using (var con = new SqlConnection(connectionString))
            {
                con.Open();

                using (var tx = con.BeginTransaction())
                {
                    var m = Load(con, tx, electionId, true);

                    if (!m.HasElection)
                    {
                        tx.Rollback();
                        return Fail("নির্বাচনটি খুঁজে পাওয়া যায়নি। পেজটি রিফ্রেশ করুন।");
                    }

                    if (!m.IsOpen)
                    {
                        tx.Rollback();
                        return Fail("ভোটগ্রহণ আগে থেকেই বন্ধ আছে।");
                    }

                    using (var cmd = new SqlCommand(
                        "UPDATE dbo.Elections SET Status = N'Closed', UpdatedAt = SYSDATETIME(), UpdatedBy = @by WHERE ElectionId = @id",
                        con, tx))
                    {
                        cmd.Parameters.Add("@by", SqlDbType.NVarChar, 100).Value = Truncate(updatedBy, 100) ?? "Admin";
                        cmd.Parameters.Add("@id", SqlDbType.Int).Value = electionId;
                        cmd.ExecuteNonQuery();
                    }

                    var state = Load(con, tx, electionId, false);
                    tx.Commit();

                    ClearResultsCache(electionId);

                    return new ElectionSettingsResult
                    {
                        Success = true,
                        Message = "জরুরি ভিত্তিতে ভোটগ্রহণ বন্ধ করা হয়েছে।",
                        State = state
                    };
                }
            }
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Notun election e pod gulo copy: sobcheye notun (onno) election theke. Na thakle default 7 ti pod.</summary>
        private static void CopyPosts(SqlConnection con, SqlTransaction tx, int newElectionId)
        {
            const string copySql =
                "DECLARE @src INT = (SELECT TOP 1 e.ElectionId FROM dbo.Elections e " +
                "                    WHERE e.ElectionId <> @new " +
                "                      AND EXISTS (SELECT 1 FROM dbo.ElectionPosts p WHERE p.ElectionId = e.ElectionId) " +
                "                    ORDER BY e.ElectionYear DESC, e.ElectionId DESC); " +
                "IF @src IS NOT NULL " +
                "  INSERT INTO dbo.ElectionPosts (ElectionId, PostName, DisplayName, DisplayNameEn, SortOrder, MaxVotes) " +
                "  SELECT @new, PostName, DisplayName, DisplayNameEn, SortOrder, MaxVotes " +
                "  FROM dbo.ElectionPosts WHERE ElectionId = @src; " +
                "SELECT COUNT(*) FROM dbo.ElectionPosts WHERE ElectionId = @new;";

            int count;
            using (var cmd = new SqlCommand(copySql, con, tx))
            {
                cmd.Parameters.Add("@new", SqlDbType.Int).Value = newElectionId;
                count = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (count > 0) return;

            // kono election e pod nai (prothom election) - nomination form er pod gulo diye shuru
            string[,] defaults =
            {
                { "সভাপতি", "President" },
                { "সহ-সভাপতি", "Vice President" },
                { "সাধারণ সম্পাদক", "General Secretary" },
                { "যুগ্ম সম্পাদক", "Joint Secretary" },
                { "কোষাধ্যক্ষ", "Treasurer" },
                { "সহকারী কোষাধ্যক্ষ", "Assistant Treasurer" },
                { "সম্মানিত সদস্য", "Honorary Member" }
            };

            for (int i = 0; i < defaults.GetLength(0); i++)
            {
                using (var cmd = new SqlCommand(
                    "INSERT INTO dbo.ElectionPosts (ElectionId, PostName, DisplayName, DisplayNameEn, SortOrder, MaxVotes) " +
                    "VALUES (@e, @n, @n, @en, @o, 1)", con, tx))
                {
                    cmd.Parameters.Add("@e", SqlDbType.Int).Value = newElectionId;
                    cmd.Parameters.Add("@n", SqlDbType.NVarChar, 100).Value = defaults[i, 0];
                    cmd.Parameters.Add("@en", SqlDbType.NVarChar, 100).Value = defaults[i, 1];
                    cmd.Parameters.Add("@o", SqlDbType.Int).Value = i + 1;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static bool SameMinute(DateTime? a, DateTime? b)
        {
            if (a.HasValue != b.HasValue) return false;
            if (!a.HasValue) return true;

            return a.Value.Year == b.Value.Year && a.Value.Month == b.Value.Month &&
                   a.Value.Day == b.Value.Day && a.Value.Hour == b.Value.Hour &&
                   a.Value.Minute == b.Value.Minute;
        }

        private static void AddCommonParams(SqlCommand cmd, ElectionSettingsInput input, int? parsedYear, string updatedBy)
        {
            cmd.Parameters.Add("@title", SqlDbType.NVarChar, 200).Value = input.Title;
            cmd.Parameters.Add("@year", SqlDbType.Int).Value = parsedYear.HasValue ? (object)parsedYear.Value : DBNull.Value;
            cmd.Parameters.Add("@term", SqlDbType.NVarChar, 50).Value = NullIfEmpty(input.Term);
            cmd.Parameters.Add("@committee", SqlDbType.NVarChar, 200).Value = NullIfEmpty(input.Committee);
            cmd.Parameters.Add("@note", SqlDbType.NVarChar, 1000).Value = NullIfEmpty(input.Note);
            cmd.Parameters.Add("@start", SqlDbType.DateTime2).Value = input.StartAt.HasValue ? (object)input.StartAt.Value : DBNull.Value;
            cmd.Parameters.Add("@end", SqlDbType.DateTime2).Value = input.EndAt.HasValue ? (object)input.EndAt.Value : DBNull.Value;
            cmd.Parameters.Add("@by", SqlDbType.NVarChar, 100).Value = Truncate(updatedBy, 100) ?? "Admin";
        }

        /// <summary>Trim + validate. null = thik ache, na hole Bangla error message.</summary>
        private static string Normalize(ElectionSettingsInput input)
        {
            if (input == null) return "কোনো তথ্য পাওয়া যায়নি।";

            input.Title = (input.Title ?? "").Trim();
            input.Term = (input.Term ?? "").Trim();
            input.Committee = (input.Committee ?? "").Trim();
            input.Note = (input.Note ?? "").Trim();

            if (input.Title.Length == 0) return "নির্বাচনের নাম ও টাইটেল দিন।";
            if (input.Title.Length > 200) return "নির্বাচনের নাম ২০০ অক্ষরের বেশি হতে পারবে না।";
            if (input.Term.Length > 50) return "বছর ও মেয়াদ ৫০ অক্ষরের বেশি হতে পারবে না।";
            if (input.Committee.Length > 200) return "পরিচালনা পরিষদ / সেলের নাম ২০০ অক্ষরের বেশি হতে পারবে না।";
            if (input.Note.Length > 1000) return "ভোটার যোগ্যতা ও নোট ১০০০ অক্ষরের বেশি হতে পারবে না।";

            if (input.StartAt.HasValue != input.EndAt.HasValue)
            {
                return input.StartAt.HasValue
                    ? "ভোট সমাপ্তির তারিখ ও সময় নির্ধারণ করুন।"
                    : "ভোট শুরুর তারিখ ও সময় নির্ধারণ করুন।";
            }

            if (input.StartAt.HasValue && input.EndAt.Value <= input.StartAt.Value)
            {
                return "ভোট সমাপ্তির সময় অবশ্যই শুরুর সময়ের পরে হতে হবে।";
            }

            return null;
        }

        /// <summary>"২০২৭ - ২০৩০" theke prothom 4 digit er sal (2027) ber kore. Na pele null.</summary>
        public static int? ExtractYear(string term)
        {
            if (string.IsNullOrEmpty(term)) return null;

            var sb = new StringBuilder(term.Length);
            foreach (char c in term)
            {
                if (c >= '\u09E6' && c <= '\u09EF') sb.Append((char)('0' + (c - '\u09E6')));   // Bangla digit -> 0-9
                else sb.Append(c);
            }

            var m = Regex.Match(sb.ToString(), @"(?<!\d)(\d{4})(?!\d)");
            if (!m.Success) return null;

            int y = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            return (y >= 2000 && y <= 2100) ? (int?)y : null;
        }

        /// <summary>Results page 3 sec cache kore; save er por jate sathe sathe notun status dekhay.</summary>
        private static void ClearResultsCache(int electionId)
        {
            HttpRuntime.Cache.Remove("swf.results.0");
            HttpRuntime.Cache.Remove("swf.results." + electionId);
        }

        /// <summary>Page load e DB error hole developer er jonno bujhar moto Bangla message.</summary>
        public static string DescribeError(Exception ex)
        {
            var sql = ex as SqlException;
            if (sql != null && (sql.Number == 207 || sql.Number == 208))
            {
                return "ডাটাবেসে নতুন কলাম বা টেবিল পাওয়া যায়নি। প্রথমে ElectionSettings_Setup.sql ও Election_Upgrade.sql স্ক্রিপ্ট NominationDB তে চালান।";
            }

            return "নির্বাচনের তথ্য লোড করা যায়নি। ডাটাবেস সংযোগ যাচাই করে পেজটি রিফ্রেশ করুন।";
        }

        private static ElectionSettingsResult Fail(string message)
        {
            return new ElectionSettingsResult { Success = false, Message = message };
        }

        private static object NullIfEmpty(string s)
        {
            return string.IsNullOrEmpty(s) ? (object)DBNull.Value : s;
        }

        private static string Truncate(string s, int max)
        {
            if (s == null) return null;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
