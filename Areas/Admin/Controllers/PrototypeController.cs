using ltwnc.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

// PROTOTYPE ONLY — route này sẽ bị xóa sau khi chọn được hướng UI production.
[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("Admin/Prototype/Dashboard")]
public sealed class PrototypeController : Controller
{
    private static readonly HashSet<string> AllowedVariants = new(StringComparer.OrdinalIgnoreCase)
    {
        "A", "B", "C"
    };

    private readonly IWebHostEnvironment _environment;

    public PrototypeController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Dashboard(string? variant)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        string selectedVariant = AllowedVariants.Contains(variant ?? string.Empty)
            ? variant!.ToUpperInvariant()
            : "A";

        return View(new AdminDashboardPrototypeViewModel
        {
            Variant = selectedVariant,
            GeneratedAt = DateTime.Now,
            Metrics =
            [
                new("Người dùng", "12.480", "+8,4% tháng này", "ph-users-three", "green"),
                new("Bộ thẻ", "3.216", "+184 trong 30 ngày", "ph-stack", "blue"),
                new("Phiên học", "48.902", "+12,7% tháng này", "ph-graduation-cap", "amber"),
                new("AI Missions", "6.840", "93,2% hoàn tất", "ph-chats-circle", "violet")
            ],
            Activities =
            [
                new("2 phút", "Provider AI phản hồi chậm", "Gemini Gateway · 4,8 giây", "ph-warning", "warning"),
                new("8 phút", "Tài khoản mới", "minh.nguyen vừa hoàn tất đăng ký", "ph-user-plus", "success"),
                new("16 phút", "Bộ thẻ được báo cáo", "Business English B2 · 3 báo cáo", "ph-flag", "danger"),
                new("31 phút", "English Mission hoàn tất", "Airport Check-in · điểm 92", "ph-check-circle", "success"),
                new("52 phút", "Achievement được mở", "30 Day Streak · 18 người dùng", "ph-medal", "info")
            ],
            PopularSets =
            [
                new("IELTS Academic 3000", "thao.linh", 342, 2840, "78%"),
                new("Business English B2", "anh.khoa", 186, 2314, "72%"),
                new("Daily Conversation", "mai.chi", 124, 1988, "84%"),
                new("TOEIC Listening", "quang.huy", 260, 1642, "69%")
            ],
            Health =
            [
                new("AI Providers", "3 / 4 online", "Một provider phản hồi chậm", "warning"),
                new("Tỷ lệ lỗi", "0,18%", "Trong 24 giờ gần nhất", "success"),
                new("Hàng đợi kiểm duyệt", "7 mục", "2 mục ưu tiên cao", "danger"),
                new("Database", "32 ms", "Truy vấn trung bình", "success")
            ]
        });
    }
}
