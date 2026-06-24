namespace ltwnc.Models.ViewModels.Home;

public class HomeViewModel
{
    public List<ltwnc.Models.Entities.FlashcardSet> PublicSets { get; set; } = new();
    public string? SearchQuery { get; set; }
}
