# Home Index Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign `/` into a service-style hybrid landing page for LTWNC English without adding backend workflows.

**Architecture:** Keep `HomeController` and `HomeViewModel` unchanged. Replace the home Razor body with static landing sections plus the existing `Model.PublicSets` area, and move page-specific styling into a new `wwwroot/css/home.css` loaded only by `Views/Home/Index.cshtml`.

**Tech Stack:** ASP.NET Core MVC, Razor, Bootstrap grid, Phosphor icons, vanilla CSS/JS, existing AOS script.

---

## File Structure

- Modify `Views/Home/Index.cshtml`: full landing page markup and small JS for carousel/CTA form.
- Create `wwwroot/css/home.css`: all home-page-specific styles.
- Do not modify `Controllers/HomeController.cs`.
- Do not modify `Models/ViewModels/Home/HomeViewModel.cs`.
- Do not add quote-form backend routes or database tables.

## Task 1: Add Home Page CSS

**Files:**
- Create: `wwwroot/css/home.css`

- [ ] **Step 1: Create the CSS file**

Create `wwwroot/css/home.css` with these foundations:

```css
.home-landing {
    --home-ink: #111111;
    --home-muted: #6f6b63;
    --home-line: #e6e0d6;
    --home-paper: #fffdf8;
    --home-canvas: #f7f3ec;
    --home-accent: #1f6c9f;
    --home-accent-soft: #e6f3fb;
    --home-green: #2f6a37;
    color: var(--home-ink);
    background: var(--home-canvas);
}

.home-landing section {
    padding: clamp(3rem, 7vw, 6rem) 0;
}

.home-kicker {
    color: var(--home-accent);
    font-size: 0.78rem;
    font-weight: 800;
    letter-spacing: 0.08em;
    text-transform: uppercase;
}

.home-section-title {
    margin: 0.35rem 0 0.8rem;
    font-size: clamp(1.8rem, 4vw, 3.4rem);
    font-weight: 800;
    letter-spacing: -0.03em;
}

.home-section-copy {
    color: var(--home-muted);
    font-size: 1rem;
    line-height: 1.7;
}

.home-card {
    height: 100%;
    border: 1px solid var(--home-line);
    border-radius: 16px;
    background: rgba(255, 253, 248, 0.86);
    padding: 1.35rem;
    box-shadow: 0 16px 44px rgba(78, 67, 48, 0.07);
}

.home-icon {
    width: 44px;
    height: 44px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    border-radius: 12px;
    background: var(--home-accent-soft);
    color: var(--home-accent);
    font-size: 1.35rem;
}

.home-btn-row {
    display: flex;
    flex-wrap: wrap;
    gap: 0.8rem;
}

.home-topbar,
.home-nav,
.home-quote-strip,
.home-hero-card,
.home-logo-pill,
.home-quote-form {
    border: 1px solid var(--home-line);
    background: rgba(255, 253, 248, 0.88);
}

.home-topbar {
    padding: 0.65rem 0;
    font-size: 0.85rem;
    color: var(--home-muted);
}

.home-nav {
    position: sticky;
    top: 0;
    z-index: 20;
    backdrop-filter: blur(14px);
    border-left: 0;
    border-right: 0;
}

.home-nav-inner {
    min-height: 72px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
}

.home-nav-links {
    display: flex;
    gap: 1rem;
}

.home-nav-links a,
.home-footer a {
    color: var(--home-muted);
    text-decoration: none;
    font-weight: 700;
}

.home-quote-strip {
    margin-top: 1rem;
    border-radius: 16px;
    padding: 1rem;
}

.home-hero {
    padding-top: clamp(2rem, 5vw, 4rem);
}

.home-hero h1 {
    font-size: clamp(2.4rem, 6vw, 5rem);
    font-weight: 850;
    line-height: 0.98;
    letter-spacing: -0.05em;
}

.home-hero-card {
    border-radius: 28px;
    padding: 1rem;
    box-shadow: 0 24px 70px rgba(78, 67, 48, 0.12);
}

.home-flashcard-preview {
    min-height: 360px;
    border-radius: 22px;
    background: #111827;
    color: #ffffff;
    display: grid;
    place-items: center;
    text-align: center;
    padding: 2rem;
}

.home-stats {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 0.9rem;
    margin-top: 1.4rem;
}

.home-stat {
    border-top: 1px solid var(--home-line);
    padding-top: 1rem;
}

.home-stat strong {
    display: block;
    font-size: 1.6rem;
    font-weight: 850;
}

.home-grid-4 {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 1rem;
}

.home-grid-3 {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 1rem;
}

.home-feature-block {
    border-radius: 28px;
    background: #111827;
    color: #ffffff;
    padding: clamp(1.5rem, 4vw, 3rem);
}

.home-logo-row {
    display: flex;
    flex-wrap: wrap;
    gap: 0.8rem;
}

.home-logo-pill {
    border-radius: 999px;
    padding: 0.7rem 1rem;
    font-weight: 800;
}

.home-checklist {
    display: grid;
    gap: 0.85rem;
}

.home-check {
    display: flex;
    gap: 0.7rem;
    color: var(--home-muted);
}

.home-testimonials {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 1rem;
}

.home-quote-form {
    border-radius: 24px;
    padding: clamp(1.2rem, 3vw, 2rem);
}

.home-quote-form input,
.home-quote-form select {
    width: 100%;
    min-height: 46px;
    border: 1px solid var(--home-line);
    border-radius: 12px;
    background: #ffffff;
    padding: 0 0.9rem;
}

.home-footer {
    padding: 3rem 0;
    background: #ffffff;
    border-top: 1px solid var(--home-line);
}

@media (max-width: 992px) {
    .home-nav-inner,
    .home-nav-links {
        align-items: flex-start;
        flex-direction: column;
    }

    .home-grid-4,
    .home-grid-3,
    .home-testimonials {
        grid-template-columns: 1fr 1fr;
    }
}

@media (max-width: 640px) {
    .home-grid-4,
    .home-grid-3,
    .home-testimonials,
    .home-stats {
        grid-template-columns: 1fr;
    }
}
```

