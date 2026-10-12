using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Caching;

namespace NominationSystem.Models
{
    /// <summary>
    /// Live / final result (plain ADO.NET, VotingRepository er moto).
    /// - Result 3 second server-e cache hoy.
    /// - Vote shesh hole winner / tie dbo.ElectionResults e save hoy.
    /// - Tie hole admin winner select kore -> dbo.ElectionTieResolutions e +1 vote jog hoy.
    /// - Gazette onumodon dbo.ElectionGazette e save hoy.
    /// </summary>
    public class ResultsRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["NominationDB"].ConnectionString;

        public ResultsViewModel Get(int? electionId)
        {
            string key = "swf.results." + (electionId.HasValue ? electionId.Value : 0);

            var cached = HttpRuntime.Cache[key] as ResultsViewModel;
            if (cached != null) return cached;

            var model = Load(electionId);

            HttpRuntime.Cache.Insert(key, model, null,
                DateTime.UtcNow.AddSeconds(3), Cache.NoSlidingExpiration);

            return model;
        }

        private static void ClearCache(int electionId)
        {
            HttpRuntime.Cache.Remove("swf.results.0");
            HttpRuntime.Cache.Remove("swf.results." + electionId);
        }

        private ResultsViewModel Load(int? electionId)
        {
            var model = new ResultsViewModel();
            DateTime dbNow;
            DateTime? startAt;
            DateTime? endAt;

            using (var con = new SqlConnection(connectionString))
            {
                con.Open();

                // 1) election : Open ta age, na thakle sobcheye notun ta
                const string electionSql =
                    "SELECT TOP 1 ElectionId, ElectionYear, Title, ShortTitle, Status, StartAt, EndAt, SYSDATETIME() AS DbNow " +
                    "FROM dbo.Elections " +
                    "WHERE (@e IS NULL OR ElectionId = @e) " +
                    "ORDER BY CASE WHEN Status = N'Open' THEN 0 ELSE 1 END, ElectionYear DESC, ElectionId DESC";

                using (var cmd = new SqlCommand(electionSql, con))
                {
                    cmd.Parameters.Add("@e", SqlDbType.Int).Value =
                        electionId.HasValue ? (object)electionId.Value : DBNull.Value;

                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return model;     // HasElection = false

                        model.HasElection = true;
                        model.ElectionId = Convert.ToInt32(r["ElectionId"]);
                        model.ElectionYear = Convert.ToInt32(r["ElectionYear"]);
                        model.Title = Convert.ToString(r["Title"]);
                        model.ShortTitle = Convert.ToString(r["ShortTitle"]);
                        model.Status = Convert.ToString(r["Status"]);
                        startAt = r["StartAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["StartAt"]);
                        endAt = r["EndAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["EndAt"]);
                        dbNow = Convert.ToDateTime(r["DbNow"]);
                    }
                }

                // phase (SQL Server er ghori diye)
                bool isOpen = string.Equals(model.Status, "Open", StringComparison.OrdinalIgnoreCase);
                if (isOpen)
                {
                    if (startAt.HasValue && dbNow < startAt.Value) model.Phase = VotePhase.NotStarted;
                    else if (endAt.HasValue && dbNow > endAt.Value) model.Phase = VotePhase.Ended;
                    else model.Phase = VotePhase.Running;
                }
                else
                {
                    string s = (model.Status ?? "").ToLowerInvariant();
                    bool closed = s == "closed" || s == "completed" || s == "finished" || s == "ended" || s == "published";
                    model.Phase = closed ? VotePhase.Ended : VotePhase.NotStarted;
                }

                model.StartText = startAt.HasValue ? Bn.DateTimeText(startAt.Value) : "";
                model.EndText = endAt.HasValue ? Bn.DateTimeText(endAt.Value) : "";
                model.UpdatedText = Bn.TimeText(dbNow);

                // 1b) gazette onumodon hoyeche ki na
                using (var cmd = new SqlCommand(
                    "SELECT ApprovedAt, ApprovedBy FROM dbo.ElectionGazette WHERE ElectionId = @e", con))
                {
                    cmd.Parameters.Add("@e", SqlDbType.Int).Value = model.ElectionId;
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            model.GazetteApproved = true;
                            model.GazetteApprovedBy = Convert.ToString(r["ApprovedBy"]);
                            model.GazetteApprovedText = Bn.DateTimeText(Convert.ToDateTime(r["ApprovedAt"]));
                        }
                    }
                }

                // 2) mot ballot
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Ballots WHERE ElectionId = @e", con))
                {
                    cmd.Parameters.Add("@e", SqlDbType.Int).Value = model.ElectionId;
                    model.TotalBallots = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // 3) pod
                var posts = new Dictionary<int, ResultPost>();

                using (var cmd = new SqlCommand(
                    "SELECT PostId, DisplayName, DisplayNameEn, SortOrder, MaxVotes " +
                    "FROM dbo.ElectionPosts WHERE ElectionId = @e ORDER BY SortOrder, PostId", con))
                {
                    cmd.Parameters.Add("@e", SqlDbType.Int).Value = model.ElectionId;
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var p = new ResultPost
                            {
                                PostId = Convert.ToInt32(r["PostId"]),
                                DisplayName = Convert.ToString(r["DisplayName"]),
                                DisplayNameEn = Convert.ToString(r["DisplayNameEn"]),
                                SortOrder = Convert.ToInt32(r["SortOrder"]),
                                Seats = Math.Max(1, Convert.ToInt32(r["MaxVotes"]))
                            };
                            posts[p.PostId] = p;
                            model.Posts.Add(p);
                        }
                    }
                }

