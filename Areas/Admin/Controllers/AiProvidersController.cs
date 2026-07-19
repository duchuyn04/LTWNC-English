using System.Security.Claims;
using ltwnc.Areas.Admin;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.AiProviders;
using ltwnc.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AdminAreaPolicy.Name)]
[Route("Admin/AiProviders")]
public sealed class AiProvidersController : Controller
{
    private readonly IAiProviderService _service;

    // Nhận service nghiệp vụ; controller chỉ chịu trách nhiệm map HTTP, TempData và ViewModel.
    public AiProvidersController(IAiProviderService service)
    {
        _service = service;
    }

    // Hiển thị danh sách nhà cung cấp AI trong layout Admin.
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        IReadOnlyList<AiProvider> providers = await _service.GetAllAsync(cancellationToken);
        IReadOnlyList<AiProviderHealthSnapshot> healthSnapshots =
            await _service.GetHealthSnapshotsAsync(cancellationToken);
        return View(ToIndexViewModel(providers, healthSnapshots));
    }

    // Mở form tạo mới; khóa bí mật chỉ có ô nhập mới, không có giá trị cũ.
    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View("Edit", new AiProviderEditViewModel());
    }

    // Mở form chỉnh sửa với dữ liệu công khai và khóa phiên bản hiện tại.
    [HttpGet("{id:int}/Edit")]
    public async Task<IActionResult> Edit(
        int id,
        CancellationToken cancellationToken)
    {
        AiProvider? provider = await _service.GetAsync(id, cancellationToken);
        if (provider == null)
        {
            return NotFound();
        }

        return View(ToEditViewModel(provider));
    }

    // Lưu tạo mới hoặc cập nhật cấu hình quan trọng bằng POST, antiforgery, lý do và audit.
    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        AiProviderEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        AiProviderOperationResult result = await _service.SaveAsync(
            model.Id,
            ToInput(model),
            BuildActorContext(),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View("Edit", model);
        }

        TempData["ProviderMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // Bật lại nhà cung cấp đã vô hiệu hóa; yêu cầu lý do và khóa phiên bản để tránh ghi đè thao tác mới hơn.
    [HttpPost("{id:int}/Enable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(
        int id,
        AiProviderLifecycleActionViewModel model,
        CancellationToken cancellationToken)
    {
        AiProviderOperationResult result = await _service.SetEnabledAsync(
            id,
            enable: true,
            model.Version,
            model.Reason,
            BuildActorContext(),
            cancellationToken);

        StoreOperationMessage(result);
        return RedirectToAction(nameof(Index));
    }

    // Vô hiệu hóa thay cho xóa cứng để giữ nguyên lịch sử vận hành và audit.
    [HttpPost("{id:int}/Disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(
        int id,
        AiProviderLifecycleActionViewModel model,
        CancellationToken cancellationToken)
    {
        AiProviderOperationResult result = await _service.SetEnabledAsync(
            id,
            enable: false,
            model.Version,
            model.Reason,
            BuildActorContext(),
            cancellationToken);

        StoreOperationMessage(result);
        return RedirectToAction(nameof(Index));
    }

    // Giữ endpoint cũ để không có thao tác xóa cứng song song; form cũ nếu còn gọi vào đây cũng chỉ disable.
    [HttpPost("{id:int}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        int id,
        AiProviderLifecycleActionViewModel model,
        CancellationToken cancellationToken)
    {
        AiProviderOperationResult result = await _service.SetEnabledAsync(
            id,
            enable: false,
            model.Version,
            model.Reason,
            BuildActorContext(),
            cancellationToken);

        StoreOperationMessage(result);
        return RedirectToAction(nameof(Index));
    }

    // Chọn nhà cung cấp chính duy nhất, có antiforgery, lý do và audit.
    [HttpPost("{id:int}/Primary")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimary(
        int id,
        AiProviderLifecycleActionViewModel model,
        CancellationToken cancellationToken)
    {
        AiProviderOperationResult result = await _service.SetPrimaryAsync(
            id,
            model.Version,
            model.Reason,
            BuildActorContext(),
            cancellationToken);

        StoreOperationMessage(result);
        return RedirectToAction(nameof(Index));
    }

    // Khám phá danh sách model bằng endpoint bảo vệ sẵn: antiforgery và xử lý lỗi an toàn.
    [HttpPost("{id:int}/Models")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Models(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<string> models = await _service.DiscoverModelsAsync(id, cancellationToken);
            return Json(new { success = true, models });
        }
        catch (Exception exception) when (exception is AiProviderUnavailableException
            or AiProviderConfigurationException
            or ArgumentException)
        {
            return BadRequest(new { success = false, error = exception.Message });
        }
    }

    // Kiểm tra kết nối nhà cung cấp, không nhận hay trả khóa bí mật qua response.
    [HttpPost("{id:int}/Test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            await _service.TestAsync(id, cancellationToken);
            return Json(new { success = true });
        }
        catch (Exception exception) when (exception is AiProviderUnavailableException
            or AiProviderConfigurationException)
        {
            return BadRequest(new { success = false, error = exception.Message });
        }
    }

    // Chuyển danh sách entity và health snapshot sang view model chỉ đọc cho trang Admin.
    private static AiProviderIndexViewModel ToIndexViewModel(
        IReadOnlyList<AiProvider> providers,
        IReadOnlyList<AiProviderHealthSnapshot> healthSnapshots)
    {
        Dictionary<int, AiProviderHealthSnapshot> healthByProviderId = healthSnapshots
            .ToDictionary(snapshot => snapshot.ProviderId);
        List<AiProviderRowViewModel> rows = new();

        foreach (AiProvider provider in providers)
        {
            healthByProviderId.TryGetValue(provider.Id, out AiProviderHealthSnapshot? health);
            string apiKeyDisplay = "Không API key";
            if (!string.IsNullOrWhiteSpace(provider.ApiKeyLastFour))
            {
                apiKeyDisplay = "****" + provider.ApiKeyLastFour;
            }

            int healthSampleSize = 0;
            decimal? errorRatePercent = null;
            bool errorRateExceeded = false;
            bool isUnstable = false;
            if (health != null)
            {
                healthSampleSize = health.SampleSize;
                errorRatePercent = health.ErrorRatePercent;
                errorRateExceeded = health.ErrorRateExceeded;
                isUnstable = health.IsUnstable;
            }

            rows.Add(new AiProviderRowViewModel
            {
                Id = provider.Id,
                Version = provider.Version,
                Name = provider.Name,
                BaseUrl = provider.BaseUrl,
                ModelId = provider.ModelId,
                ApiKeyDisplay = apiKeyDisplay,
                IsEnabled = provider.IsEnabled,
                IsPrimary = provider.IsPrimary,
                Priority = provider.Priority,
                TimeoutSeconds = provider.TimeoutSeconds,
                LastCheckedAt = provider.LastCheckedAt,
                LastCheckSucceeded = provider.LastCheckSucceeded,
                LastError = provider.LastError,
                ConsecutiveFailureCount = provider.ConsecutiveFailureCount,
                HealthSampleSize = healthSampleSize,
                ErrorRatePercent = errorRatePercent,
                ErrorRateExceeded = errorRateExceeded,
                IsUnstable = isUnstable
            });
        }

        return new AiProviderIndexViewModel
        {
            Providers = rows
        };
    }

    // Chuyển entity sang form, chỉ đưa trạng thái khóa và bốn ký tự cuối ra giao diện.
    private static AiProviderEditViewModel ToEditViewModel(AiProvider provider)
    {
        return new AiProviderEditViewModel
        {
            Id = provider.Id,
            Version = provider.Version,
            Name = provider.Name,
            AdapterType = provider.AdapterType,
            BaseUrl = provider.BaseUrl,
            ModelId = provider.ModelId,
            HasApiKey = !string.IsNullOrWhiteSpace(provider.EncryptedApiKey),
            ApiKeyLastFour = provider.ApiKeyLastFour,
            IsEnabled = provider.IsEnabled,
            IsPrimary = provider.IsPrimary,
            Priority = provider.Priority,
            TimeoutSeconds = provider.TimeoutSeconds
        };
    }

    // Chuyển ViewModel sang input service để toàn bộ validation nghiệp vụ tập trung ở service.
    private static AiProviderInput ToInput(AiProviderEditViewModel model)
    {
        return new AiProviderInput
        {
            Name = model.Name,
            AdapterType = model.AdapterType,
            BaseUrl = model.BaseUrl,
            ModelId = model.ModelId,
            ApiKey = model.ApiKey,
            ClearApiKey = model.ClearApiKey,
            IsEnabled = model.IsEnabled,
            Priority = model.Priority,
            TimeoutSeconds = model.TimeoutSeconds,
            Reason = model.Reason,
            Version = model.Version
        };
    }

    // Dựng ngữ cảnh audit từ người dùng hiện tại và mã truy vết request.
    private AiProviderActorContext BuildActorContext()
    {
        string actorUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        string actorDisplay = User.Identity?.Name ?? actorUserId;

        return new AiProviderActorContext(
            actorUserId,
            actorDisplay,
            HttpContext.TraceIdentifier);
    }

    // Lưu thông báo qua TempData để hiển thị sau redirect.
    private void StoreOperationMessage(AiProviderOperationResult result)
    {
        if (result.Succeeded)
        {
            TempData["ProviderMessage"] = result.Message;
            return;
        }

        TempData["ProviderError"] = result.Message;
    }
}
