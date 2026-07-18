namespace ltwnc.Models.ViewModels.FlashcardSet;

public class CreateSetRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
}

public class UpdateSetRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
}

public class CreateCardRequest
{
    public int SetId { get; set; }
    public string FrontText { get; set; } = string.Empty;
    public string BackText { get; set; } = string.Empty;
    public string? Pronunciation { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ExampleMeaning { get; set; }
    public string? Synonyms { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsStarred { get; set; }
}

public class UpdateCardRequest : CreateCardRequest
{
    public int Id { get; set; }
}

public class CardResponse
{
    public int Id { get; set; }
    public int SetId { get; set; }
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

public class BatchImportRequest
{
    public int SetId { get; set; }
    public List<CreateCardRequest> Cards { get; set; } = new();
    public bool ReplaceAll { get; set; }
}

public class ReorderRequest
{
    public int SetId { get; set; }
    public int[] OrderedCardIds { get; set; } = Array.Empty<int>();
}
