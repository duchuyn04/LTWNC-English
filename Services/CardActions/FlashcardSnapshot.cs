namespace ltwnc.Services.CardActions;

public class FlashcardSnapshot
{
    public int Id { get; set; }
    public int FlashcardSetId { get; set; }
    public string FrontText { get; set; } = string.Empty;
    public string BackText { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string PartOfSpeech { get; set; } = string.Empty;
    public string ExampleSentence { get; set; } = string.Empty;
    public string ExampleMeaning { get; set; } = string.Empty;
    public string? Synonyms { get; set; }
    public string? ImageUrl { get; set; }
    public string? UploadedImagePath { get; set; }
    public bool IsStarred { get; set; }
    public int OrderIndex { get; set; }
    public List<UserProgressSnapshot> UserProgresses { get; set; } = [];
    public List<DictationSessionDetailSnapshot> DictationSessionDetails { get; set; } = [];
}

public class DictationSessionDetailSnapshot
{
    public int Id { get; set; }
    public int StudySessionId { get; set; }
    public int FlashcardId { get; set; }
    public bool IsCorrect { get; set; }
    public string AnsweredText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
