using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Account;

public sealed class AdminTwoFactorRecoveryCodeViewModel
{
    [Required(ErrorMessage = "Mã khôi phục không được để trống.")]
    [Display(Name = "Mã khôi phục")]
    public string RecoveryCode { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = "/Admin";
}
