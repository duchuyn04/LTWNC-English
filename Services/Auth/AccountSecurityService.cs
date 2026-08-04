using System.ComponentModel.DataAnnotations;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.Enums;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Auth;

public sealed record RegistrationStartResult(
    bool Succeeded,
    string? ChallengeId = null,
    string? ErrorMessage = null);

public sealed record PasswordResetStartResult(
    bool Succeeded,
    string ChallengeId);

public sealed record AccountSecurityResult(
    bool Succeeded,
    string? UserId = null,
    string? ErrorMessage = null);

public sealed class AccountSecurityService : IAccountSecurityService
{
    private readonly AppDbContext _db;
    private readonly IAuthService _authService;
    private readonly IEmailOtpService _otpService;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public AccountSecurityService(
        AppDbContext db,
        IAuthService authService,
        IEmailOtpService otpService,
        IPasswordHasher<AppUser> passwordHasher,
        TimeProvider timeProvider)
    {
        _db = db;
        _authService = authService;
        _otpService = otpService;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<RegistrationStartResult> StartLocalRegistrationAsync(
        string email,
        string userName,
        string password,
        string? requestIpAddress,
        CancellationToken cancellationToken = default)
    {
        email = email.Trim();
        userName = userName.Trim();
        string normalizedEmail = email.ToUpperInvariant();
        string normalizedUserName = userName.ToUpperInvariant();

        if (!new EmailAddressAttribute().IsValid(email))
        {
            return new RegistrationStartResult(false, ErrorMessage: "Email không hợp lệ.");
        }

        AuthError? passwordError = PasswordPolicy.GetValidationError(password);
        if (passwordError != null)
        {
            return new RegistrationStartResult(false, ErrorMessage: passwordError.Message);
        }

        string? usernameError = UsernamePolicy.GetValidationError(userName);
        if (usernameError != null)
        {
            return new RegistrationStartResult(false, ErrorMessage: usernameError);
        }

        if (await _db.AppUsers.AnyAsync(
                user => user.NormalizedEmail == normalizedEmail,
                cancellationToken))
        {
            return new RegistrationStartResult(false, ErrorMessage: "Email đã được sử dụng.");
        }

        if (await _db.AppUsers.AnyAsync(
                user => user.NormalizedUserName == normalizedUserName,
                cancellationToken))
        {
            return new RegistrationStartResult(false, ErrorMessage: "Tên đăng nhập đã được sử dụng.");
        }

        PendingRegistration? pendingByEmail = await _db.PendingRegistrations
            .SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);
        PendingRegistration? pendingByUserName = await _db.PendingRegistrations
            .SingleOrDefaultAsync(item => item.NormalizedUserName == normalizedUserName, cancellationToken);

        if (pendingByEmail != null && pendingByUserName != null &&
            pendingByEmail.Id != pendingByUserName.Id)
        {
            return new RegistrationStartResult(false, ErrorMessage: "Email hoặc tên đăng nhập đang chờ xác thực.");
        }

        PendingRegistration pending = pendingByEmail ?? pendingByUserName ?? new PendingRegistration
        {
            CreatedAtUtc = _timeProvider.GetUtcNow()
        };
        pending.Email = email;
        pending.NormalizedEmail = normalizedEmail;
        pending.UserName = userName;
        pending.NormalizedUserName = normalizedUserName;
        pending.PasswordHash = _passwordHasher.HashPassword(
            new AppUser { Id = pending.Id, Email = email },
            password);
        pending.UpdatedAtUtc = _timeProvider.GetUtcNow();

        if (pendingByEmail == null && pendingByUserName == null)
        {
            _db.PendingRegistrations.Add(pending);
        }

        await _db.SaveChangesAsync(cancellationToken);
        OtpSendResult sent = await _otpService.SendAsync(
            EmailOtpPurpose.Registration,
            pending.Email,
            pendingRegistrationId: pending.Id,
            requestIpAddress: requestIpAddress,
            cancellationToken: cancellationToken);
        return sent.Succeeded
            ? new RegistrationStartResult(true, sent.ChallengeId)
            : new RegistrationStartResult(false, ErrorMessage: sent.ErrorMessage);
    }

    public async Task<RegistrationStartResult> ResendOtpAsync(
        string challengeId,
        string? requestIpAddress,
        CancellationToken cancellationToken = default)
    {
        OtpSendResult sent = await _otpService.ResendAsync(
            challengeId,
            requestIpAddress,
            cancellationToken);
        return sent.Succeeded
            ? new RegistrationStartResult(true, sent.ChallengeId)
            : new RegistrationStartResult(false, ErrorMessage: sent.ErrorMessage);
    }

    public async Task<AccountSecurityResult> VerifyRegistrationAsync(
        string challengeId,
        string code,
        CancellationToken cancellationToken = default)
    {
        OtpValidationResult validation = await _otpService.ValidateAsync(
            challengeId,
            code,
            EmailOtpPurpose.Registration,
            cancellationToken);
        if (!validation.Succeeded || validation.Challenge?.PendingRegistrationId == null)
        {
            return Failure(validation.ErrorMessage);
        }

        PendingRegistration? pending = await _db.PendingRegistrations
            .SingleOrDefaultAsync(
                item => item.Id == validation.Challenge.PendingRegistrationId,
                cancellationToken);
        if (pending == null)
        {
            return Failure("Thông tin đăng ký không còn tồn tại.");
        }

        AuthResult result = await _authService.CreateVerifiedLocalUserAsync(
            pending.Email,
            pending.UserName,
            pending.PasswordHash,
            cancellationToken);
        if (!result.Succeeded)
        {
            return Failure(result.Errors.FirstOrDefault()?.Message);
        }

        _db.PendingRegistrations.Remove(pending);
        await _db.SaveChangesAsync(cancellationToken);
        AppUser? user = await _authService.FindByEmailAsync(pending.Email, cancellationToken);
        return user == null
            ? Failure("Không thể hoàn tất đăng ký.")
            : new AccountSecurityResult(true, user.Id);
    }

