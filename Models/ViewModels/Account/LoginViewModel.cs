using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Account;

// Form đăng nhập (POST Account/Login)
public class LoginViewModel
{
    // Username dùng cho đăng nhập local
    [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    // Mật khẩu
    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    // true: cookie ~30 ngày; false: ~1 ngày
    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }
}
