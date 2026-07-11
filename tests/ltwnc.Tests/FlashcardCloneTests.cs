using ltwnc.Models.Entities;

namespace ltwnc.Tests;

public class FlashcardCloneTests
{
    [Fact]
    public void Clone_creates_new_instance_with_reset_identity()
    {
        var original = new Flashcard
        {
            Id = 42,
            FlashcardSetId = 7,
            FrontText = "hello",
            BackText = "xin chào",
            Pronunciation = "/həˈloʊ/",
            PartOfSpeech = "interjection",
            ExampleSentence = "Hello, world.",
            ExampleMeaning = "Chào, thế giới.",
            Synonyms = "hi, hey",
            ImageUrl = "https://example.com/image.png",
            UploadedImagePath = "/uploads/flashcards/original.png",
            IsStarred = true,
            OrderIndex = 3
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(0, clone.Id);
        Assert.Equal(0, clone.FlashcardSetId);
    }

    [Fact]
    public void Clone_preserves_study_content()
    {
        var original = new Flashcard
        {
            FrontText = "hello",
            BackText = "xin chào",
            Pronunciation = "/həˈloʊ/",
            PartOfSpeech = "interjection",
            ExampleSentence = "Hello, world.",
            ExampleMeaning = "Chào, thế giới.",
            Synonyms = "hi, hey",
            ImageUrl = "https://example.com/image.png",
            IsStarred = true,
            OrderIndex = 3
        };

        var clone = original.Clone();

        Assert.Equal(original.FrontText, clone.FrontText);
        Assert.Equal(original.BackText, clone.BackText);
        Assert.Equal(original.Pronunciation, clone.Pronunciation);
        Assert.Equal(original.PartOfSpeech, clone.PartOfSpeech);
        Assert.Equal(original.ExampleSentence, clone.ExampleSentence);
        Assert.Equal(original.ExampleMeaning, clone.ExampleMeaning);
        Assert.Equal(original.Synonyms, clone.Synonyms);
        Assert.Equal(original.ImageUrl, clone.ImageUrl);
        Assert.False(clone.IsStarred);
        Assert.Equal(original.OrderIndex, clone.OrderIndex);
    }

    [Fact]
    public void Clone_clears_uploaded_image_path()
    {
        var original = new Flashcard
        {
            FrontText = "hello",
            BackText = "xin chào",
            UploadedImagePath = "/uploads/flashcards/original.png"
        };

        var clone = original.Clone();

        Assert.Null(clone.UploadedImagePath);
    }

    [Fact]
    public void Clone_is_independent_from_source()
    {
        var original = new Flashcard
        {
            FrontText = "hello",
            BackText = "xin chào",
            IsStarred = false
        };

        var clone = original.Clone();
        clone.FrontText = "goodbye";
        clone.IsStarred = true;

        Assert.Equal("hello", original.FrontText);
        Assert.False(original.IsStarred);
    }
}
