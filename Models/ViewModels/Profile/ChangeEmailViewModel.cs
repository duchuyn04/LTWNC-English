using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Profile;

public sealed class ChangeEmailViewModel
{
    [Required, EmailAddress]
    public string NewEmail { get; set; } = string.Empty;
}
