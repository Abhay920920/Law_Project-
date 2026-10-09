using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using MVCCaseManagement.Common; // For PasswordHelper
using MVCCaseManagement.Services.Audit;

namespace MVCCaseManagement.Controllers
{
    [Authorize(Policy = "MasterDataAccess")]
    public class MasterController : Controller
    {
        private readonly IMasterRepository _masterRepo;
        private readonly IUserRepository _userRepo;
        private readonly ICaseActivityLogger _activityLogger;

        public MasterController(IMasterRepository masterRepo, IUserRepository userRepo, ICaseActivityLogger activityLogger)
        {
            _masterRepo = masterRepo;
            _userRepo = userRepo;
            _activityLogger = activityLogger;
        }

        public IActionResult Index()
        {
            return View();
        }

        // --- USER ACTIONS ---
        public IActionResult Users()
        {
            var users = _userRepo.GetAllUsers();
            return View(users);
        }

        [HttpGet]
        public IActionResult UserCreate()
        {
            PopulateDropdowns();
            return View("UserForm", new UserFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UserCreate(UserFormViewModel model)
        {
            if (string.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError("Password", "Password is required for new users.");
            }
            else if (model.Password.Length < 8)
            {
                ModelState.AddModelError("Password", "Password must be at least 8 characters long.");
            }

            // Check username uniqueness
            if (_userRepo.GetUserByUsername(model.Username) != null)
            {
                ModelState.AddModelError("Username", "Username is already taken.");
            }

            if (ModelState.IsValid)
            {
                var userEntity = new MVCCaseManagement.Models.User
                {
                    Username = model.Username,
                    PasswordHash = PasswordHelper.HashPassword(model.Password!),
                    FullName = model.FullName,
                    Email = model.Email,
                    Mobile = model.Mobile,
                    RoleID = model.RoleID,
                    DivisionID = model.DivisionID,
                    IsActive = model.IsActive
                };
                _userRepo.AddUser(userEntity);

                _ = _activityLogger.LogSubEntityActionAsync(
                    "MASTER",
                    userEntity.UserID,
                    userEntity.Username,
                    "USER_CREATED",
                    $"Created system user '{userEntity.Username}' ({userEntity.FullName}) with Role ID {userEntity.RoleID}, Division ID {userEntity.DivisionID}",
                    new { userEntity.Username, userEntity.FullName, userEntity.Email, userEntity.RoleID, userEntity.DivisionID });

                return RedirectToAction("Users");
            }
            PopulateDropdowns();
            return View("UserForm", model);
        }

        [HttpGet]
        public IActionResult UserEdit(int id)
        {
            var userEntity = _userRepo.GetUserById(id);
            if (userEntity == null) return NotFound();

            var model = new UserFormViewModel
            {
                UserID = userEntity.UserID,
                Username = userEntity.Username,
                FullName = userEntity.FullName,
                Email = userEntity.Email,
                Mobile = userEntity.Mobile,
                RoleID = userEntity.RoleID,
                DivisionID = userEntity.DivisionID,
                IsActive = userEntity.IsActive
            };
            PopulateDropdowns();
            return View("UserForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UserEdit(UserFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userEntity = new MVCCaseManagement.Models.User
                {
                    UserID = model.UserID,
                    FullName = model.FullName,
                    Email = model.Email,
                    Mobile = model.Mobile,
                    RoleID = model.RoleID,
                    DivisionID = model.DivisionID,
                    IsActive = model.IsActive,
                    PasswordHash = !string.IsNullOrEmpty(model.Password) ? PasswordHelper.HashPassword(model.Password!) : string.Empty
                };

                _userRepo.UpdateUser(userEntity);

                _ = _activityLogger.LogSubEntityActionAsync(
                    "MASTER",
                    userEntity.UserID,
                    model.Username ?? $"User #{userEntity.UserID}",
                    "USER_UPDATED",
                    $"Updated system user '{model.Username}' ({userEntity.FullName}), Active: {userEntity.IsActive}, Role: {userEntity.RoleID}, Div: {userEntity.DivisionID}",
                    new { userEntity.UserID, model.Username, userEntity.FullName, userEntity.RoleID, userEntity.DivisionID, userEntity.IsActive });

                return RedirectToAction("Users");
            }
            PopulateDropdowns();
            return View("UserForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UserDelete(int id)
        {
            _userRepo.DeactivateUser(id);

            _ = _activityLogger.LogSubEntityActionAsync(
                "MASTER",
                id,
                $"User #{id}",
                "USER_DEACTIVATED",
                $"Deactivated system user #{id}",
                new { UserID = id });

            return RedirectToAction("Users");
        }

        private void PopulateDropdowns()
        {
            ViewBag.Roles = _masterRepo.GetAllRoles();
            ViewBag.Divisions = _masterRepo.GetAllDivisions();
        }

        // --- ADVOCATE ACTIONS ---
        public IActionResult Advocates()
        {
            var advocates = _masterRepo.GetAllAdvocates();
            return View(advocates);
        }

        [HttpGet]
        public IActionResult AdvocateCreate()
        {
            return View("AdvocateForm", new Advocate());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdvocateCreate(Advocate advocate)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.AddAdvocate(advocate);
                return RedirectToAction("Advocates");
            }
            return View("AdvocateForm", advocate);
        }

        [HttpGet]
        public IActionResult AdvocateEdit(int id)
        {
            var advocate = _masterRepo.GetAdvocateById(id);
            if (advocate == null) return NotFound();
            return View("AdvocateForm", advocate);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdvocateEdit(Advocate advocate)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.UpdateAdvocate(advocate);
                return RedirectToAction("Advocates");
            }
            return View("AdvocateForm", advocate);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdvocateDelete(int id)
        {
            _masterRepo.DeleteAdvocate(id);
            return RedirectToAction("Advocates");
        }

        // --- MACT ACTIONS ---
        public IActionResult MACTs()
        {
            var macts = _masterRepo.GetAllMACTs();
            return View(macts);
        }

        [HttpGet]
        public IActionResult MACTCreate()
        {
            return View("MACTForm", new MACT());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MACTCreate(MACT mact)
        {
            // MACTCode is generated in repo if empty
            ModelState.Remove("MACTCode"); 

            if (ModelState.IsValid)
            {
                _masterRepo.AddMACT(mact);
                return RedirectToAction("MACTs");
            }
            return View("MACTForm", mact);
        }

        [HttpGet]
        public IActionResult MACTEdit(int id)
        {
            var mact = _masterRepo.GetMACTById(id);
            if (mact == null) return NotFound();
            return View("MACTForm", mact);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MACTEdit(MACT mact)
        {
            ModelState.Remove("MACTCode");
            if (ModelState.IsValid)
            {
                _masterRepo.UpdateMACT(mact);
                return RedirectToAction("MACTs");
            }
            return View("MACTForm", mact);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MACTDelete(int id)
        {
            _masterRepo.DeleteMACT(id);
            return RedirectToAction("MACTs");
        }

        // --- HIGH COURT ADVOCATE ACTIONS ---
        public IActionResult HighCourtAdvocates()
        {
            var advocates = _masterRepo.GetHighCourtAdvocates();
            return View(advocates);
        }

        [HttpGet]
        public IActionResult HighCourtAdvocateCreate()
        {
            return View("HighCourtAdvocateForm", new HighCourtAdvocate());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult HighCourtAdvocateCreate(HighCourtAdvocate advocate)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.AddHighCourtAdvocate(advocate);
                return RedirectToAction("HighCourtAdvocates");
            }
            return View("HighCourtAdvocateForm", advocate);
        }

        [HttpGet]
        public IActionResult HighCourtAdvocateEdit(int id)
        {
            var advocate = _masterRepo.GetHighCourtAdvocateById(id);
            if (advocate == null) return NotFound();
            return View("HighCourtAdvocateForm", advocate);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult HighCourtAdvocateEdit(HighCourtAdvocate advocate)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.UpdateHighCourtAdvocate(advocate);
                return RedirectToAction("HighCourtAdvocates");
            }
            return View("HighCourtAdvocateForm", advocate);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult HighCourtAdvocateDelete(int id)
        {
            _masterRepo.DeleteHighCourtAdvocate(id);
            return RedirectToAction("HighCourtAdvocates");
        }

        // --- GRATUITY COURT ACTIONS ---
        public IActionResult GratuityCourts()
        {
            var courts = _masterRepo.GetAllGratuityCourts();
            return View(courts);
        }

        [HttpGet]
        public IActionResult GratuityCourtCreate()
        {
            return View("GratuityCourtForm", new GratuityCourt());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GratuityCourtCreate(GratuityCourt court)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.AddGratuityCourt(court);
                return RedirectToAction("GratuityCourts");
            }
            return View("GratuityCourtForm", court);
        }

        [HttpGet]
        public IActionResult GratuityCourtEdit(int id)
        {
            var court = _masterRepo.GetGratuityCourtById(id);
            if (court == null) return NotFound();
            return View("GratuityCourtForm", court);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GratuityCourtEdit(GratuityCourt court)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.UpdateGratuityCourt(court);
                return RedirectToAction("GratuityCourts");
            }
            return View("GratuityCourtForm", court);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GratuityCourtDelete(int id)
        {
            _masterRepo.DeleteGratuityCourt(id);
            return RedirectToAction("GratuityCourts");
        }

        // --- GRATUITY ADVOCATE ACTIONS ---
        public IActionResult GratuityAdvocates()
        {
            var advocates = _masterRepo.GetAllGratuityAdvocates();
            return View(advocates);
        }

        [HttpGet]
        public IActionResult GratuityAdvocateCreate()
        {
            return View("GratuityAdvocateForm", new GratuityAdvocate());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GratuityAdvocateCreate(GratuityAdvocate advocate)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.AddGratuityAdvocate(advocate);
                return RedirectToAction("GratuityAdvocates");
            }
            return View("GratuityAdvocateForm", advocate);
        }

        [HttpGet]
        public IActionResult GratuityAdvocateEdit(int id)
        {
            var advocate = _masterRepo.GetGratuityAdvocateById(id);
            if (advocate == null) return NotFound();
            return View("GratuityAdvocateForm", advocate);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GratuityAdvocateEdit(GratuityAdvocate advocate)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.UpdateGratuityAdvocate(advocate);
                return RedirectToAction("GratuityAdvocates");
            }
            return View("GratuityAdvocateForm", advocate);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GratuityAdvocateDelete(int id)
        {
            _masterRepo.DeleteGratuityAdvocate(id);
            return RedirectToAction("GratuityAdvocates");
        }
        // --- LABOUR COURT ACTIONS ---
        public IActionResult LabourCourts()
        {
            var courts = _masterRepo.GetAllLabourCourts();
            return View(courts);
        }

        [HttpGet]
        public IActionResult LabourCourtCreate()
        {
            return View("LabourCourtForm", new LabourCourt());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LabourCourtCreate(LabourCourt court)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.AddLabourCourt(court);
                return RedirectToAction("LabourCourts");
            }
            return View("LabourCourtForm", court);
        }

        [HttpGet]
        public IActionResult LabourCourtEdit(int id)
        {
            var court = _masterRepo.GetLabourCourtById(id);
            if (court == null) return NotFound();
            return View("LabourCourtForm", court);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LabourCourtEdit(LabourCourt court)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.UpdateLabourCourt(court);
                return RedirectToAction("LabourCourts");
            }
            return View("LabourCourtForm", court);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LabourCourtDelete(int id)
        {
            _masterRepo.DeleteLabourCourt(id);
            return RedirectToAction("LabourCourts");
        }

        // --- LABOUR ADVOCATE ACTIONS ---
        public IActionResult LabourAdvocates()
        {
            var advocates = _masterRepo.GetAllLabourAdvocates();
            return View(advocates);
        }

        [HttpGet]
        public IActionResult LabourAdvocateCreate()
        {
            return View("LabourAdvocateForm", new LabourAdvocate());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LabourAdvocateCreate(LabourAdvocate advocate)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.AddLabourAdvocate(advocate);
                return RedirectToAction("LabourAdvocates");
            }
            return View("LabourAdvocateForm", advocate);
        }

        [HttpGet]
        public IActionResult LabourAdvocateEdit(int id)
        {
            var advocate = _masterRepo.GetLabourAdvocateById(id);
            if (advocate == null) return NotFound();
            return View("LabourAdvocateForm", advocate);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LabourAdvocateEdit(LabourAdvocate advocate)
        {
            if (ModelState.IsValid)
            {
                _masterRepo.UpdateLabourAdvocate(advocate);
                return RedirectToAction("LabourAdvocates");
            }
            return View("LabourAdvocateForm", advocate);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LabourAdvocateDelete(int id)
        {
            _masterRepo.DeleteLabourAdvocate(id);
            return RedirectToAction("LabourAdvocates");
        }

        // --- GLOBAL CASE DELETION ---
        [HttpGet]
        public IActionResult DeleteCase()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCaseConfirm(string module, string caseNumber, int caseYear, int mactId, string courtName)
        {
            if (string.IsNullOrEmpty(caseNumber))
            {
                TempData["ErrorMessage"] = "Please provide a Case Number.";
                return RedirectToAction("DeleteCase");
            }

            try
            {
                int deletedCount = 0;
                string trimmedNo = caseNumber.Trim();

                if (module == "MVC")
                {
                    if (caseYear <= 0) 
                    {
                        TempData["ErrorMessage"] = "Case Year is required for MVC cases.";
                        return RedirectToAction("DeleteCase");
                    }
                    deletedCount = _masterRepo.DeleteMVCRecord(trimmedNo, caseYear, mactId);
                }
                else if (module == "Labour")
                {
                    if (caseYear <= 0)
                    {
                        TempData["ErrorMessage"] = "Case Year is required for Labour cases.";
                        return RedirectToAction("DeleteCase");
                    }
                    deletedCount = _masterRepo.DeleteLabourRecord(trimmedNo, caseYear, courtName);
                }
                else if (module == "Gratuity")
                {
                    if (caseYear <= 0)
                    {
                        TempData["ErrorMessage"] = "Case Year is required for Gratuity cases.";
                        return RedirectToAction("DeleteCase");
                    }
                    deletedCount = _masterRepo.DeleteGratuityRecord(trimmedNo, caseYear, courtName);
                }
                else // Global / All
                {
                    if (caseYear <= 0)
                    {
                        TempData["ErrorMessage"] = "Case Year is required for Global search.";
                        return RedirectToAction("DeleteCase");
                    }
                    deletedCount = _masterRepo.DeleteCaseByNumberAndYear(trimmedNo, caseYear);
                }
                
                if (deletedCount > 0)
                {
                    _ = _activityLogger.LogCaseDeletedAsync(
                        module?.ToUpper() ?? "CASE",
                        0,
                        trimmedNo,
                        null,
                        $"Admin Global Deletion: Deleted {deletedCount} record(s) for {module} Case #{trimmedNo}/{caseYear} (Court: {courtName})");

                    TempData["SuccessMessage"] = $"Successfully deleted {deletedCount} record(s) matching your criteria.";
                }
                else
                {
                    TempData["WarningMessage"] = "No cases found matching the provided details.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting case: " + ex.Message;
            }

            return RedirectToAction("DeleteCase");
        }
    }
}