                // 4) prottek pode koyta ballot-e vote poreche (distinct)
                using (var cmd = new SqlCommand(
                    "SELECT PostId, COUNT(DISTINCT BallotId) AS Ballots " +
                    "FROM dbo.BallotVotes WHERE ElectionId = @e GROUP BY PostId", con))
                {
                    cmd.Parameters.Add("@e", SqlDbType.Int).Value = model.ElectionId;
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            ResultPost p;
                            if (posts.TryGetValue(Convert.ToInt32(r["PostId"]), out p))
                                p.ValidVotes = Convert.ToInt32(r["Ballots"]);
                        }
                    }
                }

                // 5) prarthi + vote count + tie-break vote
                const string candSql =
                    "SELECT c.CandidateId, c.PostId, c.BallotNo, n.CandidateName, n.MemberNo, n.Designation, " +
                    "       CASE WHEN n.Photo IS NULL THEN 0 ELSE 1 END AS HasPhoto, " +
                    "       ISNULL(v.Votes, 0) AS RealVotes, " +
                    "       ISNULL(t.Bonus, 0) AS Bonus " +
                    "FROM dbo.ElectionCandidates c " +
                    "JOIN dbo.Nominations n ON n.NominationId = c.NominationId " +
                    "LEFT JOIN (SELECT CandidateId, COUNT(*) AS Votes FROM dbo.BallotVotes " +
                    "           WHERE ElectionId = @e GROUP BY CandidateId) v ON v.CandidateId = c.CandidateId " +
                    "LEFT JOIN (SELECT CandidateId, SUM(BonusVotes) AS Bonus FROM dbo.ElectionTieResolutions " +
                    "           WHERE ElectionId = @e GROUP BY CandidateId) t ON t.CandidateId = c.CandidateId " +
                    "WHERE c.ElectionId = @e AND c.IsActive = 1 " +
                    "ORDER BY c.PostId, c.BallotNo";

                using (var cmd = new SqlCommand(candSql, con))
                {
                    cmd.Parameters.Add("@e", SqlDbType.Int).Value = model.ElectionId;
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            ResultPost p;
                            if (!posts.TryGetValue(Convert.ToInt32(r["PostId"]), out p)) continue;

                            int real = Convert.ToInt32(r["RealVotes"]);
                            int bonus = Convert.ToInt32(r["Bonus"]);

                            p.Candidates.Add(new ResultCandidate
                            {
                                CandidateId = Convert.ToInt32(r["CandidateId"]),
                                BallotNo = Convert.ToInt32(r["BallotNo"]),
                                Name = Convert.ToString(r["CandidateName"]),
                                MemberNo = Convert.ToString(r["MemberNo"]),
                                Designation = Convert.ToString(r["Designation"]),
                                HasPhoto = Convert.ToInt32(r["HasPhoto"]) == 1,
                                RealVotes = real,
                                Bonus = bonus,
                                Votes = real + bonus
                            });
                        }
                    }
                }
            }

            // prarthi nai emon pod bad
            model.Posts = model.Posts.Where(p => p.Candidates.Count > 0).ToList();

            Compute(model);

            // vote shesh hole winner / tie database e save
            if (model.Phase == VotePhase.Ended)
            {
                SaveSnapshot(model);
            }

            return model;
        }

        // ------------------------------------------------------------------ ranking / tie / remarks
        private static void Compute(ResultsViewModel m)
        {
            bool ended = m.Phase == VotePhase.Ended;

            int eligible;
            int.TryParse(ConfigurationManager.AppSettings["Election.TotalVoters"], out eligible);
            m.EligibleVoters = eligible < 0 ? 0 : eligible;
            m.Turnout = (m.EligibleVoters > 0) ? Math.Min(100.0, m.TotalBallots * 100.0 / m.EligibleVoters) : 0;

            m.TotalPosts = m.Posts.Count;
            long sumVotes = 0;

            foreach (var p in m.Posts)
            {
                // votes DESC, tarpor ballot no
                p.Candidates = p.Candidates.OrderByDescending(c => c.Votes).ThenBy(c => c.BallotNo).ToList();

                int seats = Math.Min(p.Seats, p.Candidates.Count);
                p.Seats = seats;

                int bonusSum = p.Candidates.Sum(c => c.Bonus);
                p.HasTieBreak = bonusSum > 0;

                // ValidVotes : distinct ballot + tie-break vote (fallback: vote er jog)
                int total = p.Candidates.Sum(c => c.Votes);
                if (p.ValidVotes <= 0) p.ValidVotes = total;
                else p.ValidVotes += bonusSum;
                sumVotes += total;

                // rank (same vote = same rank)
                int rank = 0, prevVotes = -1;
                for (int i = 0; i < p.Candidates.Count; i++)
                {
                    var c = p.Candidates[i];
                    if (c.Votes != prevVotes) { rank = i + 1; prevVotes = c.Votes; }
                    c.Rank = rank;
                    c.Percent = p.ValidVotes > 0 ? c.Votes * 100.0 / p.ValidVotes : 0;
                }

                // seat sima-y tie ? (shudhu vote shesh hole)
                int boundaryVotes = p.Candidates[seats - 1].Votes;
                bool tie = p.Candidates.Count > seats
           && boundaryVotes > 0
           && p.Candidates[seats].Votes == boundaryVotes;
                p.IsTie = tie;

                int firstLoser = p.Candidates.Count > seats ? p.Candidates[seats].Votes : 0;
                p.Lead = Math.Max(0, boundaryVotes - firstLoser);

                for (int i = 0; i < p.Candidates.Count; i++)
                {
                    var c = p.Candidates[i];

                    if (tie && c.Votes == boundaryVotes)
                    {
                        c.IsTie = true;
                        c.IsWinner = false;
                    }
                    else
                    {
                        c.IsWinner = i < seats && c.Votes > 0;
                    }

                    c.Lead = (i < seats && !c.IsTie) ? Math.Max(0, c.Votes - firstLoser) : 0;

                    // remarks : winner ar tie candidate er naam er pashe
                    c.Remarks = "";
                    if (ended)
                    {
                        if (c.IsTie)
                        {
                            c.Remarks = "টাই";
                        }
                        else if (c.IsWinner)
                        {
                            c.Remarks = p.Seats > 1 ? "নির্বাচিত" : "বিজয়ী";
                            if (c.Bonus > 0) c.Remarks += " (টাই সমাধানে " + Bn.Digits(c.Bonus) + " ভোট যুক্ত)";
                        }
                    }
                }
            }

            m.CountedPosts = m.Posts.Count(p => p.ValidVotes > 0);
            m.IssueCount = m.Posts.Count(p => p.IsTie);
            m.Version = m.TotalBallots + ":" + sumVotes + ":" + (int)m.Phase + ":" + m.IssueCount;
        }

        // ------------------------------------------------------------------ winner / tie -> database
        private static readonly object SnapLock = new object();
        private static readonly Dictionary<int, string> SnapKeys = new Dictionary<int, string>();

        private static string SnapshotKey(ResultsViewModel m)
        {
            return string.Join("|", m.Posts.SelectMany(p => p.Candidates
                .Where(c => c.IsWinner || c.IsTie)
                .Select(c => p.PostId + ":" + c.CandidateId + ":" + c.Rank + ":" + c.Votes + ":" +
                             (c.IsTie ? "T" : "W") + ":" + (c.Bonus > 0 ? "1" : "0") + ":" + c.Remarks)));
        }

        private void SaveSnapshot(ResultsViewModel m)
        {
            string key = SnapshotKey(m);

            lock (SnapLock)
            {
                string old;
                if (SnapKeys.TryGetValue(m.ElectionId, out old) && old == key) return;   // kichu bodlayni

                try
                {
                    using (var con = new SqlConnection(connectionString))
                    {
                        con.Open();
                        using (var tx = con.BeginTransaction())
                        {
                            using (var del = new SqlCommand(
                                "DELETE FROM dbo.ElectionResults WHERE ElectionId = @e", con, tx))
                            {
                                del.Parameters.Add("@e", SqlDbType.Int).Value = m.ElectionId;
                                del.ExecuteNonQuery();
                            }

                            foreach (var p in m.Posts)
                            {
                                foreach (var c in p.Candidates)
                                {
                                    if (!c.IsWinner && !c.IsTie) continue;

                                    using (var ins = new SqlCommand(
                                        "INSERT INTO dbo.ElectionResults " +
                                        "(ElectionId, PostId, CandidateId, Position, Votes, ResultStatus, IsTieResolved, Remarks) " +
                                        "VALUES (@e, @p, @c, @pos, @v, @st, @tr, @rm)", con, tx))
                                    {
                                        ins.Parameters.Add("@e", SqlDbType.Int).Value = m.ElectionId;
                                        ins.Parameters.Add("@p", SqlDbType.Int).Value = p.PostId;
                                        ins.Parameters.Add("@c", SqlDbType.Int).Value = c.CandidateId;
                                        ins.Parameters.Add("@pos", SqlDbType.Int).Value = c.Rank;
                                        ins.Parameters.Add("@v", SqlDbType.Int).Value = c.Votes;
                                        ins.Parameters.Add("@st", SqlDbType.NVarChar, 20).Value = c.IsTie ? "Tie" : "Winner";
                                        ins.Parameters.Add("@tr", SqlDbType.Bit).Value = c.Bonus > 0;
                                        ins.Parameters.Add("@rm", SqlDbType.NVarChar, 300).Value =
                                            string.IsNullOrEmpty(c.Remarks) ? (object)DBNull.Value : c.Remarks;
                                        ins.ExecuteNonQuery();
                                    }
                                }
                            }

                            tx.Commit();
                        }
                    }

                    SnapKeys[m.ElectionId] = key;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("ElectionResults save failed: " + ex);
                }
            }
        }

        // ------------------------------------------------------------------ tie resolve
        /// <summary>Tie hoya pode admin ekjon candidate ke winner banay: tar nam e +1 vote jog hoy.</summary>
        public TieResolveResult ResolveTie(int electionId, int postId, int candidateId, string remarks, string by)
        {
            var m = Load(electionId);      // cache chhara, notun data

            if (!m.HasElection || m.ElectionId != electionId)
                return Fail("নির্বাচনটি খুঁজে পাওয়া যায়নি।");

            if (m.Phase != VotePhase.Ended)
                return Fail("ভোটগ্রহণ শেষ না হওয়া পর্যন্ত টাই সমাধান করা যাবে না।");

            if (m.GazetteApproved)
                return Fail("চূড়ান্ত গেজেট অনুমোদন হয়ে গেছে, এখন আর টাই সমাধান করা যাবে না।");

            var post = m.Posts.FirstOrDefault(p => p.PostId == postId);
            if (post == null || !post.IsTie)
                return Fail("এই পদে এখন কোনো টাই নেই (আগেই সমাধান হয়ে থাকতে পারে)। পাতাটি রিফ্রেশ করুন।");

            var cand = post.Candidates.FirstOrDefault(c => c.CandidateId == candidateId && c.IsTie);
            if (cand == null)
                return Fail("নির্বাচিত প্রার্থী এই টাইয়ের তালিকায় নেই।");

            remarks = (remarks ?? "").Trim();
            if (remarks.Length > 300) remarks = remarks.Substring(0, 300);

            using (var con = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(
                "INSERT INTO dbo.ElectionTieResolutions (ElectionId, PostId, CandidateId, BonusVotes, Remarks, ResolvedBy) " +
                "VALUES (@e, @p, @c, 1, @rm, @by)", con))
            {
                cmd.Parameters.Add("@e", SqlDbType.Int).Value = electionId;
                cmd.Parameters.Add("@p", SqlDbType.Int).Value = postId;
                cmd.Parameters.Add("@c", SqlDbType.Int).Value = candidateId;
                cmd.Parameters.Add("@rm", SqlDbType.NVarChar, 300).Value =
                    remarks.Length == 0 ? (object)DBNull.Value : remarks;
                cmd.Parameters.Add("@by", SqlDbType.NVarChar, 100).Value =
                    string.IsNullOrWhiteSpace(by) ? "Super Admin" : by;
                con.Open();
                cmd.ExecuteNonQuery();
            }

            ClearCache(electionId);
            Load(electionId);              // notun winner / tie database e save hoye jabe
            ClearCache(electionId);

            return new TieResolveResult
            {
                Success = true,
                Message = cand.Name + " কে " + post.DisplayName + " পদে বিজয়ী ঘোষণা করা হয়েছে।"
            };
        }

        // ------------------------------------------------------------------ gazette approve
        public TieResolveResult ApproveGazette(int electionId, string by)
        {
            var m = Load(electionId);

            if (!m.HasElection || m.ElectionId != electionId)
                return Fail("নির্বাচনটি খুঁজে পাওয়া যায়নি।");

            if (m.GazetteApproved)
                return new TieResolveResult { Success = true, Message = "ফলাফল আগেই অনুমোদিত হয়েছে।" };

            if (m.Phase != VotePhase.Ended)
                return Fail("ভোটগ্রহণ শেষ হওয়ার পর চূড়ান্ত গেজেট অনুমোদন করা যাবে।");

            if (m.IssueCount > 0)
                return Fail("কিছু পদে টাই সমাধান বাকি আছে। আগে টাই সমাধান করুন।");

            using (var con = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(
                "IF NOT EXISTS (SELECT 1 FROM dbo.ElectionGazette WHERE ElectionId = @e) " +
                "INSERT INTO dbo.ElectionGazette (ElectionId, ApprovedBy) VALUES (@e, @by)", con))
            {
                cmd.Parameters.Add("@e", SqlDbType.Int).Value = electionId;
                cmd.Parameters.Add("@by", SqlDbType.NVarChar, 100).Value =
                    string.IsNullOrWhiteSpace(by) ? "Super Admin" : by;
                con.Open();
                cmd.ExecuteNonQuery();
            }

            ClearCache(electionId);
            return new TieResolveResult { Success = true, Message = "চূড়ান্ত গেজেট অনুমোদিত হয়েছে।" };
        }

        // ------------------------------------------------------------------ resolve kora tie er talika
        public List<TieHistoryRow> GetTieHistory(int electionId)
        {
            var list = new List<TieHistoryRow>();

            const string sql =
                "SELECT p.DisplayName, n.CandidateName, c.BallotNo, t.BonusVotes, t.Remarks, t.ResolvedBy, t.ResolvedAt " +
                "FROM dbo.ElectionTieResolutions t " +
                "JOIN dbo.ElectionPosts p ON p.PostId = t.PostId " +
                "JOIN dbo.ElectionCandidates c ON c.CandidateId = t.CandidateId " +
                "JOIN dbo.Nominations n ON n.NominationId = c.NominationId " +
                "WHERE t.ElectionId = @e " +
                "ORDER BY t.ResolutionId DESC";

            using (var con = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.Add("@e", SqlDbType.Int).Value = electionId;
                con.Open();

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new TieHistoryRow
                        {
                            PostName = Convert.ToString(r["DisplayName"]),
                            CandidateName = Convert.ToString(r["CandidateName"]),
                            BallotNo = Convert.ToInt32(r["BallotNo"]),
                            BonusVotes = Convert.ToInt32(r["BonusVotes"]),
                            Remarks = r["Remarks"] == DBNull.Value ? "" : Convert.ToString(r["Remarks"]),
                            ResolvedBy = Convert.ToString(r["ResolvedBy"]),
                            ResolvedAtText = Bn.DateTimeText(Convert.ToDateTime(r["ResolvedAt"]))
                        });
                    }
                }
            }

            return list;
        }

        private static TieResolveResult Fail(string message)
        {
            return new TieResolveResult { Success = false, Message = message };
        }
    }
}
