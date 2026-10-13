using MVCCaseManagement.Models;
using System.Collections.Generic;

namespace MVCCaseManagement.DAL
{
    public interface IPettyBillRepository
    {
        /// <summary>
        /// Saves a new petty bill to the database
        /// </summary>
        /// <param name="model">The petty bill view model</param>
        /// <returns>The ID of the newly created bill</returns>
        int SavePettyBill(PettyBillViewModel model);

        /// <summary>
        /// Retrieves a petty bill by its ID
        /// </summary>
        /// <param name="billId">The bill ID</param>
        /// <returns>The petty bill view model or null if not found</returns>
        PettyBillViewModel? GetPettyBillById(int billId);

        /// <summary>
        /// Retrieves all petty bills for a specific case
        /// </summary>
        /// <param name="caseId">The case ID</param>
        /// <returns>List of petty bills for the case</returns>
        List<PettyBillViewModel> GetPettyBillsByCaseId(int caseId);

        /// <summary>
        /// Retrieves all petty bills for a specific MVC number and year
        /// </summary>
        /// <param name="mvcNo">MVC Number</param>
        /// <param name="mvcYear">MVC Year</param>
        /// <returns>List of petty bills</returns>
        List<PettyBillViewModel> GetPettyBillsByMVC(string mvcNo, int mvcYear);

        /// <summary>
        /// Retrieves all unique payments used in petty bills for a specific Case ID
        /// </summary>
        /// <param name="caseId">Case ID</param>
        /// <returns>List of previous payments found in bills</returns>
        List<PreviousPayment> GetAllBillPaymentsByCaseId(int caseId);

        /// <summary>
        /// Updates an existing petty bill
        /// </summary>
        /// <param name="model">The updated petty bill view model</param>
        /// <returns>True if update was successful</returns>
        bool UpdatePettyBill(PettyBillViewModel model);

        /// <summary>
        /// Deletes a petty bill by its ID
        /// </summary>
        /// <param name="billId">The bill ID to delete</param>
        /// <returns>True if deletion was successful</returns>
        bool DeletePettyBill(int billId);

        /// <summary>
        /// Deletes a specific payment record from the petty bill payments table
        /// </summary>
        /// <param name="paymentId">The ID of the payment to delete</param>
        /// <returns>True if deletion was successful</returns>
        bool DeletePettyBillPayment(int paymentId);
    }
}
