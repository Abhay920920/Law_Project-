using System.Threading.Tasks;

namespace MVCCaseManagement.Utils
{
    public interface ISMSService
    {
        /// <summary>
        /// Sends a One-Time Password to the specified mobile number.
        /// </summary>
        /// <param name="mobileNumber">Recipient mobile number (e.g., 9481358999)</param>
        /// <param name="otp">6-digit OTP code</param>
        /// <returns>True if the request was successfully sent to the gateway.</returns>
        Task<bool> SendOTPAsync(string mobileNumber, string otp);

        string LastResponse { get; }
    }
}
