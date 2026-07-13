using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Account;

// Form đăng nhập (POST Account/Login)
public class LoginViewModel
{
    // Email Identity
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    // Mật khẩu
    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    // true: cookie ~30 ngày; false: ~1 ngày
    public bool RememberMe { get; set; }
}
