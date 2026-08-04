using System.Globalization;
using System.Security.Cryptography;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Auth;

public interface IOtpCodeGenerator
{
    string Generate();
}

public sealed class OtpCodeGenerator : IOtpCodeGenerator
{
    public string Generate()
    {
        return RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);
    }
}

public sealed record OtpSendResult(
    bool Succeeded,
    string? ChallengeId = null,
    string? ErrorMessage = null);

public sealed record OtpValidationResult(
    bool Succeeded,
    EmailOtpChallenge? Challenge = null,
    string? ErrorMessage = null);

public sealed class EmailOtpService : IEmailOtpService
{
    public const int MaxFailedAttempts = 3;
    public const int MaxSendsPerHour = 5;
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

    private readonly AppDbContext _db;
    private readonly IEmailMessageSender _emailSender;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly IOtpCodeGenerator _codeGenerator;
    private readonly TimeProvider _timeProvider;

    public EmailOtpService(
        AppDbContext db,
        IEmailMessageSender emailSender,
        IPasswordHasher<AppUser> passwordHasher,
        IOtpCodeGenerator codeGenerator,
        TimeProvider timeProvider)
    {
        _db = db;
        _emailSender = emailSender;
        _passwordHasher = passwordHasher;
        _codeGenerator = codeGenerator;
        _timeProvider = timeProvider;
    }