    public async Task<PasswordResetStartResult> StartPasswordResetAsync(
        string email,
        string? requestIpAddress,
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail = email.Trim().ToUpperInvariant();
        AppUser? user = await _db.AppUsers
            .SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);
        string fallbackChallengeId = Guid.NewGuid().ToString();

        if (user == null || user.IsAdmin)
        {
            return new PasswordResetStartResult(true, fallbackChallengeId);
        }

        OtpSendResult sent = await _otpService.SendAsync(
            EmailOtpPurpose.PasswordReset,
            user.Email,
            userId: user.Id,
            requestIpAddress: requestIpAddress,
            cancellationToken: cancellationToken);
        return new PasswordResetStartResult(true, sent.ChallengeId ?? fallbackChallengeId);
    }

    public async Task<AccountSecurityResult> CompletePasswordResetAsync(
        string challengeId,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        AuthError? passwordError = PasswordPolicy.GetValidationError(newPassword);
        if (passwordError != null)
        {
            return Failure(passwordError.Message);
        }

        OtpValidationResult validation = await _otpService.ValidateAsync(
            challengeId,
            code,
            EmailOtpPurpose.PasswordReset,
            cancellationToken);
        if (!validation.Succeeded || validation.Challenge?.UserId == null)
        {
            return Failure(validation.ErrorMessage);
        }

        AppUser? user = await _db.AppUsers
            .SingleOrDefaultAsync(item => item.Id == validation.Challenge.UserId, cancellationToken);
        if (user == null || user.IsAdmin)
        {
            return Failure("Mã OTP không hợp lệ hoặc đã hết hạn.");
        }

        AuthResult result = await _authService.ResetPasswordAsync(
            user,
            newPassword,
            cancellationToken);
        return result.Succeeded
            ? new AccountSecurityResult(true, user.Id)
            : Failure(result.Errors.FirstOrDefault()?.Message);
    }

    public async Task<RegistrationStartResult> StartGoogleLinkOtpAsync(
        string userId,
        string googleSubjectId,
        string? requestIpAddress,
        CancellationToken cancellationToken = default)
    {
        AppUser? user = await _authService.FindByIdAsync(userId, cancellationToken);
        if (user == null || user.IsAdmin || string.IsNullOrWhiteSpace(googleSubjectId))
        {
            return new RegistrationStartResult(false, ErrorMessage: "Không thể liên kết tài khoản Google.");
        }

        OtpSendResult sent = await _otpService.SendAsync(
            EmailOtpPurpose.GoogleLink,
            user.Email,
            userId: user.Id,
            googleSubjectId: googleSubjectId,
            requestIpAddress: requestIpAddress,
            cancellationToken: cancellationToken);
        return sent.Succeeded
            ? new RegistrationStartResult(true, sent.ChallengeId)
            : new RegistrationStartResult(false, ErrorMessage: sent.ErrorMessage);
    }

    public async Task<AccountSecurityResult> CompleteGoogleLinkOtpAsync(
        string challengeId,
        string code,
        CancellationToken cancellationToken = default)
    {
        OtpValidationResult validation = await _otpService.ValidateAsync(
            challengeId,
            code,
            EmailOtpPurpose.GoogleLink,
            cancellationToken);
        if (!validation.Succeeded ||
            validation.Challenge?.UserId == null ||
            string.IsNullOrWhiteSpace(validation.Challenge.GoogleSubjectId))
        {
            return Failure(validation.ErrorMessage);
        }

        AppUser? user = await _authService.FindByIdAsync(
            validation.Challenge.UserId,
            cancellationToken);
        if (user == null || user.IsAdmin)
        {
            return Failure("Không thể liên kết tài khoản Google.");
        }

        AuthResult result = await _authService.LinkGoogleAsync(
            user,
            validation.Challenge.GoogleSubjectId,
            cancellationToken);
        return result.Succeeded
            ? new AccountSecurityResult(true, user.Id)
            : Failure(result.Errors.FirstOrDefault()?.Message);
    }

    private static AccountSecurityResult Failure(string? message)
    {
        return new AccountSecurityResult(
            false,
            ErrorMessage: message ?? "Yêu cầu không hợp lệ.");
    }
}

public interface IAccountSecurityService
{
    Task<RegistrationStartResult> StartLocalRegistrationAsync(
        string email,
        string userName,
        string password,
        string? requestIpAddress,
        CancellationToken cancellationToken = default);

    Task<RegistrationStartResult> ResendOtpAsync(
        string challengeId,
        string? requestIpAddress,
        CancellationToken cancellationToken = default);

    Task<AccountSecurityResult> VerifyRegistrationAsync(
        string challengeId,
        string code,
        CancellationToken cancellationToken = default);

    Task<PasswordResetStartResult> StartPasswordResetAsync(
        string email,
        string? requestIpAddress,
        CancellationToken cancellationToken = default);

    Task<AccountSecurityResult> CompletePasswordResetAsync(
        string challengeId,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<RegistrationStartResult> StartGoogleLinkOtpAsync(
        string userId,
        string googleSubjectId,
        string? requestIpAddress,
        CancellationToken cancellationToken = default);

    Task<AccountSecurityResult> CompleteGoogleLinkOtpAsync(
        string challengeId,
        string code,
        CancellationToken cancellationToken = default);
}
