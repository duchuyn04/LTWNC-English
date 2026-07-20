using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Flashcards;

// Dữ liệu thẻ an toàn cho tầng hiển thị; không đưa entity EF hoặc navigation property ra View.
public class FlashcardViewModel
{
    public int Id { get; set; }

    public int SetId { get; set; }

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
}

public static class FlashcardViewModelMapper
{
    public static FlashcardViewModel FromEntity(Flashcard card)
    {
        return new FlashcardViewModel
        {
            Id = card.Id,
            SetId = card.FlashcardSetId,
            FrontText = card.FrontText,
            BackText = card.BackText,
            Pronunciation = card.Pronunciation,
            PartOfSpeech = card.PartOfSpeech,
            ExampleSentence = card.ExampleSentence,
            ExampleMeaning = card.ExampleMeaning,
            Synonyms = card.Synonyms,
            ImageUrl = card.ImageUrl,
            UploadedImagePath = card.UploadedImagePath,
            IsStarred = card.IsStarred,
            OrderIndex = card.OrderIndex
        };
    }

    public static IReadOnlyList<FlashcardViewModel> FromEntities(IEnumerable<Flashcard> cards)
    {
        return cards.Select(FromEntity).ToList();
    }
}
