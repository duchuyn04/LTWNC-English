namespace ltwnc.Models.ViewModels.FlashcardSet;

public class EditorViewModel
{
    public int? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public bool IsQuarantined { get; set; }
    public string? ModerationPublicReason { get; set; }
    public DateTime? ModeratedAtUtc { get; set; }
    public List<CardViewModel> Cards { get; set; } = new();
}

public class CardViewModel
{
    public int Id { get; set; }
    public string FrontText { get; set; } = string.Empty;
    public string BackText { get; set; } = string.Empty;
    public string? Pronunciation { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ExampleMeaning { get; set; }
    public string? Synonyms { get; set; }
    public string? ImageUrl { get; set; }
    public string? UploadedImagePath { get; set; }
    public bool IsStarred { get; set; }
    public int OrderIndex { get; set; }
}
