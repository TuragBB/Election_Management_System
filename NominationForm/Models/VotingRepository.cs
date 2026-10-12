using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace NominationSystem.Models
{
    /// <summary>Voting data access (plain ADO.NET, same style as AdminNominationRepository).</summary>
    public class VotingRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["NominationDB"].ConnectionString;

        // ------------------------------------------------------------------ ballot
        /// <summary>Loads the currently OPEN election with its positions and approved candidates.</summary>
        public BallotViewModel GetBallot()
        {
            var model = new BallotViewModel();

            using (var con = new SqlConnection(connectionString))
            {
                con.Open();

                // 1) open election
                // 1) open election (+ DB clock: time SQL Server er ghori diye dhora hoy, browser diye na)
                const string electionSql =
                    "SELECT TOP 1 ElectionId, ElectionYear, Title, ShortTitle, StartAt, EndAt, SYSDATETIME() AS DbNow " +
                    "FROM dbo.Elections " +
                    "WHERE Status = N'Open' " +
                    "ORDER BY ElectionYear DESC, ElectionId DESC";

                DateTime dbNow;

                using (var cmd = new SqlCommand(electionSql, con))
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return model;     // HasElection = false

                    model.HasElection = true;
                    model.ElectionId = Convert.ToInt32(r["ElectionId"]);
                    model.ElectionYear = Convert.ToInt32(r["ElectionYear"]);
                    model.Title = Convert.ToString(r["Title"]);
                    model.ShortTitle = Convert.ToString(r["ShortTitle"]);
                    model.StartAt = r["StartAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["StartAt"]);
                    model.EndAt = r["EndAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["EndAt"]);
                    dbNow = Convert.ToDateTime(r["DbNow"]);
                }

                // time shuru hoy nai
                if (model.StartAt.HasValue && dbNow < model.StartAt.Value)
                {
                    model.Phase = VotePhase.NotStarted;
                    model.SecondsToStart = (int)Math.Max(1, Math.Ceiling((model.StartAt.Value - dbNow).TotalSeconds));
                    return model;
                }

                // time shesh
                if (model.EndAt.HasValue && dbNow > model.EndAt.Value)
                {
                    model.Phase = VotePhase.Ended;
                    return model;
                }

                // cholche
                model.Phase = VotePhase.Running;
                if (model.EndAt.HasValue)
                {
                    model.SecondsToEnd = (int)Math.Max(1, Math.Ceiling((model.EndAt.Value - dbNow).TotalSeconds));
                }

                // 1b) Approved / Rejected অনুযায়ী প্রার্থী তালিকা আপনা-আপনি আপডেট
                using (var sync = new SqlCommand("dbo.Election_SyncApproved", con))
                {
                    sync.CommandType = CommandType.StoredProcedure;
                    sync.Parameters.Add("@ElectionId", SqlDbType.Int).Value = model.ElectionId;
                    sync.ExecuteNonQuery();
                }

                // 2) positions
                var posts = new Dictionary<int, BallotPost>();

                const string postSql =
                    "SELECT PostId, DisplayName, DisplayNameEn, SortOrder, MaxVotes " +
                    "FROM dbo.ElectionPosts WHERE ElectionId = @e ORDER BY SortOrder, PostId";

                using (var cmd = new SqlCommand(postSql, con))
                {
                    cmd.Parameters.Add("@e", SqlDbType.Int).Value = model.ElectionId;
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var p = new BallotPost
                            {
                                PostId = Convert.ToInt32(r["PostId"]),
                                DisplayName = Convert.ToString(r["DisplayName"]),
                                DisplayNameEn = Convert.ToString(r["DisplayNameEn"]),
                                SortOrder = Convert.ToInt32(r["SortOrder"]),
                                MaxVotes = Convert.ToInt32(r["MaxVotes"])
                            };
                            posts[p.PostId] = p;
                            model.Posts.Add(p);
                        }
                    }
                }

                // 3) candidates (photo bytes are NOT loaded here, only a flag)
                const string candSql =
                    "SELECT c.CandidateId, c.PostId, c.BallotNo, n.CandidateName, n.Designation, " +
                    "       CASE WHEN n.Photo IS NULL THEN 0 ELSE 1 END AS HasPhoto " +
                    "FROM dbo.ElectionCandidates c " +
                    "JOIN dbo.Nominations n ON n.NominationId = c.NominationId " +
                    "WHERE c.ElectionId = @e AND c.IsActive = 1 " +
                    "ORDER BY c.PostId, c.BallotNo";

                using (var cmd = new SqlCommand(candSql, con))
                {
                    cmd.Parameters.Add("@e", SqlDbType.Int).Value = model.ElectionId;
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            BallotPost p;
                            if (!posts.TryGetValue(Convert.ToInt32(r["PostId"]), out p)) continue;

                            p.Candidates.Add(new BallotCandidate
                            {
                                CandidateId = Convert.ToInt32(r["CandidateId"]),
                                BallotNo = Convert.ToInt32(r["BallotNo"]),
                                Name = Convert.ToString(r["CandidateName"]),
                                Designation = Convert.ToString(r["Designation"]),
                                HasPhoto = Convert.ToInt32(r["HasPhoto"]) == 1
                            });
                        }
                    }
                }
            }

            // positions without candidates are skipped; if candidates are fewer than the required votes, lower the requirement
            model.Posts = model.Posts.Where(p => p.Candidates.Count > 0).ToList();
            foreach (var p in model.Posts)
            {
                if (p.MaxVotes > p.Candidates.Count) p.MaxVotes = p.Candidates.Count;
            }

            return model;
        }

        // ------------------------------------------------------------------ has voted ?
        public bool HasVoted(int electionId, int voterId)
        {
            using (var con = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.VoterParticipation WHERE ElectionId = @e AND VoterId = @v", con))
            {
                cmd.Parameters.Add("@e", SqlDbType.Int).Value = electionId;
                cmd.Parameters.Add("@v", SqlDbType.Int).Value = voterId;
                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        // ------------------------------------------------------------------ candidate photo
        public bool GetPhoto(int candidateId, out byte[] data, out string contentType)
        {
            data = null;
            contentType = null;

            const string sql =
                "SELECT n.Photo, n.PhotoContentType " +
                "FROM dbo.ElectionCandidates c " +
                "JOIN dbo.Nominations n ON n.NominationId = c.NominationId " +
                "WHERE c.CandidateId = @id AND c.IsActive = 1";

            using (var con = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = candidateId;
                con.Open();

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read() || r.IsDBNull(0)) return false;

                    data = (byte[])r[0];
                    contentType = r.IsDBNull(1) ? "image/jpeg" : Convert.ToString(r[1]);
                    return data != null && data.Length > 0;
                }
            }
        }

        // ------------------------------------------------------------------ voting window check
        /// <summary>null = vote deya jabe; na hole Bangla message.</summary>
        public string GetVotingClosedMessage(int electionId)
        {
            using (var con = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(
                "SELECT StartAt, EndAt, SYSDATETIME() FROM dbo.Elections WHERE ElectionId = @e", con))
            {
                cmd.Parameters.Add("@e", SqlDbType.Int).Value = electionId;
                con.Open();

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return "নির্বাচনটি খুঁজে পাওয়া যায়নি।";

                    DateTime now = Convert.ToDateTime(r[2]);

                    if (!r.IsDBNull(0) && now < Convert.ToDateTime(r[0]))
                        return "ভোটগ্রহণ এখনো শুরু হয়নি।";

                    if (!r.IsDBNull(1) && now > Convert.ToDateTime(r[1]))
                        return "ভোটগ্রহণের সময় শেষ হয়ে গেছে। এখন আর ভোট দেওয়া যাবে না।";
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ submit ballot
        public VoteResult Submit(int electionId, int? voterId, IEnumerable<int> candidateIds)
        {
            string closed = GetVotingClosedMessage(electionId);
            if (closed != null)
            {
                return new VoteResult { Success = false, Message = closed };
            }

            var table = new DataTable();
            table.Columns.Add("CandidateId", typeof(int));
            foreach (int id in candidateIds.Distinct())
            {
                table.Rows.Add(id);
            }

            try
            {
                using (var con = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand("dbo.Vote_Submit", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@ElectionId", SqlDbType.Int).Value = electionId;
                    cmd.Parameters.Add("@VoterId", SqlDbType.Int).Value =
                        voterId.HasValue ? (object)voterId.Value : DBNull.Value;

                    var tvp = cmd.Parameters.Add("@Candidates", SqlDbType.Structured);
                    tvp.TypeName = "dbo.CandidateIdList";
                    tvp.Value = table;

                    con.Open();

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            return new VoteResult
                            {
                                Success = true,
                                ReceiptNo = Convert.ToString(r["ReceiptNo"]),
                                SubmittedAt = Convert.ToDateTime(r["SubmittedAt"])
                            };
                        }
                    }
                }

                return new VoteResult { Success = false, Message = "ভোট সংরক্ষণ করা যায়নি। আবার চেষ্টা করুন।" };
            }
            catch (SqlException ex)
            {
                // 50001 - 50006 are our own business-rule messages (written in Bangla inside the procedure)
                if (ex.Number >= 50001 && ex.Number <= 50006)
                {
                    return new VoteResult { Success = false, Message = ex.Message };
                }

                throw;
            }
        }
    }
}
