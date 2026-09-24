using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class User
    {
        public int UserID { get; set; }

        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        public string PasswordHash { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? Mobile { get; set; }

        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;

        public int? DivisionID { get; set; }
        public string? DivisionName { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? LastLogin { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public class UserFormViewModel
    {
        public int UserID { get; set; }

        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;

        // Password is required for new users, optional for updates
        public string? Password { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? Mobile { get; set; }

        [Required(ErrorMessage = "Role is required")]
        public int RoleID { get; set; }

        public int? DivisionID { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
