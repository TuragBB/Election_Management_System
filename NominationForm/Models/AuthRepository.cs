using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;

namespace NominationSystem.Models
{
    /// <summary>Business-rule error (Bangla message user ke dekhano jay).</summary>
    public class AuthException : Exception
    {
        public AuthException(string message) : base(message) { }
    }

    /// <summary>Database theke ana user row.</summary>
    public class UserRecord
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
        public string AccountType { get; set; }
        public int? MemberId { get; set; }
        public string MemberNo { get; set; }
        public bool IsActive { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public int PwdIterations { get; set; }
        public int LockMinutes { get; set; }
        public string VoterCode { get; set; }
        public int BoothNo { get; set; }
    }

    /// <summary>PBKDF2-SHA256 password hash. Password kokhono plain text e database e jay na.</summary>
    public static class PasswordHasher
    {
        // beshi korle secure, kom korle fast. 100000 = proti login e ~50-80ms.
        public const int Iterations = 100000;

        public static void Create(string password, out byte[] hash, out byte[] salt)
        {
            salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            hash = Derive(password, salt, Iterations);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return kdf.GetBytes(32);
            }
        }

        public static bool Verify(string password, byte[] hash, byte[] salt, int iterations)
        {
            if (string.IsNullOrEmpty(password) || hash == null || salt == null || iterations <= 0) return false;

            byte[] test = Derive(password, salt, iterations);

            // constant-time compare
            int diff = hash.Length ^ test.Length;
            for (int i = 0; i < hash.Length && i < test.Length; i++)
            {
                diff |= hash[i] ^ test[i];
            }
            return diff == 0;
        }
    }

    public class AuthRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["NominationDB"].ConnectionString;

        // ------------------------------------------------------------------ read helpers
        private static UserRecord Map(IDataRecord r)
        {
            return new UserRecord
            {
                UserId = Convert.ToInt32(r["UserId"]),
                FullName = Convert.ToString(r["FullName"]),
                Email = r["Email"] as string,
                Mobile = r["Mobile"] as string,
                AccountType = Convert.ToString(r["AccountType"]),
                MemberId = r["MemberId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["MemberId"]),
                MemberNo = r["MemberNo"] as string,
                IsActive = Convert.ToBoolean(r["IsActive"]),
                PasswordHash = r["PasswordHash"] as byte[],
                PasswordSalt = r["PasswordSalt"] as byte[],
                PwdIterations = r["PwdIterations"] == DBNull.Value ? 0 : Convert.ToInt32(r["PwdIterations"]),
                LockMinutes = Convert.ToInt32(r["LockMinutes"]),
                VoterCode = Convert.ToString(r["VoterCode"]),
                BoothNo = Convert.ToInt32(r["BoothNo"])
            };
        }

        private UserRecord ReadOne(SqlCommand cmd)
        {
            using (var con = new SqlConnection(connectionString))
            {
                cmd.Connection = con;
                con.Open();
                using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    return r.Read() ? Map(r) : null;
                }
            }
        }

        private static SqlCommand Proc(string name)
        {
            return new SqlCommand(name) { CommandType = CommandType.StoredProcedure };
        }

        private static object Db(object value)
        {
            return value ?? DBNull.Value;
        }

        // ------------------------------------------------------------------ queries
        public UserRecord GetByLogin(string login)
        {
            using (var cmd = Proc("dbo.Auth_GetByLogin"))
            {
                cmd.Parameters.Add("@Login", SqlDbType.NVarChar, 256).Value = login;
                return ReadOne(cmd);
            }
        }

        public UserRecord GetById(int userId)
        {
            using (var cmd = Proc("dbo.Auth_GetById"))
            {
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                return ReadOne(cmd);
            }
        }

        public UserRecord GoogleLogin(string googleSub, string email)
        {
            using (var cmd = Proc("dbo.Auth_GoogleLogin"))
            {
                cmd.Parameters.Add("@GoogleSub", SqlDbType.NVarChar, 64).Value = googleSub;
                cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = Db(email);
                return ReadOne(cmd);
            }
        }

        public void LoginResult(int userId, bool success)
        {
            try
            {
                using (var con = new SqlConnection(connectionString))
                using (var cmd = Proc("dbo.Auth_LoginResult"))
                {
                    cmd.Connection = con;
                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    cmd.Parameters.Add("@Success", SqlDbType.Bit).Value = success;
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // login result save na hole-o login atkabe na
                System.Diagnostics.Trace.TraceError(ex.ToString());
            }
        }

        // ------------------------------------------------------------------ register
        /// <param name="password">null hole (Google account) password set hobe na</param>
        public UserRecord Register(string fullName, string email, string mobile, string password,
                                   string googleSub, string accountType, string memberNo)
        {
            byte[] hash = null, salt = null;
            if (!string.IsNullOrEmpty(password))
            {
                PasswordHasher.Create(password, out hash, out salt);
            }

            try
            {
                using (var cmd = Proc("dbo.Auth_Register"))
                {
                    cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = fullName;
                    cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = Db(email);
                    cmd.Parameters.Add("@Mobile", SqlDbType.NVarChar, 20).Value = mobile;
                    cmd.Parameters.Add("@PasswordHash", SqlDbType.VarBinary, 32).Value = Db(hash);
                    cmd.Parameters.Add("@PasswordSalt", SqlDbType.VarBinary, 16).Value = Db(salt);
                    cmd.Parameters.Add("@PwdIterations", SqlDbType.Int).Value =
                        hash == null ? (object)DBNull.Value : PasswordHasher.Iterations;
                    cmd.Parameters.Add("@GoogleSub", SqlDbType.NVarChar, 64).Value = Db(googleSub);
                    cmd.Parameters.Add("@AccountType", SqlDbType.NVarChar, 10).Value = accountType;
                    cmd.Parameters.Add("@MemberNo", SqlDbType.NVarChar, 50).Value = Db(memberNo);
                    return ReadOne(cmd);
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number >= 51001 && ex.Number <= 51009) throw new AuthException(ex.Message);
                throw;
            }
        }

        public UserRecord UpgradeToMember(int userId, string memberNo)
        {
            try
            {
                using (var cmd = Proc("dbo.Auth_UpgradeToMember"))
                {
                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    cmd.Parameters.Add("@MemberNo", SqlDbType.NVarChar, 50).Value = memberNo;
                    return ReadOne(cmd);
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number >= 51001 && ex.Number <= 51009) throw new AuthException(ex.Message);
                throw;
            }
        }

        // ------------------------------------------------------------------ voting page er voter card
        /// <summary>Login kora user er tothyo diye voting page er "ভোটার: ..." card toiri.</summary>
        public VoterInfo GetVoterInfo(int userId)
        {
            UserRecord u = GetById(userId);
            if (u == null || !u.IsActive) return null;

            bool member = u.AccountType == AccountTypes.Member && !string.IsNullOrEmpty(u.MemberNo);

            return new VoterInfo
            {
                VoterId = u.UserId,
                Name = u.FullName,
                CodeLabel = member ? "সদস্য কোড" : "ভোটার কোড",
                MemberCode = member ? u.MemberNo : u.VoterCode,
                Serial = Bn.Digits(u.UserId.ToString("0000")),
                Booth = "ডিজিটাল সেল-" + Bn.Digits(u.BoothNo)
            };
        }
    }
}
