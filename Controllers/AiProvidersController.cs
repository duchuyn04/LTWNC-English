using ltwnc.Areas.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.AiProviders;
using ltwnc.Services.Ai;

namespace ltwnc.Controllers;

[Authorize(Policy = AdminAreaPolicy.Name)]
[Route("Admin/AiProviders")]
public sealed class AiProvidersController : Controller
{
    private readonly IAiProviderService _service;

    public AiProvidersController(IAiProviderService service)
    {
        _service = service;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _service.GetAllAsync(cancellationToken));

    [HttpGet("Create")]
    public IActionResult Create() => View("Edit", new AiProviderEditViewModel());

    [HttpGet("{id:int}/Edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        AiProvider? provider = await _service.GetAsync(id, cancellationToken);
        if (provider == null) return NotFound();
        return View(new AiProviderEditViewModel
        {
            Id = provider.Id,
            Name = provider.Name,
            AdapterType = provider.AdapterType,
            BaseUrl = provider.BaseUrl,
            ModelId = provider.ModelId,
            HasApiKey = !string.IsNullOrWhiteSpace(provider.EncryptedApiKey),
            ApiKeyLastFour = provider.ApiKeyLastFour,
            IsEnabled = provider.IsEnabled,
            Priority = provider.Priority,
            TimeoutSeconds = provider.TimeoutSeconds
        });
    }

    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AdminAreaPolicy.RecentAuthenticationName)]
    public async Task<IActionResult> Save(AiProviderEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Edit", model);
        try
        {
            AiProvider provider = await _service.SaveAsync(model.Id, new AiProviderInput
            {
                Name = model.Name,
                AdapterType = model.AdapterType,
                BaseUrl = model.BaseUrl,
                ModelId = model.ModelId,
                ApiKey = model.ApiKey,
                ClearApiKey = model.ClearApiKey,
                IsEnabled = model.IsEnabled,
                Priority = model.Priority,
                TimeoutSeconds = model.TimeoutSeconds
            }, cancellationToken);
            TempData["ProviderMessage"] = $"Đã lưu provider {provider.Name}.";
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Edit", model);
        }
    }

    [HttpPost("{id:int}/Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AdminAreaPolicy.RecentAuthenticationName)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/Models")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Models(int id, CancellationToken cancellationToken)
    {
        try { return Json(new { success = true, models = await _service.DiscoverModelsAsync(id, cancellationToken) }); }
        catch (Exception exception) when (exception is AiProviderUnavailableException or AiProviderConfigurationException or ArgumentException)
        { return BadRequest(new { success = false, error = exception.Message }); }
    }

    [HttpPost("{id:int}/Test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(int id, CancellationToken cancellationToken)
    {
        try { await _service.TestAsync(id, cancellationToken); return Json(new { success = true }); }
        catch (Exception exception) when (exception is AiProviderUnavailableException or AiProviderConfigurationException)
        { return BadRequest(new { success = false, error = exception.Message }); }
    }
}
