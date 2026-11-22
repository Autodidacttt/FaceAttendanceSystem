using System.ComponentModel.DataAnnotations;

namespace FaceAttendanceSystem.ViewModels
{
    public class LoginViewModel
    {
        [Required, StringLength(50)]
        public string Username { get; set; }

        [Required, DataType(DataType.Password), StringLength(100)]
        public string Password { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required, DataType(DataType.Password), Display(Name = "Current password")]
        public string CurrentPassword { get; set; }

        [Required, DataType(DataType.Password), Display(Name = "New password")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "The new password must be at least 8 characters.")]
        [RegularExpression(PasswordPolicy.Pattern, ErrorMessage = PasswordPolicy.Message)]
        public string NewPassword { get; set; }

        [Required, DataType(DataType.Password), Display(Name = "Confirm new password")]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; }

        public bool IsForced { get; set; }
    }

    public static class PasswordPolicy
    {
        public const string Pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,100}$";
        public const string Message = "Use at least 8 characters with an upper-case letter, a lower-case letter and a digit.";
    }
}