    public async Task<OtpSendResult> SendAsync(
        EmailOtpPurpose purpose,
        string email,
        string? userId = null,
        string? pendingRegistrationId = null,
        string? googleSubjectId = null,
        string? requestIpAddress = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail = email.Trim().ToUpperInvariant();
        DateTimeOffset now = _timeProvider.GetUtcNow();

        EmailOtpChallenge? latest = await _db.EmailOtpChallenges
            .Where(challenge => challenge.NormalizedEmail == normalizedEmail)
            .OrderByDescending(challenge => challenge.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest != null && now - latest.CreatedAtUtc < ResendCooldown)
        {
            return new OtpSendResult(false, ErrorMessage: "Vui lòng chờ 1 phút trước khi gửi mã mới.");
        }

        DateTimeOffset hourAgo = now.Subtract(TimeSpan.FromHours(1));
        int emailSendCount = await _db.EmailOtpChallenges.CountAsync(
            challenge => challenge.NormalizedEmail == normalizedEmail &&
                challenge.CreatedAtUtc >= hourAgo,
            cancellationToken);
        if (emailSendCount >= MaxSendsPerHour)
        {
            return new OtpSendResult(false, ErrorMessage: "Email đã yêu cầu quá nhiều mã. Vui lòng thử lại sau.");
        }

        if (!string.IsNullOrWhiteSpace(requestIpAddress))
        {
            int ipSendCount = await _db.EmailOtpChallenges.CountAsync(
                challenge => challenge.RequestIpAddress == requestIpAddress &&
                    challenge.CreatedAtUtc >= hourAgo,
                cancellationToken);
            if (ipSendCount >= MaxSendsPerHour)
            {
                return new OtpSendResult(false, ErrorMessage: "Bạn đã yêu cầu quá nhiều mã. Vui lòng thử lại sau.");
            }
        }

        List<EmailOtpChallenge> activeChallenges = await _db.EmailOtpChallenges
            .Where(challenge => challenge.NormalizedEmail == normalizedEmail &&
                challenge.UsedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (EmailOtpChallenge challenge in activeChallenges)
        {
            challenge.UsedAtUtc = now;
        }

        string code = _codeGenerator.Generate();
        var challengeToSave = new EmailOtpChallenge
        {
            Purpose = purpose,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            UserId = userId,
            PendingRegistrationId = pendingRegistrationId,
            GoogleSubjectId = googleSubjectId,
            CodeHash = _passwordHasher.HashPassword(
                new AppUser { Id = Guid.NewGuid().ToString() },
                code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(CodeLifetime),
            RequestIpAddress = requestIpAddress
        };
        _db.EmailOtpChallenges.Add(challengeToSave);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailSender.SendAsync(
                challengeToSave.Email,
                BuildSubject(purpose),
                BuildBody(purpose, code),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new OtpSendResult(false, ErrorMessage: "Không thể gửi email lúc này. Vui lòng thử lại sau.");
        }

        return new OtpSendResult(true, challengeToSave.Id);
    }

    public async Task<OtpSendResult> ResendAsync(
        string challengeId,
        string? requestIpAddress,
        CancellationToken cancellationToken = default)
    {
        EmailOtpChallenge? previous = await _db.EmailOtpChallenges
            .SingleOrDefaultAsync(item => item.Id == challengeId, cancellationToken);
        if (previous == null)
        {
            return new OtpSendResult(false, ErrorMessage: "Yêu cầu OTP không hợp lệ.");
        }

        return await SendAsync(
            previous.Purpose,
            previous.Email,
            previous.UserId,
            previous.PendingRegistrationId,
            previous.GoogleSubjectId,
            requestIpAddress,
            cancellationToken);
    }

    public async Task<OtpValidationResult> ValidateAsync(
        string challengeId,
        string code,
        EmailOtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        EmailOtpChallenge? challenge = await _db.EmailOtpChallenges
            .SingleOrDefaultAsync(item => item.Id == challengeId, cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (challenge == null ||
            challenge.Purpose != purpose ||
            challenge.UsedAtUtc != null ||
            challenge.ExpiresAtUtc <= now)
        {
            return new OtpValidationResult(false, ErrorMessage: "Mã OTP không hợp lệ hoặc đã hết hạn.");
        }

        AppUser codeUser = new() { Id = challenge.Id, Email = challenge.Email };
        PasswordVerificationResult verification = _passwordHasher.VerifyHashedPassword(
            codeUser,
            challenge.CodeHash,
            code.Trim());
        if (verification == PasswordVerificationResult.Failed)
        {
            challenge.FailedAttempts++;
            if (challenge.FailedAttempts >= MaxFailedAttempts)
            {
                challenge.UsedAtUtc = now;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return new OtpValidationResult(false, ErrorMessage: challenge.UsedAtUtc != null
                ? "Mã OTP đã bị vô hiệu do nhập sai quá số lần cho phép."
                : "Mã OTP không đúng.");
        }

        challenge.UsedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
        return new OtpValidationResult(true, challenge);
    }

    private static string BuildSubject(EmailOtpPurpose purpose)
    {
        return purpose switch
        {
            EmailOtpPurpose.Registration => "Xác thực đăng ký LTWNC English",
            EmailOtpPurpose.PasswordReset => "Mã khôi phục mật khẩu LTWNC English",
            EmailOtpPurpose.GoogleLink => "Xác nhận liên kết Google LTWNC English",
            _ => "Mã xác thực LTWNC English"
        };
    }

    private static string BuildBody(EmailOtpPurpose purpose, string code)
    {
        string action = purpose switch
        {
            EmailOtpPurpose.Registration => "xác thực đăng ký",
            EmailOtpPurpose.PasswordReset => "khôi phục mật khẩu",
            EmailOtpPurpose.GoogleLink => "liên kết tài khoản Google",
            _ => "xác thực tài khoản"
        };

        return $"Mã OTP để {action} LTWNC English là: {code}\n\nMã có hiệu lực trong 5 phút và bị vô hiệu sau 3 lần nhập sai. Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email.";
    }
}

public interface IEmailOtpService
{
    Task<OtpSendResult> SendAsync(
        EmailOtpPurpose purpose,
        string email,
        string? userId = null,
        string? pendingRegistrationId = null,
        string? googleSubjectId = null,
        string? requestIpAddress = null,
        CancellationToken cancellationToken = default);

    Task<OtpSendResult> ResendAsync(
        string challengeId,
        string? requestIpAddress,
        CancellationToken cancellationToken = default);

    Task<OtpValidationResult> ValidateAsync(
        string challengeId,
        string code,
        EmailOtpPurpose purpose,
        CancellationToken cancellationToken = default);
}
