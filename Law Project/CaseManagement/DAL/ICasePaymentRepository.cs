using MVCCaseManagement.Models;
using System.Collections.Generic;

namespace MVCCaseManagement.DAL
{
    public interface ICasePaymentRepository
    {
        /// <summary>
        /// Gets all payment records for a specific case
        /// </summary>
        List<CasePaymentViewModel> GetPaymentsByCaseId(int caseId);
        
        /// <summary>
        /// Adds a new payment record and returns the PaymentID
        /// </summary>
        int AddPayment(CasePaymentViewModel payment);
        
        /// <summary>
        /// Updates an existing payment record
        /// </summary>
        bool UpdatePayment(CasePaymentViewModel payment);
        
        /// <summary>
        /// Deletes a payment record by ID
        /// </summary>
        bool DeletePayment(int paymentId);
        
        /// <summary>
        /// Calculates total payments for a case
        /// </summary>
        decimal GetTotalPaymentsByCaseId(int caseId);
    }
}
