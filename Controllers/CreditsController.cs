using ltwnc.Models.Entities;
using ltwnc.Services.Auth;
using ltwnc.Services.Credits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ltwnc.Controllers;

[Authorize]
[Route("Credits")]
public sealed class CreditsController : Controller
{
    private readonly ICreditService _credits;
    private readonly ICurrentUser _currentUser;

    public CreditsController(ICreditService credits, ICurrentUser currentUser)
    {
        _credits = credits;
        _currentUser = currentUser;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (userId == null) return Challenge();
        return View(await _credits.GetAccountAsync(userId, cancellationToken));
    }

    [HttpPost("Buy/{packageId:int}")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("payments")]
    public async Task<IActionResult> Buy(int packageId, CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        try
        {
            return View("Checkout", await _credits.CreateCheckoutAsync(userId, packageId, cancellationToken));
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException)
        {
            TempData["CreditError"] = "Hệ thống thanh toán bằng VietQR hiện chưa khả dụng. Vui lòng thử lại sau hoặc liên hệ hỗ trợ.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("Payment/{purchaseId:int}")]
    public async Task<IActionResult> Payment(
        int purchaseId,
        string? result,
        CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (userId == null) return Challenge();
        CreditPurchase? purchase = await _credits.GetPurchaseAsync(userId, purchaseId, cancellationToken);
        if (purchase == null) return NotFound();
        ViewData["PaymentResult"] = result;
        return View(purchase);
    }

    [HttpGet("Payment/{purchaseId:int}/Status")]
    public async Task<IActionResult> PaymentStatus(int purchaseId, CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        CreditPurchase? purchase = await _credits.GetPurchaseAsync(userId, purchaseId, cancellationToken);
        return purchase == null
            ? NotFound()
            : Json(new { status = purchase.Status, paid = purchase.Status == CreditPurchaseStatuses.Paid });
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(64 * 1024)]
    [HttpPost("/api/payments/sepay/ipn")]
    public async Task<IActionResult> SePayIpn(
        [FromBody] SePayIpnPayload payload,
        CancellationToken cancellationToken)
    {
        if (!_credits.VerifyIpnSecret(Request.Headers["X-Secret-Key"].FirstOrDefault()))
            return Unauthorized(new { success = false });
        try
        {
            await _credits.HandleIpnAsync(payload, cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
        {
            return BadRequest(new { success = false, error = exception.Message });
        }
    }
}
