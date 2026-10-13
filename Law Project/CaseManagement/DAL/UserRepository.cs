using Microsoft.Data.SqlClient;
using System.Data;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public class UserRepository : IUserRepository
    {
        private readonly DBHelper _dbHelper;

        public UserRepository(DBHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        public IEnumerable<User> GetAllUsers()
        {
            var users = new List<User>();
            var query = @"
                SELECT u.UserID, u.Username, u.FullName, u.Email, u.Mobile,
                       u.RoleID, r.RoleName, u.DivisionID, d.DivisionNameEnglish as DivisionName,
                       u.IsActive, u.LastLogin, u.CreatedDate
                FROM USERS u
                INNER JOIN ROLE_MASTER r ON u.RoleID = r.RoleID
                LEFT JOIN DIVISION_MASTER d ON u.DivisionID = d.DivisionID
                ORDER BY u.FullName";

            var dt = _dbHelper.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                users.Add(new User
                {
                    UserID = Convert.ToInt32(row["UserID"]),
                    Username = row["Username"].ToString() ?? string.Empty,
                    // PasswordHash intentionally skipped for list
                    FullName = row["FullName"].ToString() ?? string.Empty,
                    Email = row["Email"] != DBNull.Value ? row["Email"].ToString() : null,
                    Mobile = row["Mobile"] != DBNull.Value ? row["Mobile"].ToString() : null,
                    RoleID = Convert.ToInt32(row["RoleID"]),
                    RoleName = row["RoleName"].ToString() ?? string.Empty,
                    DivisionID = row["DivisionID"] != DBNull.Value ? Convert.ToInt32(row["DivisionID"]) : null,
                    DivisionName = row["DivisionName"] != DBNull.Value ? row["DivisionName"].ToString() : null,
                    IsActive = Convert.ToBoolean(row["IsActive"]),
                    LastLogin = row["LastLogin"] != DBNull.Value ? Convert.ToDateTime(row["LastLogin"]) : null,
                    CreatedDate = Convert.ToDateTime(row["CreatedDate"])
                });
            }
            return users;
        }

        public User? GetUserById(int id)
        {
            var query = @"
                SELECT u.UserID, u.Username, u.PasswordHash, u.FullName, u.Email, u.Mobile,
                       u.RoleID, r.RoleName, u.DivisionID, d.DivisionNameEnglish as DivisionName,
                       u.IsActive, u.LastLogin, u.CreatedDate
                FROM USERS u
                INNER JOIN ROLE_MASTER r ON u.RoleID = r.RoleID
                LEFT JOIN DIVISION_MASTER d ON u.DivisionID = d.DivisionID
                WHERE u.UserID = @UserID";

            var dt = _dbHelper.ExecuteQuery(query, new[] { new SqlParameter("@UserID", id) });
            if (dt.Rows.Count == 0) return null;

            var row = dt.Rows[0];
            return new User
            {
                UserID = Convert.ToInt32(row["UserID"]),
                Username = row["Username"].ToString() ?? string.Empty,
                PasswordHash = row["PasswordHash"].ToString() ?? string.Empty,
                FullName = row["FullName"].ToString() ?? string.Empty,
                Email = row["Email"] != DBNull.Value ? row["Email"].ToString() : null,
                Mobile = row["Mobile"] != DBNull.Value ? row["Mobile"].ToString() : null,
                RoleID = Convert.ToInt32(row["RoleID"]),
                RoleName = row["RoleName"].ToString() ?? string.Empty,
                DivisionID = row["DivisionID"] != DBNull.Value ? Convert.ToInt32(row["DivisionID"]) : null,
                DivisionName = row["DivisionName"] != DBNull.Value ? row["DivisionName"].ToString() : null,
                IsActive = Convert.ToBoolean(row["IsActive"]),
                LastLogin = row["LastLogin"] != DBNull.Value ? Convert.ToDateTime(row["LastLogin"]) : null,
                CreatedDate = Convert.ToDateTime(row["CreatedDate"])
            };
        }

        public void AddUser(User user)
        {
            var query = @"
                INSERT INTO USERS (Username, PasswordHash, FullName, Email, Mobile, RoleID, DivisionID, IsActive, CreatedDate)
                VALUES (@Username, @PasswordHash, @FullName, @Email, @Mobile, @RoleID, @DivisionID, 1, GETDATE())";

            var parameters = new[]
            {
                new SqlParameter("@Username", user.Username),
                new SqlParameter("@PasswordHash", user.PasswordHash), // Must be pre-hashed
                new SqlParameter("@FullName", user.FullName),
                new SqlParameter("@Email", user.Email ?? (object)DBNull.Value),
                new SqlParameter("@Mobile", user.Mobile ?? (object)DBNull.Value),
                new SqlParameter("@RoleID", user.RoleID),
                new SqlParameter("@DivisionID", user.DivisionID ?? (object)DBNull.Value)
            };

            _dbHelper.ExecuteNonQuery(query, parameters);
        }

        public void UpdateUser(User user)
        {
            var query = @"
                UPDATE USERS 
                SET FullName = @FullName, 
                    Email = @Email, 
                    Mobile = @Mobile, 
                    RoleID = @RoleID, 
                    DivisionID = @DivisionID,
                    IsActive = @IsActive
                WHERE UserID = @UserID";

            // Note: Password update should be separate or handled with care if included here. 
            // For now, normal update doesn't change password unless we add explicit logic.
            // If PasswordHash is provided and different, we might update it, but typically separating ChangePassword is safer.
            // Let's stick to updating profile fields + Role/Division/Active status.
            
            // If we want to support password reset in Edit, we check if PasswordHash is not empty and different?
            // Simpler: Just update profile fields. Password reset can be a separate action or flag.
            
            // However, the `user` object might have the hash.
            // Let's update basic fields here.

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@FullName", user.FullName),
                new SqlParameter("@Email", user.Email ?? (object)DBNull.Value),
                new SqlParameter("@Mobile", user.Mobile ?? (object)DBNull.Value),
                new SqlParameter("@RoleID", user.RoleID),
                new SqlParameter("@DivisionID", user.DivisionID ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", user.IsActive),
                new SqlParameter("@UserID", user.UserID)
            };

            // Assuming password change is optional during edit. 
            // If the caller sets PasswordHash, we update it.
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                 query = @"
                    UPDATE USERS 
                    SET FullName = @FullName, 
                        PasswordHash = @PasswordHash,
                        Email = @Email, 
                        Mobile = @Mobile, 
                        RoleID = @RoleID, 
                        DivisionID = @DivisionID,
                        IsActive = @IsActive
                    WHERE UserID = @UserID";
                 parameters.Add(new SqlParameter("@PasswordHash", user.PasswordHash));
            }

            _dbHelper.ExecuteNonQuery(query, parameters.ToArray());
        }

        public void DeactivateUser(int id)
        {
            var query = "UPDATE USERS SET IsActive = 0 WHERE UserID = @UserID";
            _dbHelper.ExecuteNonQuery(query, new[] { new SqlParameter("@UserID", id) });
        }

        public User? GetUserByUsername(string username)
        {
            var query = @"
                SELECT u.UserID, u.Username, u.PasswordHash, u.FullName, u.Email, u.Mobile,
                       u.RoleID, r.RoleName, u.DivisionID, d.DivisionNameEnglish as DivisionName,
                       u.IsActive, u.LastLogin, u.CreatedDate
                FROM USERS u
                INNER JOIN ROLE_MASTER r ON u.RoleID = r.RoleID
                LEFT JOIN DIVISION_MASTER d ON u.DivisionID = d.DivisionID
                WHERE u.Username = @Username AND u.IsActive = 1";

            var dt = _dbHelper.ExecuteQuery(query, new[] { new SqlParameter("@Username", username) });

            if (dt.Rows.Count == 0)
                return null;

            var row = dt.Rows[0];
            return new User
            {
                UserID = Convert.ToInt32(row["UserID"]),
                Username = row["Username"].ToString() ?? string.Empty,
                PasswordHash = row["PasswordHash"].ToString() ?? string.Empty,
                FullName = row["FullName"].ToString() ?? string.Empty,
                Email = row["Email"] != DBNull.Value ? row["Email"].ToString() : null,
                Mobile = row["Mobile"] != DBNull.Value ? row["Mobile"].ToString() : null,
                RoleID = Convert.ToInt32(row["RoleID"]),
                RoleName = row["RoleName"].ToString() ?? string.Empty,
                DivisionID = row["DivisionID"] != DBNull.Value ? Convert.ToInt32(row["DivisionID"]) : null,
                DivisionName = row["DivisionName"] != DBNull.Value ? row["DivisionName"].ToString() : null,
                IsActive = Convert.ToBoolean(row["IsActive"]),
                LastLogin = row["LastLogin"] != DBNull.Value ? Convert.ToDateTime(row["LastLogin"]) : null,
                CreatedDate = Convert.ToDateTime(row["CreatedDate"])
            };
        }

        public void UpdateLastLogin(int userId)
        {
            var query = "UPDATE USERS SET LastLogin = GETDATE() WHERE UserID = @UserID";
            var parameters = new[] { new SqlParameter("@UserID", userId) };
            _dbHelper.ExecuteNonQuery(query, parameters);
        }

        public void SaveOtp(int userId, string otp, DateTime expiryTime)
        {
            var query = "INSERT INTO USER_OTP_VERIFICATIONS (UserID, OTP, ExpiryTime) VALUES (@UserID, @OTP, @ExpiryTime)";
            var parameters = new[]
            {
                new SqlParameter("@UserID", userId),
                new SqlParameter("@OTP", otp),
                new SqlParameter("@ExpiryTime", expiryTime)
            };
            _dbHelper.ExecuteNonQuery(query, parameters);
        }

        public bool VerifyOtp(int userId, string otp)
        {
            var query = @"
                SELECT COUNT(*) FROM USER_OTP_VERIFICATIONS 
                WHERE UserID = @UserID AND OTP = @OTP 
                  AND ExpiryTime > GETDATE() AND IsVerified = 0";
            
            var parameters = new[]
            {
                new SqlParameter("@UserID", userId),
                new SqlParameter("@OTP", otp)
            };
            
            int count = (int)(_dbHelper.ExecuteScalar(query, parameters) ?? 0);
            return count > 0;
        }

        public void MarkOtpAsVerified(int userId, string otp)
        {
            var query = @"
                UPDATE USER_OTP_VERIFICATIONS 
                SET IsVerified = 1 
                WHERE UserID = @UserID AND OTP = @OTP";
            
            var parameters = new[]
            {
                new SqlParameter("@UserID", userId),
                new SqlParameter("@OTP", otp)
            };
            
            _dbHelper.ExecuteNonQuery(query, parameters);
        }

        public bool UpdatePassword(string username, string passwordHash)
        {
            var query = "UPDATE USERS SET PasswordHash = @PasswordHash WHERE Username = @Username";
            var parameters = new[]
            {
                new SqlParameter("@PasswordHash", passwordHash),
                new SqlParameter("@Username", username)
            };
            
            return _dbHelper.ExecuteNonQuery(query, parameters) > 0;
        }
    }
}
