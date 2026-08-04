using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Account;

public sealed class GoogleLinkViewModel
{
    [Required]
    public string Ticket { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string Password { get; set; } = string.Empty;
}

public sealed class GoogleLinkOtpViewModel
{
    [Required]
    public string ChallengeId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã OTP không được để trống.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP gồm 6 chữ số.")]
    [RegularExpression("^\\d{6}$", ErrorMessage = "Mã OTP gồm 6 chữ số.")]
    [Display(Name = "Mã OTP")]
    public string Code { get; set; } = string.Empty;
}
