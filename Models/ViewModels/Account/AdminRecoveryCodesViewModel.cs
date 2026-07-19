namespace ltwnc.Models.ViewModels.Account;

public sealed class AdminRecoveryCodesViewModel
{
    public IReadOnlyList<string> RecoveryCodes { get; init; } = [];
    public string ReturnUrl { get; init; } = "/Admin";
}
