using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public interface IUserRepository
    {
        IEnumerable<User> GetAllUsers();
        User? GetUserById(int id);
        void AddUser(User user);
        void UpdateUser(User user);
        void DeactivateUser(int id);
        User? GetUserByUsername(string username);
        void UpdateLastLogin(int userId);
        
        // OTP methods
        void SaveOtp(int userId, string otp, DateTime expiryTime);
        bool VerifyOtp(int userId, string otp);
        void MarkOtpAsVerified(int userId, string otp);
        bool UpdatePassword(string username, string passwordHash);
    }
}
