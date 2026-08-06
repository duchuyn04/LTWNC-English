using System.Security.Claims;
using ltwnc.Areas.Admin.Models;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using ltwnc.Services.Credits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AdminAreaPolicy.Name)]
[Route("Admin/Credits")]
public sealed class CreditsController : Controller
{
    private readonly IAdminCreditService _service;

    public CreditsController(IAdminCreditService service)
    {
        _service = service;
    }

    [HttpGet("Stats")]
    public async Task<IActionResult> Stats(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        return View(await _service.GetStatsAsync(from, to, cancellationToken));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        AdminCreditOverview overview = await _service.GetOverviewAsync(cancellationToken);
        return View(new AdminCreditIndexViewModel
        {
            Packages = overview.Packages.Select(ToPackageRow).ToArray(),
            Purchases = overview.RecentPurchases.Select(purchase => new AdminCreditPurchaseRowViewModel
            {
                Id = purchase.Id,
                InvoiceNumber = purchase.InvoiceNumber,
                UserDisplay = $"{purchase.UserName} ({purchase.Email})",
                PackageName = purchase.PackageName,
                PriceVnd = purchase.PriceVnd,
                Credits = purchase.Credits,
                Status = purchase.Status,
                CreatedAtDisplay = FormatTime(purchase.CreatedAtUtc),
                PaidAtDisplay = purchase.PaidAtUtc.HasValue ? FormatTime(purchase.PaidAtUtc.Value) : "-"
            }).ToArray()
        });
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View("Edit", new AdminCreditPackageEditViewModel());
    }

    [HttpGet("Packages/{id:int}/Edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        CreditPackage? package = await _service.GetPackageAsync(id, cancellationToken);
        if (package == null)
            return NotFound();

        return View(new AdminCreditPackageEditViewModel
        {
            Id = package.Id,
            Version = package.Version,
            Name = package.Name,
            Description = package.Description,
            PriceVnd = package.PriceVnd,
            Credits = package.Credits,
            DisplayOrder = package.DisplayOrder,
            IsActive = package.IsActive
        });
    }

    [HttpPost("Packages/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePackage(
        AdminCreditPackageEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("Edit", model);

        AdminCreditOperationResult result = await _service.SavePackageAsync(
            new AdminCreditPackageCommand(
                model.Id, model.Version, model.Name, model.Description, model.PriceVnd,
                model.Credits, model.DisplayOrder, model.IsActive, model.Reason, BuildActor()),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View("Edit", model);
        }

        TempData["AdminCreditsSuccess"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Packages/{id:int}/Archive")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Archive(
        int id,
        AdminCreditPackageLifecycleViewModel model,
        CancellationToken cancellationToken)
    {
        return SetArchived(id, true, model, cancellationToken);
    }

    [HttpPost("Packages/{id:int}/Unarchive")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Unarchive(
        int id,
        AdminCreditPackageLifecycleViewModel model,
        CancellationToken cancellationToken)
    {
        return SetArchived(id, false, model, cancellationToken);
    }

    [HttpGet("Users")]
    public async Task<IActionResult> UserCredits(
        string? search,
        CancellationToken cancellationToken)
    {
        AdminCreditUser? user = string.IsNullOrWhiteSpace(search)
            ? null
            : await _service.FindUserAsync(search, cancellationToken);
        return View("User", ToUserViewModel(search, user));
    }

    [HttpPost("Users/Adjust")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(
        AdminCreditAdjustmentViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminCreditsError"] = FirstModelError();
            return RedirectToAction(nameof(UserCredits), new { search = model.Search });
        }

        int signedAmount = model.Operation == "Subtract" ? -model.Amount : model.Amount;
        AdminCreditOperationResult result = await _service.AdjustBalanceAsync(
            new AdminCreditAdjustmentCommand(
                model.UserId, model.CreditVersion, signedAmount, model.Reason, BuildActor()),
            cancellationToken);
        StoreMessage(result);
        return RedirectToAction(nameof(UserCredits), new { search = model.Search });
    }

    private async Task<IActionResult> SetArchived(
        int id,
        bool archive,
        AdminCreditPackageLifecycleViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminCreditsError"] = FirstModelError();
            return RedirectToAction(nameof(Index));
        }

        AdminCreditOperationResult result = await _service.SetPackageArchivedAsync(
            new AdminCreditPackageLifecycleCommand(
                id, model.Version, archive, model.Reason, BuildActor()),
            cancellationToken);
        StoreMessage(result);
        return RedirectToAction(nameof(Index));
    }

    private AdminActorContext BuildActor()
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        return new AdminActorContext(userId, User.Identity?.Name ?? userId, HttpContext.TraceIdentifier);
    }

    private void StoreMessage(AdminCreditOperationResult result)
    {
        TempData[result.Succeeded ? "AdminCreditsSuccess" : "AdminCreditsError"] = result.Message;
    }

    private string FirstModelError()
    {
        return ModelState.Values.SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
            ?? "Dữ liệu thao tác không hợp lệ.";
    }

    private static AdminCreditPackageRowViewModel ToPackageRow(CreditPackage package)
    {
        return new AdminCreditPackageRowViewModel
        {
            Id = package.Id,
            Version = package.Version,
            Name = package.Name,
            Description = package.Description,
            PriceVnd = package.PriceVnd,
            Credits = package.Credits,
            DisplayOrder = package.DisplayOrder,
            IsActive = package.IsActive,
            IsArchived = package.IsArchived
        };
    }

    private static AdminCreditUserViewModel ToUserViewModel(string? search, AdminCreditUser? user)
    {
        return new AdminCreditUserViewModel
        {
            Search = search?.Trim() ?? string.Empty,
            SearchAttempted = !string.IsNullOrWhiteSpace(search),
            UserId = user?.Id,
            UserName = user?.UserName,
            Email = user?.Email,
            Balance = user?.Balance ?? 0,
            CreditVersion = user?.CreditVersion ?? 0,
            Ledger = user?.RecentLedger.Select(entry => new AdminCreditLedgerRowViewModel
            {
                Id = entry.Id,
                Amount = entry.Amount,
                BalanceAfter = entry.BalanceAfter,
                Type = entry.Type,
                Description = entry.Description,
                CreatedAtDisplay = FormatTime(entry.CreatedAtUtc)
            }).ToArray() ?? Array.Empty<AdminCreditLedgerRowViewModel>()
        };
    }

    private static string FormatTime(DateTime value)
    {
        return AdminTimeZone.ToVietnamTime(value).ToString("HH:mm dd/MM/yyyy");
    }
}
