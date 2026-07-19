using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Account;

public sealed class AdminTwoFactorVerifyViewModel
{
    [Required(ErrorMessage = "Mã xác thực không được để trống.")]
    [Display(Name = "Mã xác thực")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }

    public string ReturnUrl { get; set; } = "/Admin";
}
