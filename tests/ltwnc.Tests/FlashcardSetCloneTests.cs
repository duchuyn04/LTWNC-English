using ltwnc.Models.Entities;

namespace ltwnc.Tests;

public class FlashcardSetCloneTests
{
    [Fact]
    public void Clone_creates_new_set_with_reset_identity()
    {
        var original = new FlashcardSet
        {
            Id = 10,
            Title = "Public Set",
            Description = "A public set",
            UserId = "author",
            IsPublic = true,
            SourceSetId = 5,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(0, clone.Id);
        Assert.Equal(string.Empty, clone.UserId);
        Assert.True(clone.IsPublic);
        Assert.Null(clone.SourceSetId);
    }

    [Fact]
    public void Clone_preserves_title_and_description()
    {
        var original = new FlashcardSet
        {
            Title = "Public Set",
            Description = "A public set"
        };

        var clone = original.Clone();

        Assert.Equal(original.Title, clone.Title);
        Assert.Equal(original.Description, clone.Description);
    }

    [Fact]
    public void Clone_resets_timestamps_to_now()
    {
        var before = DateTime.UtcNow;
        var original = new FlashcardSet
        {
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var clone = original.Clone();

        var after = DateTime.UtcNow;
        Assert.True(clone.CreatedAt >= before && clone.CreatedAt <= after);
        Assert.True(clone.UpdatedAt >= before && clone.UpdatedAt <= after);
    }

    [Fact]
    public void Clone_deep_copies_flashcards()
    {
        var original = new FlashcardSet
        {
            Title = "Set with cards",
            Flashcards =
            [
                new Flashcard { Id = 1, FrontText = "hello", BackText = "xin chào" },
                new Flashcard { Id = 2, FrontText = "world", BackText = "thế giới" }
            ]
        };

        var clone = original.Clone();

        Assert.Equal(2, clone.Flashcards.Count);
        Assert.All(clone.Flashcards, c => Assert.Equal(0, c.Id));
        Assert.All(clone.Flashcards, c => Assert.Equal(0, c.FlashcardSetId));
        Assert.DoesNotContain(clone.Flashcards, c => original.Flashcards.Any(oc => ReferenceEquals(oc, c)));

        var clonedCard = clone.Flashcards.First();
        clonedCard.FrontText = "changed";

        Assert.Equal("hello", original.Flashcards.First().FrontText);
    }

    [Fact]
    public void Clone_empty_flashcards_collection()
    {
        var original = new FlashcardSet { Title = "Empty set" };

        var clone = original.Clone();

        Assert.Empty(clone.Flashcards);
    }

    [Fact]
    public void Clone_resets_IsStarred()
    {
        var original = new FlashcardSet
        {
            Title = "Set",
            Flashcards =
            [
                new Flashcard { FrontText = "a", BackText = "b", IsStarred = true }
            ]
        };

        var clone = original.Clone();

        Assert.All(clone.Flashcards, c => Assert.False(c.IsStarred));
    }

    [Fact]
    public void Clone_clears_uploaded_image_path()
    {
        var original = new FlashcardSet
        {
            Title = "Set",
            Flashcards =
            [
                new Flashcard { FrontText = "a", BackText = "b", UploadedImagePath = "/uploads/x.png" }
            ]
        };

        var clone = original.Clone();

        Assert.All(clone.Flashcards, c => Assert.Null(c.UploadedImagePath));
    }

    [Fact]
    public void Clone_modifying_clone_does_not_change_source_star_state()
    {
        var original = new FlashcardSet
        {
            Title = "Set",
            Flashcards =
            [
                new Flashcard { FrontText = "a", BackText = "b", IsStarred = true }
            ]
        };

        var clone = original.Clone();
        clone.Flashcards.First().IsStarred = true;

        Assert.True(original.Flashcards.First().IsStarred);
    }
}
