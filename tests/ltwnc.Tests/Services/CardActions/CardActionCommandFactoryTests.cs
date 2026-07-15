using ltwnc.Data;
using ltwnc.Services.CardActions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ltwnc.Tests;

// Kiểm tra factory tạo đúng command theo loại hành động
public class CardActionCommandFactoryTests
{
    private readonly CardActionCommandFactory _factory;

    public CardActionCommandFactoryTests()
    {
        // Mỗi test dùng database in-memory riêng để tránh ảnh hưởng lẫn nhau
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        _factory = new CardActionCommandFactory(context);
    }

    // Factory phải ánh xạ đúng action string sang command tương ứng
    [Theory]
    [InlineData("Delete", typeof(DeleteCardsCommand))]
    [InlineData("Star", typeof(StarCardsCommand))]
    [InlineData("Unstar", typeof(UnstarCardsCommand))]
    public void Create_returns_the_matching_command(string actionType, Type expectedType)
        => Assert.IsType(expectedType, _factory.Create(actionType, 1, "user", [1]));

    // Action không được hỗ trợ phải ném ra ngoại lệ rõ ràng
    [Fact]
    public void Create_throws_for_unknown_action_type()
        => Assert.Throws<InvalidOperationException>(() => _factory.Create("Unknown", 1, "user", [1]));
}
