using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Account;

public sealed class AdminTwoFactorSetupViewModel
{
    public string SharedKey { get; init; } = string.Empty;
    public string AuthenticatorUri { get; init; } = string.Empty;
    public string ReturnUrl { get; set; } = "/Admin";

    [Required(ErrorMessage = "Mã xác thực không được để trống.")]
    [Display(Name = "Mã xác thực")]
    public string Code { get; set; } = string.Empty;
}