- [ ] **Step 2: Commit CSS foundation**

Run:

```powershell
git add wwwroot/css/home.css
git commit -m "feat: add home landing styles"
```

Expected: one commit with only `wwwroot/css/home.css`.

## Task 2: Replace Home Markup With The Hybrid Landing Layout

**Files:**
- Modify: `Views/Home/Index.cshtml`

- [ ] **Step 1: Replace the page with this Razor skeleton**

Replace `Views/Home/Index.cshtml` with:

```razor
@model HomeViewModel
@{
    ViewData["Title"] = "Trang chủ";
    var primaryCta = User.Identity?.IsAuthenticated == true ? "/Set/Create" : "/Account/Register";
    var secondaryCta = User.Identity?.IsAuthenticated == true ? "/Set" : "#featured-sets";
}

<link rel="stylesheet" href="~/css/home.css" asp-append-version="true" />

@section FullWidth {
<div class="home-landing">
    <div class="home-topbar">
        <div class="container d-flex justify-content-between gap-3 flex-wrap">
            <span><i class="ph ph-sparkle"></i> Học từ vựng bằng flashcard, giọng đọc và tiến độ cá nhân.</span>
            <span>Miễn phí bắt đầu · Tạo bộ thẻ trong vài phút</span>
        </div>
    </div>

    <nav class="home-nav">
        <div class="container home-nav-inner">
            <a class="navbar-brand fw-bold" href="/">
                <i class="ph ph-book-open"></i> LTWNC English
            </a>
            <div class="home-nav-links">
                <a href="#benefits">Lợi ích</a>
                <a href="#services">Tính năng</a>
                <a href="#onboarding">Cách bắt đầu</a>
                <a href="#quote">Tư vấn học</a>
            </div>
            <a class="btn-primary-custom" href="@primaryCta">
                <i class="ph ph-arrow-right"></i> Bắt đầu
            </a>
        </div>
    </nav>

    <div class="container">
        <div class="home-quote-strip d-flex align-items-center justify-content-between gap-3 flex-wrap">
            <div>
                <strong>Muốn có lộ trình học từ vựng rõ ràng?</strong>
                <span class="ms-2 text-muted">Chọn mục tiêu, tạo bộ thẻ, bắt đầu ôn ngay.</span>
            </div>
            <a class="btn-secondary-custom" href="@primaryCta">Bắt đầu lộ trình</a>
        </div>
    </div>

    <section class="home-hero">
        <div class="container">
            <div class="row align-items-center g-5">
                <div class="col-lg-6" data-aos="fade-right">
                    <div class="home-kicker">Flashcard learning service</div>
                    <h1>Học từ vựng tiếng Anh có hệ thống hơn.</h1>
                    <p class="home-section-copy mt-3">
                        LTWNC English giúp bạn tạo bộ thẻ, luyện phát âm, đánh dấu từ quan trọng
                        và theo dõi tiến độ học trong một giao diện gọn.
                    </p>
                    <div class="home-btn-row mt-4">
                        <a class="btn-primary-custom btn-lg" href="@primaryCta">
                            <i class="ph ph-rocket-launch"></i> Tạo bộ thẻ
                        </a>
                        <a class="btn-secondary-custom btn-lg" href="@secondaryCta">
                            <i class="ph ph-compass"></i> Khám phá
                        </a>
                    </div>
                    <div class="home-stats">
                        <div class="home-stat"><strong>3 phút</strong><span>Tạo bộ thẻ đầu tiên</span></div>
                        <div class="home-stat"><strong>5 chế độ</strong><span>Học và ôn tập</span></div>
                        <div class="home-stat"><strong>100%</strong><span>Theo dõi tiến độ</span></div>
                    </div>
                </div>
                <div class="col-lg-6" data-aos="fade-left">
                    <div class="home-hero-card">
                        <div class="home-flashcard-preview">
                            <div>
                                <span class="badge bg-light text-dark mb-3">ENGLISH</span>
                                <h2 class="display-4 fw-bold">consistency</h2>
                                <p class="mb-0">/kənˈsɪstənsi/ · sự đều đặn</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </section>

    <section id="benefits">
        <div class="container">
            <div class="text-center mb-4">
                <div class="home-kicker">Benefits</div>
                <h2 class="home-section-title">Tập trung vào cách bạn ghi nhớ.</h2>
            </div>
            <div class="home-grid-4">
                <div class="home-card"><span class="home-icon"><i class="ph ph-cards"></i></span><h3 class="h5 mt-3">Flashcard rõ ràng</h3><p class="home-section-copy">Mỗi từ có nghĩa, IPA, ví dụ và trạng thái học.</p></div>
                <div class="home-card"><span class="home-icon"><i class="ph ph-speaker-high"></i></span><h3 class="h5 mt-3">Giọng đọc</h3><p class="home-section-copy">Nghe phát âm tiếng Anh ngay khi học thẻ.</p></div>
                <div class="home-card"><span class="home-icon"><i class="ph ph-star"></i></span><h3 class="h5 mt-3">Đánh dấu sao</h3><p class="home-section-copy">Tách nhóm từ quan trọng để ôn nhanh.</p></div>
                <div class="home-card"><span class="home-icon"><i class="ph ph-chart-line-up"></i></span><h3 class="h5 mt-3">Tiến độ học</h3><p class="home-section-copy">Biết từ nào chưa học, đang học, đã thành thạo.</p></div>
            </div>
        </div>
    </section>

    <section id="services">
        <div class="container">
            <div class="row g-4 align-items-end mb-4">
                <div class="col-lg-7">
                    <div class="home-kicker">Services</div>
                    <h2 class="home-section-title">Dịch vụ học nằm ngay trong app.</h2>
                </div>
                <div class="col-lg-5"><p class="home-section-copy">Không thêm quy trình phức tạp: tạo bộ thẻ, học, xem tiến độ.</p></div>
            </div>
            <div class="home-grid-3">
                <div class="home-card"><span class="home-icon"><i class="ph ph-plus-circle"></i></span><h3 class="h5 mt-3">Tạo bộ thẻ</h3><p class="home-section-copy">Nhập thuật ngữ, định nghĩa, IPA, ví dụ và ảnh minh họa.</p></div>
                <div class="home-card"><span class="home-icon"><i class="ph ph-brain"></i></span><h3 class="h5 mt-3">Học flashcard</h3><p class="home-section-copy">Lật thẻ, nghe voice, trộn thẻ, đánh dấu đã biết/chưa biết.</p></div>
                <div class="home-card"><span class="home-icon"><i class="ph ph-stack"></i></span><h3 class="h5 mt-3">Theo dõi tiến độ</h3><p class="home-section-copy">Sắp xếp theo trạng thái học để biết nên ôn gì trước.</p></div>
            </div>
        </div>
    </section>

    <section>
        <div class="container">
            <div class="home-feature-block">
                <div class="row g-4 align-items-center">
                    <div class="col-lg-6">
                        <div class="home-kicker">Product</div>
                        <h2 class="home-section-title text-white">Một workspace học từ vựng gọn.</h2>
                        <p class="text-white-50">Từ tạo bộ thẻ đến học flashcard đều nằm trong cùng một luồng.</p>
                    </div>
                    <div class="col-lg-6">
                        <div class="home-card text-dark">
                            <strong>debugging</strong>
                            <p class="mb-2 text-muted">/ˌdiːˈbʌɡɪŋ/ · gỡ lỗi</p>
                            <small>Debugging helped identify the cause of the system crash.</small>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </section>

    <section>
        <div class="container">
            <div class="home-logo-row justify-content-center">
                <span class="home-logo-pill">Flashcards</span>
                <span class="home-logo-pill">Voice</span>
                <span class="home-logo-pill">Progress</span>
                <span class="home-logo-pill">Images</span>
                <span class="home-logo-pill">Public sets</span>
            </div>
        </div>
    </section>

    <section>
        <div class="container">
            <div class="row g-5 align-items-center">
                <div class="col-lg-6">
                    <div class="home-kicker">Why choose us</div>
                    <h2 class="home-section-title">Ít thao tác, nhiều thời gian học hơn.</h2>
                </div>
                <div class="col-lg-6 home-checklist">
                    <div class="home-check"><i class="ph ph-check-circle"></i><span>Không cần cài thêm công cụ học rời rạc.</span></div>
                    <div class="home-check"><i class="ph ph-check-circle"></i><span>Dữ liệu bộ thẻ nằm trong tài khoản của bạn.</span></div>
                    <div class="home-check"><i class="ph ph-check-circle"></i><span>Giao diện học tối giản, tập trung vào từ vựng.</span></div>
                    <div class="home-check"><i class="ph ph-check-circle"></i><span>Có thể học từ bộ thẻ cộng đồng.</span></div>
                </div>
            </div>
        </div>
    </section>

    <section id="onboarding">
        <div class="container">
            <div class="text-center mb-4">
                <div class="home-kicker">Onboarding</div>
                <h2 class="home-section-title">Bắt đầu trong 3 bước.</h2>
            </div>
            <div class="home-grid-3">
                <div class="home-card"><strong>01</strong><h3 class="h5 mt-3">Đăng ký</h3><p class="home-section-copy">Tạo tài khoản để lưu bộ thẻ và tiến độ.</p></div>
                <div class="home-card"><strong>02</strong><h3 class="h5 mt-3">Tạo hoặc chọn bộ thẻ</h3><p class="home-section-copy">Tự nhập từ hoặc học từ bộ thẻ công khai.</p></div>
                <div class="home-card"><strong>03</strong><h3 class="h5 mt-3">Ôn mỗi ngày</h3><p class="home-section-copy">Dùng flashcard, voice và trạng thái học để ôn đúng trọng tâm.</p></div>
            </div>
        </div>
    </section>

    <section>
        <div class="container">
            <div class="home-testimonials">
                <div class="home-card"><p>“Flashcard rõ, dễ ôn lại từ chưa thuộc.”</p><strong>Minh Anh</strong><small class="d-block text-muted">Sinh viên</small></div>
                <div class="home-card"><p>“Tôi thích phần đánh sao và nghe phát âm.”</p><strong>Quang Huy</strong><small class="d-block text-muted">Người tự học</small></div>
                <div class="home-card"><p>“Tạo bộ thẻ cho lớp rất nhanh.”</p><strong>Lan Phương</strong><small class="d-block text-muted">Gia sư</small></div>
            </div>
        </div>
    </section>

    <section>
        <div class="container">
            <div class="row g-4">
                <div class="col-lg-4"><div class="home-card"><div class="home-kicker">Blog</div><h3 class="h5">Cách nhớ từ theo ngữ cảnh</h3><p class="home-section-copy">Viết ví dụ riêng cho mỗi từ để nhớ lâu hơn.</p></div></div>
                <div class="col-lg-4"><div class="home-card"><div class="home-kicker">Guide</div><h3 class="h5">Khi nào nên đánh dấu sao?</h3><p class="home-section-copy">Dùng sao cho từ khó, từ hay sai và từ cần ôn trước kỳ kiểm tra.</p></div></div>
                <div class="col-lg-4"><div class="home-card"><div class="home-kicker">Tips</div><h3 class="h5">Nghe phát âm khi lật thẻ</h3><p class="home-section-copy">Kết hợp nhìn, nghe và ví dụ để học sâu hơn.</p></div></div>
            </div>
        </div>
    </section>

    <section>
        <div class="container">
            <div class="home-grid-3">
                <div class="home-card"><span class="home-icon"><i class="ph ph-user-focus"></i></span><h3 class="h5 mt-3">Learning design</h3><p class="home-section-copy">Luồng học ngắn, dễ lặp lại.</p></div>
                <div class="home-card"><span class="home-icon"><i class="ph ph-code"></i></span><h3 class="h5 mt-3">Product build</h3><p class="home-section-copy">Tính năng tập trung vào flashcard thật.</p></div>
                <div class="home-card"><span class="home-icon"><i class="ph ph-seal-check"></i></span><h3 class="h5 mt-3">Quality</h3><p class="home-section-copy">Không claim chứng nhận giả, chỉ cam kết rõ ràng.</p></div>
            </div>
        </div>
    </section>

    <section id="featured-sets">
        <div class="container">
            <div class="d-flex justify-content-between align-items-end gap-3 flex-wrap mb-4">
                <div>
                    <div class="home-kicker">Public sets</div>
                    <h2 class="home-section-title">Bộ thẻ công khai</h2>
                </div>
                @if (!string.IsNullOrWhiteSpace(Model.SearchQuery))
                {
                    <span class="text-muted">Kết quả cho “@Model.SearchQuery”</span>
                }
            </div>

            @if (!Model.PublicSets.Any())
            {
                <div class="home-card text-center">
                    <i class="ph ph-book-open" style="font-size: 2rem;"></i>
                    <p class="mb-0 mt-2">Chưa có bộ thẻ công khai phù hợp.</p>
                </div>
            }
            else
            {
                <div class="home-grid-3">
                    @foreach (var set in Model.PublicSets.Take(6))
                    {
                        <div class="home-card">
                            <div class="d-flex justify-content-between gap-3">
                                <h3 class="h5">@set.Title</h3>
                                <span class="tag tag-new">Công khai</span>
                            </div>
                            @if (!string.IsNullOrWhiteSpace(set.Description))
                            {
                                <p class="home-section-copy">@set.Description</p>
                            }
                            <a href="/Study/@set.Id" class="btn-primary-custom">
                                <i class="ph ph-play-circle"></i> Học ngay
                            </a>
                        </div>
                    }
                </div>
            }
        </div>
    </section>

    <section id="quote">
        <div class="container">
            <div class="home-quote-form">
                <div class="row g-4 align-items-end">
                    <div class="col-lg-5">
                        <div class="home-kicker">Quote</div>
                        <h2 class="home-section-title">Bắt đầu lộ trình học.</h2>
                    </div>
                    <div class="col-lg-7">
                        <form id="home-quote-form" class="row g-3">
                            <div class="col-md-6"><input type="text" placeholder="Tên của bạn" aria-label="Tên của bạn"></div>
                            <div class="col-md-6"><select aria-label="Mục tiêu học"><option>Giao tiếp</option><option>Thi cử</option><option>Công việc</option></select></div>
                            <div class="col-12"><button type="submit" class="btn-primary-custom btn-lg">Tiếp tục</button></div>
                        </form>
                    </div>
                </div>
            </div>
        </div>
    </section>

    <footer class="home-footer">
        <div class="container">
            <div class="row g-4">
                <div class="col-lg-4"><strong>LTWNC English</strong><p class="home-section-copy mt-2">Học tiếng Anh cùng flashcard.</p></div>
                <div class="col-6 col-lg-2"><strong>Sản phẩm</strong><a class="d-block mt-2" href="#services">Tính năng</a><a class="d-block mt-2" href="#featured-sets">Bộ thẻ</a></div>
                <div class="col-6 col-lg-2"><strong>Học tập</strong><a class="d-block mt-2" href="#benefits">Lợi ích</a><a class="d-block mt-2" href="#onboarding">Bắt đầu</a></div>
                <div class="col-6 col-lg-2"><strong>Tài khoản</strong><a class="d-block mt-2" href="/Account/Login">Đăng nhập</a><a class="d-block mt-2" href="/Account/Register">Đăng ký</a></div>
                <div class="col-6 col-lg-2"><strong>CTA</strong><a class="d-block mt-2" href="@primaryCta">Tạo bộ thẻ</a></div>
            </div>
        </div>
    </footer>
</div>
}

@section Scripts {
<script>
    AOS.init({ duration: 700, once: true, offset: 80 });

    document.querySelectorAll('.home-landing a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            const target = document.querySelector(this.getAttribute('href'));
            if (!target) return;
            e.preventDefault();
            target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
    });

    document.getElementById('home-quote-form')?.addEventListener('submit', function(e) {
        e.preventDefault();
        window.location.href = '@primaryCta';
    });
</script>
}
```

