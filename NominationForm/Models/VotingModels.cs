using System;
using System.Collections.Generic;
using System.Web;

namespace NominationSystem.Models
{
    /// <summary>Logged-in voter. VoterId stays null until the voter-account module is built (demo mode).</summary>
    public class VoterInfo
    {
        public int? VoterId { get; set; }
        public string Name { get; set; }
        public string MemberCode { get; set; }
        public string Serial { get; set; }
        public string Booth { get; set; }
        public string CodeLabel { get; set; }
    }

    public class BallotCandidate
    {
        public int CandidateId { get; set; }
        public int BallotNo { get; set; }
        public string Name { get; set; }
        public string Designation { get; set; }
        public bool HasPhoto { get; set; }
    }

    public class BallotPost
    {
        public BallotPost()
        {
            Candidates = new List<BallotCandidate>();
        }

        public int PostId { get; set; }
        public string DisplayName { get; set; }
        public string DisplayNameEn { get; set; }
        public int SortOrder { get; set; }
        public int MaxVotes { get; set; }
        public List<BallotCandidate> Candidates { get; set; }
    }
    public enum VotePhase
    {
        NoElection = 0,
        NotStarted = 1,
        Running = 2,
        Ended = 3
    }

    public class BallotViewModel
    {
        public BallotViewModel()
        {
            Posts = new List<BallotPost>();
        }

        public bool HasElection { get; set; }
        public bool AlreadyVoted { get; set; }

        public VotePhase Phase { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public int SecondsToStart { get; set; }
        public int SecondsToEnd { get; set; }

        public int ElectionId { get; set; }
        public int ElectionYear { get; set; }
        public string Title { get; set; }
        public string ShortTitle { get; set; }

        public VoterInfo Voter { get; set; }
        public List<BallotPost> Posts { get; set; }

        public int TotalVotes
        {
            get
            {
                int t = 0;
                foreach (var p in Posts) t += p.MaxVotes;
                return t;
            }
        }
    }

    public class VoteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ReceiptNo { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    /// <summary>Small inline SVG icon set (24x24, stroke icons) used by the voting pages.</summary>
    public static class VoteIcons
    {
        public static IHtmlString Get(string name, int size = 16)
        {
            string body;

            switch (name)
            {
                case "shield":
                    body = "<path d=\"M12 3l8 3v6c0 4.5-3.2 7.8-8 9-4.8-1.2-8-4.5-8-9V6z\"/><path d=\"M9 12l2 2 4-4\"/>";
                    break;
                case "briefcase":
                    body = "<rect x=\"3\" y=\"7\" width=\"18\" height=\"13\" rx=\"2\"/><path d=\"M8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2\"/><path d=\"M3 13h18\"/>";
                    break;
                case "calendar":
                    body = "<rect x=\"3\" y=\"5\" width=\"18\" height=\"16\" rx=\"2\"/><path d=\"M16 3v4M8 3v4M3 10h18\"/>";
                    break;
                case "clock":
                    body = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 7v5l3 2\"/>";
                    break;
                case "copy":
                    body = "<rect x=\"9\" y=\"9\" width=\"11\" height=\"11\" rx=\"2\"/><path d=\"M5 15V6a2 2 0 0 1 2-2h9\"/>";
                    break;
                case "download":
                    body = "<path d=\"M12 3v12M7 11l5 5 5-5\"/><path d=\"M4 20h16\"/>";
                    break;
                case "home":
                    body = "<path d=\"M3 11l9-8 9 8\"/><path d=\"M5 10v10h14V10\"/>";
                    break;
                case "lock":
                    body = "<rect x=\"4\" y=\"11\" width=\"16\" height=\"10\" rx=\"2\"/><path d=\"M8 11V7a4 4 0 0 1 8 0v4\"/>";
                    break;
                case "reset":
                    body = "<path d=\"M3 12a9 9 0 1 0 3-6.7L3 8\"/><path d=\"M3 3v5h5\"/>";
                    break;
                case "ballot":
                    body = "<rect x=\"3\" y=\"13\" width=\"18\" height=\"8\" rx=\"2\"/><path d=\"M8 13V8l4-4 4 4v5\"/><path d=\"M10 17h4\"/>";
                    break;
                case "chart":
                    body = "<path d=\"M4 20V10M10 20V4M16 20v-8\"/><path d=\"M2 20h20\"/>";
                    break;
                case "check":
                    body = "<path d=\"M5 12.5l4.5 4.5L19 7.5\"/>";
                    break;
                case "alert":
                    body = "<path d=\"M12 3l10 18H2z\"/><path d=\"M12 10v5M12 18h.01\"/>";
                    break;
                case "info":
                    body = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 11v5M12 8h.01\"/>";
                    break;
                case "user":
                    body = "<path d=\"M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2\"/><circle cx=\"12\" cy=\"7\" r=\"4\"/>";
                    break;
                default:
                    body = "";
                    break;
            }

            return new HtmlString(
                "<svg class=\"ico\" viewBox=\"0 0 24 24\" width=\"" + size + "\" height=\"" + size +
                "\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">" +
                body + "</svg>");
        }
    }
}
