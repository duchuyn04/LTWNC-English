using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Account;

// Form đăng nhập (POST Account/Login)
public class LoginViewModel
{
    // Email Identity
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    // Mật khẩu
    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    // true: cookie ~30 ngày; false: ~1 ngày
    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }
}
