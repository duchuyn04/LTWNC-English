using ltwnc.Models.Entities;

namespace ltwnc.Tests;

// Kiểm tra phương thức Clone của FlashcardSet
public class FlashcardSetCloneTests
{
    // Bản sao phải tách biệt và reset thông tin chủ sở hữu / nguồn sao chép
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
        Assert.Null(clone.SourceSetId);
        // Bản clone không mang chính sách công khai của nguồn
        Assert.False(clone.IsPublic);
    }

    // Bản clone luôn private; service sẽ gán lại ownership và lineage sau
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Clone_resets_IsPublic_to_false(bool isPublic)
    {
        var original = new FlashcardSet
        {
            Title = "Set",
            IsPublic = isPublic
        };

        var clone = original.Clone();

        Assert.False(clone.IsPublic);
    }

    // Tiêu đề và mô tả phải được bảo toàn
    [Fact]
    // Clone giữ tiêu đề và mô tả
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

    // Thờigian tạo/cập nhật phải được reset về hiện tại
    [Fact]
    // Clone cập nhật CreatedAt/UpdatedAt về thờidiạm hiện tại
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

    // Clone phải deep-copy danh sách thẻ, không chia sẻ reference
    [Fact]
    // Clone sao chép sâu danh sách thẻ, không dùng chung reference
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

    // Bộ thẻ rỗng vẫn clone được bình thường
    [Fact]
    // Clone bộ thẻ rỗng vẫn cho danh sách thẻ rỗng
    public void Clone_empty_flashcards_collection()
    {
        var original = new FlashcardSet { Title = "Empty set" };

        var clone = original.Clone();

        Assert.Empty(clone.Flashcards);
    }

    // Trạng thái đánh sao của chủ bộ nguồn không được mang sang bản sao
    [Fact]
    // Clone reset trạng thái đánh sao của các thẻ
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

    // Ảnh upload nội bộ không được duplicate
    [Fact]
    // Clone xóa đường dẫn ảnh upload nội bộ để tránh trùng file
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

    // Thay đổi trên bản sao không được ảnh hưởng đến bản gốc
    [Fact]
    // Thay đổi trên bản sao không ảnh hưởng đến bộ thẻ gốc
    public void Clone_modifying_clone_does_not_change_source_star_state()
    {
        var original = new FlashcardSet
        {
            Title = "Set",
            Flashcards =
            [
                new Flashcard { FrontText = "a", BackText = "b", IsStarred = false }
            ]
        };

        var clone = original.Clone();
        clone.Flashcards.First().IsStarred = true;

        Assert.False(original.Flashcards.First().IsStarred);
        Assert.True(clone.Flashcards.First().IsStarred);
    }
}
