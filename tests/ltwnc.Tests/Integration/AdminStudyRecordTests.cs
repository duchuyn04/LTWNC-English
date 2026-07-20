using System.Net;
using System.Text.RegularExpressions;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class AdminStudyRecordTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    // Reset đồng hồ dùng chung để trạng thái phiên (đang học/bỏ dở) ổn định giữa các test.
    public AdminStudyRecordTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Clock.Reset();
    }

    // Khách chưa đăng nhập bị chuyển đến trang đăng nhập khi mở hồ sơ học tập.
    [Fact]
    public async Task Index_Guest_IsRedirectedToLogin()
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync("/Admin/Learning");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    // Ngườ học đã đăng nhập nhưng không phải Admin nhận 403 thay vì dữ liệu học tập.
    [Fact]
    public async Task Index_Learner_IsForbidden()
    {
        const string learnerEmail = "learner-learning-forbidden@example.com";
        await _factory.SeedUserAsync("learner_learning_forbidden", learnerEmail);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SignInAsync(client, learnerEmail);

        HttpResponseMessage response = await client.GetAsync("/Admin/Learning");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Danh sách phiên học lọc theo ngườ dùng, chế độ và trạng thái phía máy chủ.
    [Fact]
    public async Task Index_FiltersByUserModeAndStatus()
    {
        const string adminEmail = "admin-learning-filter@example.com";
        const string learnerEmail = "learner-learning-filter@example.com";
        await _factory.SeedUserAsync("admin_learning_filter", adminEmail, isAdmin: true, twoFactorEnabled: true);
        string learnerId = await SeedLearnerAsync("learner_learning_filter", learnerEmail);

        int setId = await SeedFlashcardSetAsync(learnerId, "Bộ từ vựng lọc");
        // Phiên Quiz đã hoàn thành: phải xuất hiện khi lọc mode=Quiz&status=completed.
        await SeedSessionAsync(learnerId, setId, StudyMode.Quiz, TimeSpan.FromDays(2), completed: true, score: 8);
        // Phiên Dictation bỏ dở (bắt đầu 2 ngày trước, chưa hoàn thành): không được lọt bộ lọc.
        await SeedSessionAsync(learnerId, setId, StudyMode.Dictation, TimeSpan.FromDays(2), completed: false);

        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync(
            $"/Admin/Learning?search={Uri.EscapeDataString(learnerEmail)}&mode=Quiz&status=completed");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hồ sơ học tập", html);
        Assert.Contains(learnerEmail, html);
        // Chỉ đúng một phiên Quiz đã hoàn thành của learner này lọt bộ lọc.
        Assert.Equal(1, CountSessionRows(html));
    }

    // Danh sách mặc định 25 dòng mỗi trang và kẹp tối đa 100 dòng kể cả khi client yêu cầu nhiều hơn.
    [Fact]
    public async Task Index_PaginatesWithDefault25AndClampsPageSizeTo100()
    {
        const string adminEmail = "admin-learning-paging@example.com";
        const string learnerEmail = "learner-learning-paging@example.com";
        await _factory.SeedUserAsync("admin_learning_paging", adminEmail, isAdmin: true, twoFactorEnabled: true);
        string learnerId = await SeedLearnerAsync("learner_learning_paging", learnerEmail);
        int setId = await SeedFlashcardSetAsync(learnerId, "Bộ từ vựng phân trang");

        // Tạo 30 phiên để trang mặc định chỉ chứa 25 và phải có trang 2.
        for (int index = 0; index < 30; index++)
        {
            await SeedSessionAsync(
                learnerId,
                setId,
                StudyMode.Flashcard,
                TimeSpan.FromHours(index + 1),
                completed: true);
        }

        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, adminEmail);

        // Lọc theo email learner của test này vì các test trong cùng class dùng chung database.
        string filter = $"search={Uri.EscapeDataString(learnerEmail)}";
        HttpResponseMessage firstPage = await client.GetAsync($"/Admin/Learning?{filter}");
        string firstHtml = WebUtility.HtmlDecode(await firstPage.Content.ReadAsStringAsync());

        HttpResponseMessage clampedPage = await client.GetAsync($"/Admin/Learning?{filter}&pageSize=500");
        string clampedHtml = WebUtility.HtmlDecode(await clampedPage.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        Assert.Equal(25, CountSessionRows(firstHtml));
        Assert.Contains("trang 1/2", firstHtml, StringComparison.OrdinalIgnoreCase);
        // pageSize=500 bị kẹp về 100 nên cả 30 phiên đều nằm trong trang đầu.
        Assert.Equal(30, CountSessionRows(clampedHtml));
    }

    // Mở chi tiết không có lý do chỉ hiển thị cổng nhập lý do, không lộ dữ liệu và không tạo audit.
    [Fact]
    public async Task Details_WithoutReason_ShowsReasonGateWithoutDataOrAudit()
    {
        const string adminEmail = "admin-learning-gate@example.com";
        const string learnerEmail = "learner-learning-gate@example.com";
        await _factory.SeedUserAsync("admin_learning_gate", adminEmail, isAdmin: true, twoFactorEnabled: true);
        string learnerId = await SeedLearnerAsync("learner_learning_gate", learnerEmail);

        int setId = await SeedFlashcardSetAsync(learnerId, "Bộ từ vựng cổng lý do");
        int sessionId = await SeedSessionAsync(
            learnerId,
            setId,
            StudyMode.Quiz,
            TimeSpan.FromHours(3),
            completed: true,
            score: 9);

        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync($"/Admin/Learning/{sessionId}");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("lý do", html, StringComparison.OrdinalIgnoreCase);
        // Trang cổng không được hiển thị dữ liệu phiên như điểm số.
        Assert.DoesNotContain("Điểm", html);
        Assert.False(await AuditExistsAsync(sessionId));
    }

    // Mở chi tiết cấp ngườ học với lý do hợp lệ: audit thành công trước rồi mới trả dữ liệu phiên.
    [Fact]
    public async Task Details_WithReason_RecordsAuditThenReturnsSessionData()
    {
        const string adminEmail = "admin-learning-detail@example.com";
        const string learnerEmail = "learner-learning-detail@example.com";
        await _factory.SeedUserAsync("admin_learning_detail", adminEmail, isAdmin: true, twoFactorEnabled: true);
        string learnerId = await SeedLearnerAsync("learner_learning_detail", learnerEmail);

        int setId = await SeedFlashcardSetAsync(learnerId, "Bộ từ vựng chi tiết");
        int sessionId = await SeedSessionAsync(
            learnerId,
            setId,
            StudyMode.Dictation,
            TimeSpan.FromHours(5),
            completed: true,
            score: 2);
        await SeedDictationDetailAsync(sessionId, setId, "apple", "aple", isCorrect: false);
        await SeedDictationDetailAsync(sessionId, setId, "banana", "banana", isCorrect: true);

        const string reason = "Hỗ trợ vụ việc #123: học viên báo mất điểm.";
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync(
            $"/Admin/Learning/{sessionId}?reason={Uri.EscapeDataString(reason)}");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nghe chép chính tả", html);
        Assert.Contains(learnerEmail, html);
        Assert.Contains("aple", html);
        Assert.Contains("banana", html);
        Assert.True(await AuditExistsAsync(sessionId, reason));
    }

    // Hồ sơ học tập chỉ đọc: không có endpoint sửa điểm, sửa tiến độ hoặc xóa phiên học.
    [Fact]
    public async Task Details_MutationEndpoints_DoNotExist()
    {
        const string adminEmail = "admin-learning-readonly@example.com";
        const string learnerEmail = "learner-learning-readonly@example.com";
        await _factory.SeedUserAsync("admin_learning_readonly", adminEmail, isAdmin: true, twoFactorEnabled: true);
        string learnerId = await SeedLearnerAsync("learner_learning_readonly", learnerEmail);

        int setId = await SeedFlashcardSetAsync(learnerId, "Bộ từ vựng chỉ đọc");
        int sessionId = await SeedSessionAsync(
            learnerId,
            setId,
            StudyMode.Quiz,
            TimeSpan.FromHours(1),
            completed: true,
            score: 5);

        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, adminEmail);

        using var emptyForm = new FormUrlEncodedContent(new Dictionary<string, string>());
        HttpResponseMessage postResponse = await client.PostAsync(
            $"/Admin/Learning/{sessionId}", emptyForm);
        HttpResponseMessage deleteResponse = await client.PostAsync(
            $"/Admin/Learning/{sessionId}/Delete", emptyForm);

        // POST vào route chi tiết chỉ có GET nên bị từ chối bằng 405;
        // route xóa không có handler ghi nào nên bị từ chối (404 hoặc 405 tùy route khớp).
        Assert.Equal(HttpStatusCode.MethodNotAllowed, postResponse.StatusCode);
        Assert.Contains(
            deleteResponse.StatusCode,
            new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        // Dữ liệu phiên không bị thay đổi sau các nỗ lực ghi.
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        StudySession session = await context.StudySessions.SingleAsync(item => item.Id == sessionId);
        Assert.Equal(5, session.Score);
    }

    // Thờ gian hiển thị theo múi giờ Việt Nam với ngày giờ đầy đủ.
    [Fact]
    public async Task Details_DisplaysFullVietnamTime()
    {
        const string adminEmail = "admin-learning-timezone@example.com";
        const string learnerEmail = "learner-learning-timezone@example.com";
        await _factory.SeedUserAsync("admin_learning_timezone", adminEmail, isAdmin: true, twoFactorEnabled: true);
        string learnerId = await SeedLearnerAsync("learner_learning_timezone", learnerEmail);

        int setId = await SeedFlashcardSetAsync(learnerId, "Bộ từ vựng múi giờ");
        // 2026-07-18 17:30 UTC = 2026-07-19 00:30 giờ Việt Nam.
        DateTime startedAtUtc = new(2026, 7, 18, 17, 30, 0, DateTimeKind.Utc);
        int sessionId = await SeedSessionAsync(
            learnerId,
            setId,
            StudyMode.Quiz,
            startedAtUtc,
            completed: true,
            score: 7);

        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync(
            $"/Admin/Learning/{sessionId}?reason={Uri.EscapeDataString("Kiểm tra hiển thị múi giờ.")}");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("00:30", html);
        Assert.Contains("19/07/2026", html);
    }

    // Đếm số hàng phiên trong bảng qua thuộc tính đánh dấu dành riêng cho kiểm thử.
    private static int CountSessionRows(string html)
    {
        return Regex.Matches(html, "data-session-row").Count;
    }

    // Kiểm tra audit truy cập nhạy cảm đã được ghi cho đúng phiên học và lý do.
    private async Task<bool> AuditExistsAsync(int sessionId, string? reason = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.AdminAuditLogs.AnyAsync(log =>
            log.Action == AdminAuditActions.StudyRecordsViewDetails
            && log.TargetId == sessionId.ToString()
            && (reason == null || log.Reason == reason));
    }

    // Tạo tài khoản ngườ học và trả về Id để gắn vào phiên học.
    private async Task<string> SeedLearnerAsync(string userName, string email)
    {
        await _factory.SeedUserAsync(userName, email);
        return await _factory.GetUserIdAsync(email);
    }

    // Tạo bộ flashcard tối thiểu để phiên học có khóa ngoại hợp lệ.
    private async Task<int> SeedFlashcardSetAsync(string ownerId, string title)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var set = new FlashcardSet
        {
            UserId = ownerId,
            Title = title,
            Description = "Bộ thẻ dữ liệu thử nghiệm.",
            IsPublic = false
        };
        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();
        return set.Id;
    }

    // Tạo phiên học với mốc bắt đầu lùi về quá khứ so với đồng hồ thử nghiệm.
    private async Task<int> SeedSessionAsync(
        string userId,
        int setId,
        StudyMode mode,
        TimeSpan startedAgo,
        bool completed,
        int? score = null)
    {
        DateTime startedAtUtc = _factory.Clock.GetUtcNow().UtcDateTime - startedAgo;
        return await SeedSessionAsync(userId, setId, mode, startedAtUtc, completed, score);
    }

    // Tạo phiên học với mốc bắt đầu tuyệt đối để kiểm tra hiển thị múi giờ ổn định.
    private async Task<int> SeedSessionAsync(
        string userId,
        int setId,
        StudyMode mode,
        DateTime startedAtUtc,
        bool completed,
        int? score = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = mode,
            Score = score,
            PlannedItemCount = 10,
            StartedAt = startedAtUtc
        };
        if (completed)
        {
            session.CompletedAt = startedAtUtc.AddMinutes(10);
            session.DurationSeconds = 600;
        }

        context.StudySessions.Add(session);
        await context.SaveChangesAsync();
        return session.Id;
    }

    // Tạo một câu trả lờ nghe chép chính tả gắn với phiên và thẻ thật.
    private async Task SeedDictationDetailAsync(
        int sessionId,
        int setId,
        string frontText,
        string answeredText,
        bool isCorrect)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var card = new Flashcard
        {
            FlashcardSetId = setId,
            FrontText = frontText,
            BackText = $"nghĩa của {frontText}",
            Pronunciation = "/test/",
            PartOfSpeech = "noun",
            ExampleSentence = $"Câu ví dụ với {frontText}.",
            ExampleMeaning = "Nghĩa câu ví dụ."
        };
        context.Flashcards.Add(card);
        await context.SaveChangesAsync();

        context.DictationSessionDetails.Add(new DictationSessionDetail
        {
            StudySessionId = sessionId,
            FlashcardId = card.Id,
            IsCorrect = isCorrect,
            AnsweredText = answeredText,
            CreatedAt = _factory.Clock.GetUtcNow().UtcDateTime
        });
        await context.SaveChangesAsync();
    }

    // Tạo HttpClient không tự follow redirect để test đúng contract HTTP.
    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}
