using System;
using System.Collections.Generic;

namespace NominationSystem.Models
{
    /// <summary>Ekjon prarthir live / final result.</summary>
    public class ResultCandidate
    {
        public int CandidateId { get; set; }
        public int BallotNo { get; set; }
        public string Name { get; set; }
        public string MemberNo { get; set; }
        public string Designation { get; set; }
        public bool HasPhoto { get; set; }

        public int Votes { get; set; }          // asol vote + tie-break vote
        public int RealVotes { get; set; }      // shudhu ballot theke pawa vote
        public int Bonus { get; set; }          // tie resolve e admin er deya vote
        public double Percent { get; set; }     // Votes / ValidVotes * 100
        public int Rank { get; set; }           // 1, 2, 3 ... (same votes = same rank)
        public bool IsWinner { get; set; }      // seat er moddhe + tie na
        public bool IsTie { get; set; }         // seat-er sima-y tie (shudhu vote shesh hole)
        public int Lead { get; set; }           // seat-er winner - prothom loser
        public string Remarks { get; set; }     // "বিজয়ী" / "নির্বাচিত" / "টাই" (vote shesh hole)
    }

    /// <summary>Ekta pod (President, Vice President ...) er result.</summary>
    public class ResultPost
    {
        public ResultPost()
        {
            Candidates = new List<ResultCandidate>();
        }

        public int PostId { get; set; }
        public int SortOrder { get; set; }
        public string DisplayName { get; set; }
        public string DisplayNameEn { get; set; }
        public int Seats { get; set; }           // MaxVotes = koyta seat
        public int ValidVotes { get; set; }      // ei pode vote deya ballot sonkhya (+ tie-break vote)
        public bool IsTie { get; set; }
        public bool HasTieBreak { get; set; }    // ei pode tie resolve kora hoyeche
        public int Lead { get; set; }            // shesh winner - prothom loser (post level)
        public List<ResultCandidate> Candidates { get; set; }   // votes DESC
    }

    public class ResultsViewModel
    {
        public ResultsViewModel()
        {
            Posts = new List<ResultPost>();
        }

        public bool HasElection { get; set; }
        public int ElectionId { get; set; }
        public int ElectionYear { get; set; }
        public string Title { get; set; }
        public string ShortTitle { get; set; }
        public string Status { get; set; }

        /// <summary>NotStarted / Running / Ended (SQL Server er ghori diye)</summary>
        public VotePhase Phase { get; set; }
        public string StartText { get; set; }
        public string EndText { get; set; }
        public string UpdatedText { get; set; }   // "১০:৫১ পূর্বাহ্ন"

        public int TotalBallots { get; set; }     // mot kast vote (ballot)
        public int EligibleVoters { get; set; }   // web.config theke, 0 hole jana nai
        public double Turnout { get; set; }       // %
        public int TotalPosts { get; set; }
        public int CountedPosts { get; set; }
        public int IssueCount { get; set; }       // ekhono resolve na hoya tie pod (shudhu vote shesh hole)
        public string Version { get; set; }       // polling-e change dhorar jonno

        public bool GazetteApproved { get; set; }
        public string GazetteApprovedBy { get; set; }
        public string GazetteApprovedText { get; set; }

        public List<ResultPost> Posts { get; set; }
    }

    /// <summary>Tie resolve / gazette approve er por controller ke dewa uttor.</summary>
    public class TieResolveResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>Ager resolve kora tie er talika (history).</summary>
    public class TieHistoryRow
    {
        public string PostName { get; set; }
        public string CandidateName { get; set; }
        public int BallotNo { get; set; }
        public int BonusVotes { get; set; }
        public string Remarks { get; set; }
        public string ResolvedBy { get; set; }
        public string ResolvedAtText { get; set; }
    }
}
