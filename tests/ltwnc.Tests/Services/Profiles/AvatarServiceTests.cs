using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ltwnc.Tests.Services.Profiles;

public class AvatarServiceTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"ltwnc-avatar-{Guid.NewGuid():N}");

    [Fact]
    public async Task ReplaceAvatar_ValidCroppedPng_SavesRandomFileAndUpdatesProfile()
    {
        await using var db = CreateContext();
        db.UserProfiles.Add(new UserProfile { UserId = "user-1" });
        await db.SaveChangesAsync();
        AvatarService service = CreateService(db);
        IFormFile file = await CreatePngAsync(32, 32);

        AvatarUploadResult result = await service.ReplaceAvatarAsync("user-1", file);

        Assert.True(result.Succeeded);
        Assert.StartsWith("/uploads/avatars/", result.AvatarPath);
        Assert.True(File.Exists(ToPhysicalPath(result.AvatarPath!)));
        Assert.Equal(result.AvatarPath, db.UserProfiles.Single().AvatarPath);
    }

    [Fact]
    public async Task ReplaceAvatar_FileOverFiveMegabytes_ReturnsVietnameseError()
    {
        await using var db = CreateContext();
        db.UserProfiles.Add(new UserProfile { UserId = "user-1" });
        await db.SaveChangesAsync();
        AvatarService service = CreateService(db);
        var content = new byte[5 * 1024 * 1024 + 1];
        var file = new FormFile(new MemoryStream(content), 0, content.Length, "avatar", "large.png");

        AvatarUploadResult result = await service.ReplaceAvatarAsync("user-1", file);

        Assert.False(result.Succeeded);
        Assert.Contains("5 MB", result.Error);
    }

    [Fact]
    public async Task ReplaceAvatar_SpoofedImageContent_ReturnsVietnameseError()
    {
        await using var db = CreateContext();
        db.UserProfiles.Add(new UserProfile { UserId = "user-1" });
        await db.SaveChangesAsync();
        AvatarService service = CreateService(db);
        byte[] content = "not an image"u8.ToArray();
        var file = new FormFile(new MemoryStream(content), 0, content.Length, "avatar", "fake.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        AvatarUploadResult result = await service.ReplaceAvatarAsync("user-1", file);

        Assert.False(result.Succeeded);
        Assert.Equal("File ảnh không hợp lệ.", result.Error);
    }

    [Fact]
    public async Task ReplaceAvatar_ExcessiveDimensions_ReturnsVietnameseError()
    {
        await using var db = CreateContext();
        db.UserProfiles.Add(new UserProfile { UserId = "user-1" });
        await db.SaveChangesAsync();
        AvatarService service = CreateService(db);
        IFormFile file = await CreatePngAsync(5000, 1);

        AvatarUploadResult result = await service.ReplaceAvatarAsync("user-1", file);

        Assert.False(result.Succeeded);
        Assert.Contains("4096", result.Error);
    }

    [Fact]
    public async Task ReplaceAvatar_DatabaseFailure_DeletesNewFileAndKeepsOldAvatar()
    {
        await using var db = CreateContext();
        const string oldPath = "/uploads/avatars/old.png";
        Directory.CreateDirectory(Path.GetDirectoryName(ToPhysicalPath(oldPath))!);
        await File.WriteAllBytesAsync(ToPhysicalPath(oldPath), [1, 2, 3]);
        db.UserProfiles.Add(new UserProfile { UserId = "user-1", AvatarPath = oldPath });
        await db.SaveChangesAsync();
        db.FailSaves = true;
        AvatarService service = CreateService(db);
        IFormFile file = await CreatePngAsync(32, 32);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReplaceAvatarAsync("user-1", file));

        Assert.True(File.Exists(ToPhysicalPath(oldPath)));
        Assert.Single(Directory.GetFiles(Path.Combine(_webRoot, "uploads", "avatars")));
    }

    private FailingAppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FailingAppDbContext(options);
    }

    private AvatarService CreateService(AppDbContext db)
    {
        Directory.CreateDirectory(_webRoot);
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.WebRootPath).Returns(_webRoot);
        return new AvatarService(db, environment.Object, TimeProvider.System);
    }

    private static async Task<IFormFile> CreatePngAsync(int width, int height)
    {
        var stream = new MemoryStream();
        using (var image = new Image<Rgba32>(width, height))
        {
            await image.SaveAsPngAsync(stream);
        }

        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "avatar", "avatar.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }

    private string ToPhysicalPath(string webPath) =>
        Path.Combine(_webRoot, webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }

    private sealed class FailingAppDbContext(DbContextOptions<AppDbContext> options)
        : AppDbContext(options)
    {
        public bool FailSaves { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return FailSaves
                ? throw new InvalidOperationException("database failure")
                : base.SaveChangesAsync(cancellationToken);
        }
    }
}
