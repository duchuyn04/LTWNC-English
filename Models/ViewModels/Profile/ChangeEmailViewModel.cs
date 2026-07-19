using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Profile;

public sealed class ChangeEmailViewModel
{
    [Required, EmailAddress]
    public string NewEmail { get; set; } = string.Empty;

    [Required, EmailAddress, Compare(nameof(NewEmail))]
    public string ConfirmEmail { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;
}
