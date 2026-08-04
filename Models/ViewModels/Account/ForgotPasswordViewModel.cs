using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Account;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}