- [ ] **Step 2: Build**

Run:

```powershell
dotnet build --no-restore /p:UseAppHost=false /p:OutputPath=C:\it\ltwnc\.build-review\
```

Expected:

```text
Build succeeded.
0 Error(s)
```

- [ ] **Step 3: Commit markup**

Run:

```powershell
git add Views/Home/Index.cshtml
git commit -m "feat: redesign home landing page"
```

Expected: one commit with only `Views/Home/Index.cshtml`.

## Task 3: Verify Home Page Behavior

**Files:**
- Verify: `Views/Home/Index.cshtml`
- Verify: `wwwroot/css/home.css`

- [ ] **Step 1: Check route and CTA strings**

Run:

```powershell
Select-String -Path Views\Home\Index.cshtml -Pattern '/Set/Create|/Account/Register|home-quote-form|featured-sets'
```

Expected:
- `primaryCta` uses `/Set/Create` for authenticated users.
- `primaryCta` uses `/Account/Register` for anonymous users.
- `home-quote-form` submit exists.
- `featured-sets` section exists.

- [ ] **Step 2: Check no backend route was added**

Run:

```powershell
git diff --name-only HEAD~2..HEAD
```

Expected files only:

```text
Views/Home/Index.cshtml
wwwroot/css/home.css
```

- [ ] **Step 3: Run app for browser check**

Run:

```powershell
dotnet run
```

Open `/`.

Expected:
- Header has two tiers plus quote strip.
- Hero is split into text and app preview.
- Sections are present: benefits, services, feature block, logos, checklist, onboarding, testimonials, blog, team/quality, public sets, quote, footer.
- Quote form submit navigates to registration when logged out.
- No text overlaps at mobile width.

- [ ] **Step 4: Clean build artifact**

If `.build-review/` exists from the build step, remove it:

```powershell
Remove-Item -LiteralPath .build-review -Recurse -Force
git -c core.excludesfile= status --short
```

Expected: only `.codegraph/` may remain untracked.
