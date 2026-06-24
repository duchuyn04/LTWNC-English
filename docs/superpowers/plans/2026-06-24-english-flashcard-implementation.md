# English Flashcard Learning Website - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build MVP of community English flashcard learning website — auth, flashcard set CRUD, flashcard study mode.

**Architecture:** 3-layer ASP.NET MVC on .NET 10.0. Controller → Service → Repository → EF Core → SQL Server. ASP.NET Identity for auth.

**Tech Stack:** ASP.NET MVC, .NET 10.0, SQL Server, EF Core, ASP.NET Identity, Razor Views, jQuery, Bootstrap 5, Phosphor Icons.

## Global Constraints

- All views use Razor syntax
- UI follows minimalist light theme spec: background `#F7F6F3`, cards `#FFFFFF`, borders `#EAEAEA`, text `#111111`
- All POST forms include anti-forgery tokens
- Validation messages in Vietnamese
- Password: min 8 chars, 1 uppercase, 1 digit
- No unit tests in MVP — verify manually via browser

---

## File Structure

```
ltwnc/
├── Models/Entities/
│   ├── FlashcardSet.cs
│   ├── Flashcard.cs
│   ├── StudySession.cs
│   └── UserProgress.cs
├── Models/ViewModels/
│   ├── Account/RegisterViewModel.cs
│   ├── Account/LoginViewModel.cs
│   ├── FlashcardSet/CreateSetViewModel.cs
│   ├── FlashcardSet/EditSetViewModel.cs
│   ├── FlashcardSet/SetDetailViewModel.cs
│   ├── Study/FlashcardStudyViewModel.cs
│   └── Home/HomeViewModel.cs
├── Data/
│   └── AppDbContext.cs
├── Repositories/
│   ├── IFlashcardSetRepository.cs
│   ├── FlashcardSetRepository.cs
│   ├── IFlashcardRepository.cs
│   ├── FlashcardRepository.cs
│   ├── IStudySessionRepository.cs
│   └── StudySessionRepository.cs
├── Services/
│   ├── IAccountService.cs
│   ├── AccountService.cs
│   ├── IFlashcardSetService.cs
│   ├── FlashcardSetService.cs
│   ├── IStudyService.cs
│   └── StudyService.cs
├── Controllers/
│   ├── AccountController.cs
│   ├── HomeController.cs
│   ├── FlashcardSetController.cs
│   └── StudyController.cs
├── Views/
│   ├── Shared/_Layout.cshtml
│   ├── Shared/_Layout.cshtml.css
│   ├── Account/Register.cshtml
│   ├── Account/Login.cshtml
│   ├── Home/Index.cshtml
│   ├── FlashcardSet/Index.cshtml
│   ├── FlashcardSet/Create.cshtml
│   ├── FlashcardSet/Edit.cshtml
│   ├── FlashcardSet/Details.cshtml
│   ├── Study/Index.cshtml
│   ├── Study/Flashcard.cshtml
│   └── _ViewImports.cshtml
├── wwwroot/css/site.css
├── wwwroot/js/site.js
├── Data/
├── Program.cs
└── ltwnc.csproj
```

---

### Task 1: Install NuGet packages

**Files:**
- Modify: `ltwnc.csproj`

- [ ] **Step 1: Add Identity and EF Core packages**

Edit `ltwnc.csproj` — add inside `<ItemGroup>`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.0-*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0-*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0-*">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

- [ ] **Step 2: Restore packages**

Run: `dotnet restore`
Expected: No errors, packages restored.

- [ ] **Step 3: Verify build**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 2: Create Entity Models

**Files:**
- Create: `Models/Entities/FlashcardSet.cs`
- Create: `Models/Entities/Flashcard.cs`
- Create: `Models/Entities/StudySession.cs`
- Create: `Models/Entities/UserProgress.cs`

- [ ] **Step 1: Create FlashcardSet entity**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

public class FlashcardSet
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public bool IsPublic { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    public ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
}
```

- [ ] **Step 2: Create Flashcard entity**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

public class Flashcard
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int FlashcardSetId { get; set; }

    [Required]
    public string FrontText { get; set; } = string.Empty;

    [Required]
    public string BackText { get; set; } = string.Empty;

    public int OrderIndex { get; set; }

    // Navigation
    [ForeignKey(nameof(FlashcardSetId))]
    public FlashcardSet? FlashcardSet { get; set; }
}
```

- [ ] **Step 3: Create StudySession entity**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

public enum StudyMode
{
    Flashcard,
    Quiz,
    Write,
    Match
}

public class StudySession
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int FlashcardSetId { get; set; }

    public StudyMode Mode { get; set; } = StudyMode.Flashcard;

    public int? Score { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    [ForeignKey(nameof(FlashcardSetId))]
    public FlashcardSet? FlashcardSet { get; set; }
}
```

- [ ] **Step 4: Create UserProgress entity**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

public class UserProgress
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int FlashcardId { get; set; }

    public bool IsLearned { get; set; }

    public DateTime LastReviewed { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }
}
```

- [ ] **Step 5: Verify build**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 3: Database Context & Identity Registration

**Files:**
- Create: `Data/AppDbContext.cs`
- Modify: `Program.cs`

- [ ] **Step 1: Create AppDbContext**

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;

namespace ltwnc.Data;

public class AppDbContext : IdentityDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<FlashcardSet> FlashcardSets => Set<FlashcardSet>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<UserProgress> UserProgresses => Set<UserProgress>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<FlashcardSet>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsPublic);
        });

        builder.Entity<Flashcard>(entity =>
        {
            entity.HasIndex(e => e.FlashcardSetId);
            entity.HasOne(e => e.FlashcardSet)
                  .WithMany(s => s.Flashcards)
                  .HasForeignKey(e => e.FlashcardSetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StudySession>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.FlashcardSetId });
        });

        builder.Entity<UserProgress>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.FlashcardId }).IsUnique();
        });
    }
}
```

- [ ] **Step 2: Configure services in Program.cs**

Replace `Program.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ltwnc.Data;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

// Add MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
```

- [ ] **Step 3: Verify build**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 4: Database Migration

**Files:**
- Run EF migrations

- [ ] **Step 1: Create initial migration**

Run: `dotnet ef migrations add InitialCreate`
Expected: Creates `Migrations/` folder with migration files.

- [ ] **Step 2: Update database**

Run: `dotnet ef database update`
Expected: Tables created in SQL Server.

- [ ] **Step 3: Verify connection**

Run: `dotnet build`
Expected: Build passes.

---

### Task 5: Repository Layer

**Files:**
- Create: `Repositories/IFlashcardSetRepository.cs`
- Create: `Repositories/FlashcardSetRepository.cs`
- Create: `Repositories/IFlashcardRepository.cs`
- Create: `Repositories/FlashcardRepository.cs`
- Create: `Repositories/IStudySessionRepository.cs`
- Create: `Repositories/StudySessionRepository.cs`

- [ ] **Step 1: Create IFlashcardSetRepository interface**

```csharp
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public interface IFlashcardSetRepository
{
    Task<List<FlashcardSet>> GetByUserIdAsync(string userId);
    Task<List<FlashcardSet>> GetPublicSetsAsync();
    Task<List<FlashcardSet>> SearchPublicSetsAsync(string query);
    Task<FlashcardSet?> GetByIdAsync(int id);
    Task<FlashcardSet?> GetByIdWithCardsAsync(int id);
    Task AddAsync(FlashcardSet set);
    void Update(FlashcardSet set);
    void Delete(FlashcardSet set);
    Task SaveChangesAsync();
}
```

- [ ] **Step 2: Create FlashcardSetRepository implementation**

```csharp
using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public class FlashcardSetRepository : IFlashcardSetRepository
{
    private readonly AppDbContext _context;

    public FlashcardSetRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<FlashcardSet>> GetByUserIdAsync(string userId)
    {
        return await _context.FlashcardSets
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<FlashcardSet>> GetPublicSetsAsync()
    {
        return await _context.FlashcardSets
            .Where(s => s.IsPublic)
            .OrderByDescending(s => s.UpdatedAt)
            .Take(20)
            .ToListAsync();
    }

    public async Task<List<FlashcardSet>> SearchPublicSetsAsync(string query)
    {
        return await _context.FlashcardSets
            .Where(s => s.IsPublic && s.Title.Contains(query))
            .OrderByDescending(s => s.UpdatedAt)
            .Take(20)
            .ToListAsync();
    }

    public async Task<FlashcardSet?> GetByIdAsync(int id)
    {
        return await _context.FlashcardSets.FindAsync(id);
    }

    public async Task<FlashcardSet?> GetByIdWithCardsAsync(int id)
    {
        return await _context.FlashcardSets
            .Include(s => s.Flashcards.OrderBy(f => f.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task AddAsync(FlashcardSet set)
    {
        await _context.FlashcardSets.AddAsync(set);
    }

    public void Update(FlashcardSet set)
    {
        _context.FlashcardSets.Update(set);
    }

    public void Delete(FlashcardSet set)
    {
        _context.FlashcardSets.Remove(set);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
```

- [ ] **Step 3: Create IFlashcardRepository interface**

```csharp
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public interface IFlashcardRepository
{
    Task<List<Flashcard>> GetBySetIdAsync(int setId);
    Task<Flashcard?> GetByIdAsync(int id);
    Task AddAsync(Flashcard card);
    void Update(Flashcard card);
    void Delete(Flashcard card);
}
```

- [ ] **Step 4: Create FlashcardRepository implementation**

```csharp
using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public class FlashcardRepository : IFlashcardRepository
{
    private readonly AppDbContext _context;

    public FlashcardRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Flashcard>> GetBySetIdAsync(int setId)
    {
        return await _context.Flashcards
            .Where(f => f.FlashcardSetId == setId)
            .OrderBy(f => f.OrderIndex)
            .ToListAsync();
    }

    public async Task<Flashcard?> GetByIdAsync(int id)
    {
        return await _context.Flashcards.FindAsync(id);
    }

    public async Task AddAsync(Flashcard card)
    {
        await _context.Flashcards.AddAsync(card);
    }

    public void Update(Flashcard card)
    {
        _context.Flashcards.Update(card);
    }

    public void Delete(Flashcard card)
    {
        _context.Flashcards.Remove(card);
    }
}
```

- [ ] **Step 5: Create IStudySessionRepository interface**

```csharp
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public interface IStudySessionRepository
{
    Task AddAsync(StudySession session);
    Task<UserProgress?> GetProgressAsync(string userId, int flashcardId);
    Task<List<UserProgress>> GetProgressBySetAsync(string userId, int setId);
    void UpdateProgress(UserProgress progress);
    Task AddProgressAsync(UserProgress progress);
}
```

- [ ] **Step 6: Create StudySessionRepository implementation**

```csharp
using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public class StudySessionRepository : IStudySessionRepository
{
    private readonly AppDbContext _context;

    public StudySessionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(StudySession session)
    {
        await _context.StudySessions.AddAsync(session);
        await _context.SaveChangesAsync();
    }

    public async Task<UserProgress?> GetProgressAsync(string userId, int flashcardId)
    {
        return await _context.UserProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.FlashcardId == flashcardId);
    }

    public async Task<List<UserProgress>> GetProgressBySetAsync(string userId, int setId)
    {
        return await _context.UserProgresses
            .Where(p => p.UserId == userId && p.Flashcard.FlashcardSetId == setId)
            .ToListAsync();
    }

    public void UpdateProgress(UserProgress progress)
    {
        _context.UserProgresses.Update(progress);
    }

    public async Task AddProgressAsync(UserProgress progress)
    {
        await _context.UserProgresses.AddAsync(progress);
    }
}
```

- [ ] **Step 7: Register repositories in DI**

Add to `Program.cs` before `var app = builder.Build();`:

```csharp
builder.Services.AddScoped<IFlashcardSetRepository, FlashcardSetRepository>();
builder.Services.AddScoped<IFlashcardRepository, FlashcardRepository>();
builder.Services.AddScoped<IStudySessionRepository, StudySessionRepository>();
```

Also add `using ltwnc.Repositories;` to top of file.

- [ ] **Step 8: Verify build**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 6: Service Layer

**Files:**
- Create: `Services/IAccountService.cs`
- Create: `Services/AccountService.cs`
- Create: `Services/IFlashcardSetService.cs`
- Create: `Services/FlashcardSetService.cs`
- Create: `Services/IStudyService.cs`
- Create: `Services/StudyService.cs`

- [ ] **Step 1: Create IAccountService interface**

```csharp
using Microsoft.AspNetCore.Identity;

namespace ltwnc.Services;

public interface IAccountService
{
    Task<IdentityResult> RegisterAsync(string email, string username, string password);
    Task<SignInResult> LoginAsync(string email, string password, bool rememberMe);
    Task LogoutAsync();
    Task<IdentityUser?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal);
}
```

- [ ] **Step 2: Create AccountService implementation**

```csharp
using Microsoft.AspNetCore.Identity;
using ltwnc.Repositories;

namespace ltwnc.Services;

public class AccountService : IAccountService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    public AccountService(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<IdentityResult> RegisterAsync(string email, string username, string password)
    {
        var user = new IdentityUser { UserName = username, Email = email };
        var result = await _userManager.CreateAsync(user, password);
        return result;
    }

    public async Task<SignInResult> LoginAsync(string email, string password, bool rememberMe)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return SignInResult.Failed;
        return await _signInManager.PasswordSignInAsync(user, password, rememberMe, false);
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }

    public async Task<IdentityUser?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal)
    {
        return await _userManager.GetUserAsync(principal);
    }
}
```

- [ ] **Step 3: Create IFlashcardSetService interface**

```csharp
using ltwnc.Models.Entities;

namespace ltwnc.Services;

public interface IFlashcardSetService
{
    Task<List<FlashcardSet>> GetMySetsAsync(string userId);
    Task<List<FlashcardSet>> GetPublicSetsAsync();
    Task<List<FlashcardSet>> SearchPublicSetsAsync(string query);
    Task<FlashcardSet?> GetSetByIdAsync(int id);
    Task<FlashcardSet?> GetSetWithCardsAsync(int id, string userId);
    Task<FlashcardSet> CreateSetAsync(string title, string? description, bool isPublic, string userId);
    Task UpdateSetAsync(int id, string title, string? description, bool isPublic, string userId);
    Task DeleteSetAsync(int id, string userId);
    Task<Flashcard> AddCardAsync(int setId, string frontText, string backText, string userId);
    Task<int> UpdateCardAsync(int cardId, string frontText, string backText, string userId);
    Task<int> DeleteCardAsync(int cardId, string userId);
}
```

- [ ] **Step 4: Create FlashcardSetService implementation**

```csharp
using ltwnc.Models.Entities;
using ltwnc.Repositories;

namespace ltwnc.Services;

public class FlashcardSetService : IFlashcardSetService
{
    private readonly IFlashcardSetRepository _setRepo;
    private readonly IFlashcardRepository _cardRepo;

    public FlashcardSetService(IFlashcardSetRepository setRepo, IFlashcardRepository cardRepo)
    {
        _setRepo = setRepo;
        _cardRepo = cardRepo;
    }

    public async Task<List<FlashcardSet>> GetMySetsAsync(string userId)
    {
        return await _setRepo.GetByUserIdAsync(userId);
    }

    public async Task<List<FlashcardSet>> GetPublicSetsAsync()
    {
        return await _setRepo.GetPublicSetsAsync();
    }

    public async Task<List<FlashcardSet>> SearchPublicSetsAsync(string query)
    {
        return await _setRepo.SearchPublicSetsAsync(query);
    }

    public async Task<FlashcardSet?> GetSetByIdAsync(int id)
    {
        return await _setRepo.GetByIdAsync(id);
    }

    public async Task<FlashcardSet?> GetSetWithCardsAsync(int id, string userId)
    {
        var set = await _setRepo.GetByIdWithCardsAsync(id);
        if (set == null || set.UserId != userId) return null;
        return set;
    }

    public async Task<FlashcardSet> CreateSetAsync(string title, string? description, bool isPublic, string userId)
    {
        var set = new FlashcardSet
        {
            Title = title,
            Description = description,
            IsPublic = isPublic,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _setRepo.AddAsync(set);
        await _setRepo.SaveChangesAsync();
        return set;
    }

    public async Task UpdateSetAsync(int id, string title, string? description, bool isPublic, string userId)
    {
        var set = await _setRepo.GetByIdAsync(id);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền sửa bộ thẻ này.");
        set.Title = title;
        set.Description = description;
        set.IsPublic = isPublic;
        set.UpdatedAt = DateTime.UtcNow;
        _setRepo.Update(set);
        await _setRepo.SaveChangesAsync();
    }

    public async Task DeleteSetAsync(int id, string userId)
    {
        var set = await _setRepo.GetByIdAsync(id);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền xóa bộ thẻ này.");
        _setRepo.Delete(set);
        await _setRepo.SaveChangesAsync();
    }

    public async Task<Flashcard> AddCardAsync(int setId, string frontText, string backText, string userId)
    {
        var set = await _setRepo.GetByIdWithCardsAsync(setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền thêm thẻ.");
        var maxOrder = set.Flashcards.Any() ? set.Flashcards.Max(f => f.OrderIndex) : 0;
        var card = new Flashcard
        {
            FlashcardSetId = setId,
            FrontText = frontText,
            BackText = backText,
            OrderIndex = maxOrder + 1
        };
        await _cardRepo.AddAsync(card);
        await _setRepo.SaveChangesAsync();
        return card;
    }

    public async Task<int> UpdateCardAsync(int cardId, string frontText, string backText, string userId)
    {
        var card = await _cardRepo.GetByIdAsync(cardId);
        if (card == null) throw new KeyNotFoundException("Thẻ không tồn tại.");
        var setId = card.FlashcardSetId;
        var set = await _setRepo.GetByIdAsync(setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền sửa thẻ này.");
        card.FrontText = frontText;
        card.BackText = backText;
        _cardRepo.Update(card);
        await _setRepo.SaveChangesAsync();
        return setId;
    }

    public async Task<int> DeleteCardAsync(int cardId, string userId)
    {
        var card = await _cardRepo.GetByIdAsync(cardId);
        if (card == null) throw new KeyNotFoundException("Thẻ không tồn tại.");
        var setId = card.FlashcardSetId;
        var set = await _setRepo.GetByIdAsync(setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền xóa thẻ này.");
        _cardRepo.Delete(card);
        await _setRepo.SaveChangesAsync();
        return setId;
    }
}
```

- [ ] **Step 5: Create IStudyService interface**

```csharp
using ltwnc.Models.Entities;

namespace ltwnc.Services;

public interface IStudyService
{
    Task<List<Flashcard>> GetFlashcardsForStudyAsync(int setId);
    Task MarkLearnedAsync(string userId, int flashcardId, bool learned);
    Task CompleteSessionAsync(string userId, int setId, StudyMode mode);
}
```

- [ ] **Step 6: Create StudyService implementation**

```csharp
using ltwnc.Models.Entities;
using ltwnc.Repositories;

namespace ltwnc.Services;

public class StudyService : IStudyService
{
    private readonly IFlashcardRepository _cardRepo;
    private readonly IStudySessionRepository _studyRepo;
    private readonly IFlashcardSetRepository _setRepo;

    public StudyService(IFlashcardRepository cardRepo, IStudySessionRepository studyRepo, IFlashcardSetRepository setRepo)
    {
        _cardRepo = cardRepo;
        _studyRepo = studyRepo;
        _setRepo = setRepo;
    }

    public async Task<List<Flashcard>> GetFlashcardsForStudyAsync(int setId)
    {
        return await _cardRepo.GetBySetIdAsync(setId);
    }

    public async Task MarkLearnedAsync(string userId, int flashcardId, bool learned)
    {
        var progress = await _studyRepo.GetProgressAsync(userId, flashcardId);
        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = userId,
                FlashcardId = flashcardId,
                IsLearned = learned,
                LastReviewed = DateTime.UtcNow
            };
            await _studyRepo.AddProgressAsync(progress);
        }
        else
        {
            progress.IsLearned = learned;
            progress.LastReviewed = DateTime.UtcNow;
            _studyRepo.UpdateProgress(progress);
        }
        // Save via the card repo's DbContext (same scoped AppDbContext)
        await _setRepo.SaveChangesAsync();
    }

    public async Task CompleteSessionAsync(string userId, int setId, StudyMode mode)
    {
        var session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = mode,
            CompletedAt = DateTime.UtcNow
        };
        await _studyRepo.AddAsync(session);
    }
}
```

- [ ] **Step 7: Register services in DI**

Add to `Program.cs` before `var app = builder.Build();`:

```csharp
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IFlashcardSetService, FlashcardSetService>();
builder.Services.AddScoped<IStudyService, StudyService>();
```

- [ ] **Step 8: Verify build**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 7: ViewModels

**Files:**
- Create: `Models/ViewModels/Account/RegisterViewModel.cs`
- Create: `Models/ViewModels/Account/LoginViewModel.cs`
- Create: `Models/ViewModels/FlashcardSet/CreateSetViewModel.cs`
- Create: `Models/ViewModels/FlashcardSet/EditSetViewModel.cs`
- Create: `Models/ViewModels/FlashcardSet/SetDetailViewModel.cs`
- Create: `Models/ViewModels/Study/FlashcardStudyViewModel.cs`
- Create: `Models/ViewModels/Home/HomeViewModel.cs`

- [ ] **Step 1: Create Account ViewModels**

```csharp
// Models/ViewModels/Account/RegisterViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3-50 ký tự.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự.")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Mật khẩu phải có ít nhất 1 chữ hoa và 1 số.")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// Models/ViewModels/Account/LoginViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Account;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
```

- [ ] **Step 2: Create FlashcardSet ViewModels**

```csharp
// Models/ViewModels/FlashcardSet/CreateSetViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.FlashcardSet;

public class CreateSetViewModel
{
    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsPublic { get; set; } = true;
}

// Models/ViewModels/FlashcardSet/EditSetViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.FlashcardSet;

public class EditSetViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsPublic { get; set; } = true;
}

// Models/ViewModels/FlashcardSet/SetDetailViewModel.cs
using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.FlashcardSet;

public class SetDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public string UserId { get; set; } = string.Empty;
    public List<Models.Entities.Flashcard> Flashcards { get; set; } = new();
    public bool IsOwner { get; set; }
}
```

- [ ] **Step 3: Create Study and Home ViewModels**

```csharp
// Models/ViewModels/Study/FlashcardStudyViewModel.cs
using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

public class FlashcardStudyViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public List<Models.Entities.Flashcard> Flashcards { get; set; } = new();
    public int CurrentIndex { get; set; }
}

// Models/ViewModels/Home/HomeViewModel.cs
using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Home;

public class HomeViewModel
{
    public List<Models.Entities.FlashcardSet> PublicSets { get; set; } = new();
    public string? SearchQuery { get; set; }
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 8: UI Layout & Design System

**Files:**
- Modify: `Views/Shared/_Layout.cshtml`
- Modify: `Views/_ViewImports.cshtml`
- Modify: `wwwroot/css/site.css`
- Modify: `wwwroot/js/site.js`

- [ ] **Step 1: Update _ViewImports.cshtml**

```html
@using ltwnc
@using ltwnc.Models.ViewModels.Account
@using ltwnc.Models.ViewModels.FlashcardSet
@using ltwnc.Models.ViewModels.Study
@using ltwnc.Models.ViewModels.Home
@using ltwnc.Models.Entities
@using Microsoft.AspNetCore.Identity
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@inject SignInManager<IdentityUser> SignInManager
@inject UserManager<IdentityUser> UserManager
```

- [ ] **Step 2: Create _Layout.cshtml with design system**

```html
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - LTWNC English</title>
    <link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="https://unpkg.com/phosphor-icons@1.4.2/src/css/icons.css" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
</head>
<body>
    <header>
        <nav class="navbar navbar-expand-lg" style="background: #FFFFFF; border-bottom: 1px solid #EAEAEA;">
            <div class="container">
                <a class="navbar-brand fw-bold" href="/" style="color: #111111; letter-spacing: -0.02em;">
                    <i class="ph ph-book-open"></i> LTWNC
                </a>
                <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarNav">
                    <span class="navbar-toggler-icon"></span>
                </button>
                <div class="collapse navbar-collapse" id="navbarNav">
                    <ul class="navbar-nav me-auto">
                        <li class="nav-item">
                            <a class="nav-link" href="/" style="color: #787774;">Trang chủ</a>
                        </li>
                        @if (SignInManager.IsSignedIn(User))
                        {
                            <li class="nav-item">
                                <a class="nav-link" href="/Set" style="color: #787774;">Bộ thẻ của tôi</a>
                            </li>
                        }
                    </ul>
                    <ul class="navbar-nav">
                        @if (SignInManager.IsSignedIn(User))
                        {
                            <li class="nav-item dropdown">
                                <a class="nav-link dropdown-toggle" href="#" data-bs-toggle="dropdown" style="color: #111111;">
                                    <i class="ph ph-user-circle"></i> @User.Identity?.Name
                                </a>
                                <ul class="dropdown-menu dropdown-menu-end" style="border: 1px solid #EAEAEA; border-radius: 8px;">
                                    <li>
                                        <form asp-controller="Account" asp-action="Logout" method="post" class="dropdown-item p-0">
                                            <button type="submit" class="dropdown-item" style="color: #787774;">
                                                <i class="ph ph-sign-out"></i> Đăng xuất
                                            </button>
                                        </form>
                                    </li>
                                </ul>
                            </li>
                        }
                        else
                        {
                            <li class="nav-item">
                                <a class="nav-link" href="/Account/Login" style="color: #787774;">Đăng nhập</a>
                            </li>
                            <li class="nav-item ms-2">
                                <a class="btn" href="/Account/Register" style="background: #111111; color: #FFFFFF; border-radius: 6px;">
                                    Đăng ký
                                </a>
                            </li>
                        }
                    </ul>
                </div>
            </div>
        </nav>
    </header>

    <main style="background: #F7F6F3; min-height: calc(100vh - 120px);">
        <div class="container py-5">
            @RenderBody()
        </div>
    </main>

    <footer style="background: #FFFFFF; border-top: 1px solid #EAEAEA; color: #787774; font-size: 0.875rem;">
        <div class="container py-4 text-center">
            &copy; @DateTime.Now.Year LTWNC English. Học tiếng Anh cùng flashcard.
        </div>
    </footer>

    <script src="~/lib/jquery/dist/jquery.min.js"></script>
    <script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
    <script src="~/js/site.js" asp-append-version="true"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

- [ ] **Step 3: Replace site.css with design system**

```css
/* LTWNC English — Premium Utilitarian Minimalism */
/* Color tokens */
:root {
    --bg-canvas: #F7F6F3;
    --bg-surface: #FFFFFF;
    --border-default: #EAEAEA;
    --text-primary: #111111;
    --text-secondary: #787774;
    --accent-green-bg: #EDF3EC;
    --accent-green-text: #346538;
    --accent-red-bg: #FDEBEC;
    --accent-red-text: #9F2F2D;
    --accent-blue-bg: #E1F3FE;
    --accent-blue-text: #1F6C9F;
    --accent-yellow-bg: #FBF3DB;
    --accent-yellow-text: #956400;
}

body {
    font-family: 'SF Pro Display', 'Helvetica Neue', 'Segoe UI', sans-serif;
    color: var(--text-primary);
    background: var(--bg-canvas);
    line-height: 1.6;
}

/* Cards */
.card-custom {
    background: var(--bg-surface);
    border: 1px solid var(--border-default);
    border-radius: 12px;
    padding: 24px;
    transition: box-shadow 200ms ease;
}

.card-custom:hover {
    box-shadow: 0 2px 8px rgba(0,0,0,0.04);
}

/* Buttons */
.btn-primary-custom {
    background: #111111;
    color: #FFFFFF;
    border: none;
    border-radius: 6px;
    padding: 8px 20px;
    font-size: 0.9375rem;
    transition: transform 0.1s ease;
}

.btn-primary-custom:hover {
    background: #333333;
    color: #FFFFFF;
}

.btn-primary-custom:active {
    transform: scale(0.98);
}

.btn-secondary-custom {
    background: #FFFFFF;
    color: #111111;
    border: 1px solid #EAEAEA;
    border-radius: 6px;
    padding: 8px 20px;
    font-size: 0.9375rem;
    transition: transform 0.1s ease;
}

.btn-secondary-custom:hover {
    background: #F7F6F3;
}

.btn-secondary-custom:active {
    transform: scale(0.98);
}

/* Tags */
.tag {
    display: inline-block;
    border-radius: 9999px;
    padding: 2px 12px;
    font-size: 0.75rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.tag-learned {
    background: var(--accent-green-bg);
    color: var(--accent-green-text);
}

.tag-new {
    background: var(--accent-blue-bg);
    color: var(--accent-blue-text);
}

/* Flashcard */
.flashcard-container {
    perspective: 1000px;
    width: 100%;
    max-width: 600px;
    height: 350px;
    margin: 0 auto;
    cursor: pointer;
}

.flashcard {
    position: relative;
    width: 100%;
    height: 100%;
    transition: transform 0.6s ease;
    transform-style: preserve-3d;
}

.flashcard.flipped {
    transform: rotateY(180deg);
}

.flashcard-front,
.flashcard-back {
    position: absolute;
    width: 100%;
    height: 100%;
    backface-visibility: hidden;
    display: flex;
    align-items: center;
    justify-content: center;
    background: #FFFFFF;
    border: 1px solid #EAEAEA;
    border-radius: 12px;
    padding: 40px;
    font-size: 1.5rem;
    font-weight: 500;
}

.flashcard-back {
    transform: rotateY(180deg);
}

/* Form inputs */
.form-input-custom {
    border: 1px solid #EAEAEA;
    border-radius: 8px;
    padding: 10px 16px;
    font-size: 0.9375rem;
    width: 100%;
    transition: border-color 0.2s ease;
}

.form-input-custom:focus {
    outline: none;
    border-color: #111111;
}

/* Progress bar */
.progress-custom {
    height: 6px;
    background: #EAEAEA;
    border-radius: 3px;
    overflow: hidden;
}

.progress-custom-bar {
    height: 100%;
    background: #111111;
    border-radius: 3px;
    transition: width 0.3s ease;
}

/* Animations */
.fade-in-up {
    opacity: 0;
    transform: translateY(12px);
    animation: fadeInUp 0.6s cubic-bezier(0.16, 1, 0.3, 1) forwards;
}

@keyframes fadeInUp {
    to {
        opacity: 1;
        transform: translateY(0);
    }
}

.stagger-item {
    opacity: 0;
    transform: translateY(12px);
    animation: fadeInUp 0.6s cubic-bezier(0.16, 1, 0.3, 1) forwards;
}

/* Validation */
.field-validation-error {
    color: #9F2F2D;
    font-size: 0.8125rem;
}

.input-validation-error {
    border-color: #9F2F2D !important;
}

/* Hero section */
.hero-section {
    padding: 80px 0;
    text-align: center;
}

.hero-title {
    font-size: 2.5rem;
    font-weight: 700;
    letter-spacing: -0.03em;
    color: #111111;
    margin-bottom: 16px;
}

.hero-subtitle {
    font-size: 1.125rem;
    color: #787774;
    max-width: 500px;
    margin: 0 auto 32px;
}
```

- [ ] **Step 4: Create site.js with basic helpers**

```javascript
// Flashcard flip
function toggleFlip() {
    document.querySelector('.flashcard')?.classList.toggle('flipped');
}

// Add stagger animation to list items
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.stagger-item').forEach((el, i) => {
        el.style.animationDelay = `${i * 80}ms`;
    });
});
```

- [ ] **Step 5: Verify build**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 9: Account Controller & Views

**Files:**
- Create: `Controllers/AccountController.cs`
- Create: `Views/Account/Register.cshtml`
- Create: `Views/Account/Login.cshtml`

- [ ] **Step 1: Create AccountController**

```csharp
using Microsoft.AspNetCore.Mvc;
using ltwnc.Services;
using ltwnc.Models.ViewModels.Account;

namespace ltwnc.Controllers;

public class AccountController : Controller
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _accountService.RegisterAsync(model.Email, model.Username, model.Password);
        if (result.Succeeded)
        {
            await _accountService.LoginAsync(model.Email, model.Password, false);
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _accountService.LoginAsync(model.Email, model.Password, model.RememberMe);
        if (result.Succeeded)
        {
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _accountService.LogoutAsync();
        return RedirectToAction("Index", "Home");
    }
}
```

- [ ] **Step 2: Create Register.cshtml**

```html
@model RegisterViewModel
@{
    ViewData["Title"] = "Đăng ký";
}

<div class="row justify-content-center fade-in-up">
    <div class="col-md-6 col-lg-5">
        <div class="card-custom">
            <h1 class="h3 mb-1" style="letter-spacing: -0.02em;">Đăng ký</h1>
            <p style="color: #787774; font-size: 0.9375rem;">Tạo tài khoản để bắt đầu học.</p>

            <form asp-action="Register" method="post" class="mt-4">
                <div asp-validation-summary="ModelOnly" class="mb-3"></div>

                <div class="mb-3">
                    <label asp-for="Email" class="form-label" style="font-size: 0.875rem; color: #787774;"></label>
                    <input asp-for="Email" class="form-input-custom" placeholder="you@example.com" />
                    <span asp-validation-for="Email"></span>
                </div>

                <div class="mb-3">
                    <label asp-for="Username" class="form-label" style="font-size: 0.875rem; color: #787774;"></label>
                    <input asp-for="Username" class="form-input-custom" placeholder="Tên của bạn" />
                    <span asp-validation-for="Username"></span>
                </div>

                <div class="mb-3">
                    <label asp-for="Password" class="form-label" style="font-size: 0.875rem; color: #787774;"></label>
                    <input asp-for="Password" class="form-input-custom" placeholder="Mật khẩu" />
                    <span asp-validation-for="Password"></span>
                </div>

                <div class="mb-4">
                    <label asp-for="ConfirmPassword" class="form-label" style="font-size: 0.875rem; color: #787774;"></label>
                    <input asp-for="ConfirmPassword" class="form-input-custom" placeholder="Nhập lại mật khẩu" />
                    <span asp-validation-for="ConfirmPassword"></span>
                </div>

                <button type="submit" class="btn-primary-custom w-100">Đăng ký</button>
            </form>

            <p class="mt-3 text-center" style="font-size: 0.875rem; color: #787774;">
                Đã có tài khoản? <a href="/Account/Login" style="color: #1F6C9F;">Đăng nhập</a>
            </p>
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

- [ ] **Step 3: Create Login.cshtml**

```html
@model LoginViewModel
@{
    ViewData["Title"] = "Đăng nhập";
}

<div class="row justify-content-center fade-in-up">
    <div class="col-md-6 col-lg-5">
        <div class="card-custom">
            <h1 class="h3 mb-1" style="letter-spacing: -0.02em;">Đăng nhập</h1>
            <p style="color: #787774; font-size: 0.9375rem;">Chào mừng bạn trở lại.</p>

            <form asp-action="Login" method="post" class="mt-4">
                <div asp-validation-summary="ModelOnly" class="mb-3"></div>

                <div class="mb-3">
                    <label asp-for="Email" class="form-label" style="font-size: 0.875rem; color: #787774;"></label>
                    <input asp-for="Email" class="form-input-custom" placeholder="you@example.com" />
                    <span asp-validation-for="Email"></span>
                </div>

                <div class="mb-3">
                    <label asp-for="Password" class="form-label" style="font-size: 0.875rem; color: #787774;"></label>
                    <input asp-for="Password" class="form-input-custom" placeholder="Mật khẩu" />
                    <span asp-validation-for="Password"></span>
                </div>

                <div class="mb-4 form-check">
                    <input asp-for="RememberMe" class="form-check-input" />
                    <label asp-for="RememberMe" style="font-size: 0.875rem; color: #787774;">Ghi nhớ đăng nhập</label>
                </div>

                <button type="submit" class="btn-primary-custom w-100">Đăng nhập</button>
            </form>

            <p class="mt-3 text-center" style="font-size: 0.875rem; color: #787774;">
                Chưa có tài khoản? <a href="/Account/Register" style="color: #1F6C9F;">Đăng ký</a>
            </p>
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 10: Home Controller & View

**Files:**
- Modify: `Controllers/HomeController.cs`
- Create: `Views/Home/Index.cshtml`

- [ ] **Step 1: Update HomeController**

```csharp
using Microsoft.AspNetCore.Mvc;
using ltwnc.Services;
using ltwnc.Models.ViewModels.Home;

namespace ltwnc.Controllers;

public class HomeController : Controller
{
    private readonly IFlashcardSetService _setService;

    public HomeController(IFlashcardSetService setService)
    {
        _setService = setService;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var model = new HomeViewModel();

        if (!string.IsNullOrEmpty(q))
        {
            model.SearchQuery = q;
            model.PublicSets = await _setService.SearchPublicSetsAsync(q);
        }
        else
        {
            model.PublicSets = await _setService.GetPublicSetsAsync();
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
```

- [ ] **Step 2: Create Home/Index.cshtml**

```html
@model HomeViewModel
@{
    ViewData["Title"] = "Trang chủ";
}

<div class="hero-section fade-in-up">
    <h1 class="hero-title">Học tiếng Anh với Flashcard</h1>
    <p class="hero-subtitle">Tạo bộ thẻ của riêng bạn hoặc học từ bộ thẻ có sẵn từ cộng đồng.</p>

    <form asp-action="Index" method="get" class="d-flex justify-content-center gap-2" style="max-width: 480px; margin: 0 auto;">
        <input type="text" name="q" value="@Model.SearchQuery" class="form-input-custom" placeholder="Tìm bộ thẻ..." />
        <button type="submit" class="btn-primary-custom">
            <i class="ph ph-magnifying-glass"></i> Tìm
        </button>
    </form>

    @if (User.Identity?.IsAuthenticated == true)
    {
        <a href="/Set/Create" class="btn-primary-custom mt-3 d-inline-block">
            <i class="ph ph-plus-circle"></i> Tạo bộ thẻ mới
        </a>
    }
</div>

@if (!string.IsNullOrEmpty(Model.SearchQuery))
{
    <p class="mb-4" style="color: #787774;">
        Kết quả tìm kiếm cho "<strong>@Model.SearchQuery</strong>"
    </p>
}

@if (!Model.PublicSets.Any())
{
    <div class="text-center py-5" style="color: #787774;">
        <i class="ph ph-book-open" style="font-size: 3rem;"></i>
        <p class="mt-3">Chưa có bộ thẻ nào.</p>
    </div>
}

<div class="row g-4">
    @foreach (var set in Model.PublicSets)
    {
        <div class="col-md-6 col-lg-4 stagger-item">
            <div class="card-custom h-100">
                <div class="d-flex justify-content-between align-items-start mb-2">
                    <h5 class="mb-0" style="font-weight: 600;">@set.Title</h5>
                    <span class="tag tag-new">Công khai</span>
                </div>
                @if (!string.IsNullOrEmpty(set.Description))
                {
                    <p style="color: #787774; font-size: 0.875rem;">@set.Description</p>
                }
                <div class="d-flex justify-content-between align-items-center mt-3">
                    <small style="color: #787774;">
                        <i class="ph ph-cards"></i> @set.Flashcards?.Count ?? 0 thẻ
                    </small>
                    <a href="/Study/@set.Id/Flashcard" class="btn-primary-custom" style="padding: 4px 16px; font-size: 0.8125rem;">
                        <i class="ph ph-play-circle"></i> Học
                    </a>
                </div>
            </div>
        </div>
    }
</div>
```

- [ ] **Step 3: Verify build**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 11: FlashcardSet Controller & Views

**Files:**
- Create: `Controllers/FlashcardSetController.cs`
- Create: `Views/FlashcardSet/Index.cshtml`
- Create: `Views/FlashcardSet/Create.cshtml`
- Create: `Views/FlashcardSet/Edit.cshtml`
- Create: `Views/FlashcardSet/Details.cshtml`

- [ ] **Step 1: Create FlashcardSetController**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Services;
using ltwnc.Models.ViewModels.FlashcardSet;

namespace ltwnc.Controllers;

[Authorize]
public class FlashcardSetController : Controller
{
    private readonly IFlashcardSetService _setService;
    private readonly IAccountService _accountService;

    public FlashcardSetController(IFlashcardSetService setService, IAccountService accountService)
    {
        _setService = setService;
        _accountService = accountService;
    }

    [Route("/Set")]
    public async Task<IActionResult> Index()
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        var sets = await _setService.GetMySetsAsync(user.Id);
        return View(sets);
    }

    [Route("/Set/Create")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [Route("/Set/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSetViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        var set = await _setService.CreateSetAsync(model.Title, model.Description, model.IsPublic, user.Id);
        return RedirectToAction("Edit", new { id = set.Id });
    }

    [Route("/Set/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        var set = await _setService.GetSetByIdAsync(id);
        if (set == null) return NotFound();

        // Load cards
        var cards = await _setService.GetSetWithCardsAsync(id, set.UserId);
        
        var model = new SetDetailViewModel
        {
            Id = set.Id,
            Title = set.Title,
            Description = set.Description,
            IsPublic = set.IsPublic,
            UserId = set.UserId,
            Flashcards = cards?.Flashcards.ToList() ?? new(),
            IsOwner = user?.Id == set.UserId
        };
        return View(model);
    }

    [Route("/Set/{id}/Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        var set = await _setService.GetSetWithCardsAsync(id, user.Id);
        if (set == null) return NotFound();

        var model = new EditSetViewModel
        {
            Id = set.Id,
            Title = set.Title,
            Description = set.Description,
            IsPublic = set.IsPublic
        };
        ViewBag.Cards = set.Flashcards.ToList();
        return View(model);
    }

    [HttpPost]
    [Route("/Set/{id}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditSetViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            await _setService.UpdateSetAsync(id, model.Title, model.Description, model.IsPublic, user.Id);
            return RedirectToAction("Edit", new { id });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [Route("/Set/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            await _setService.DeleteSetAsync(id, user.Id);
            return RedirectToAction("Index");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // Card management (inline on Edit page)
    [HttpPost]
    [Route("/Set/{setId}/Cards/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCard(int setId, string frontText, string backText)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            await _setService.AddCardAsync(setId, frontText, backText, user.Id);
            return RedirectToAction("Edit", new { id = setId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [Route("/Cards/{id}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCard(int id, string frontText, string backText)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            var setId = await _setService.UpdateCardAsync(id, frontText, backText, user.Id);
            return RedirectToAction("Edit", new { id = setId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [Route("/Cards/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCard(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            var setId = await _setService.DeleteCardAsync(id, user.Id);
            return RedirectToAction("Edit", new { id = setId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
```

- [ ] **Step 2: Create Views/FlashcardSet/Index.cshtml**

```html
@model List<ltwnc.Models.Entities.FlashcardSet>
@{
    ViewData["Title"] = "Bộ thẻ của tôi";
}

<div class="d-flex justify-content-between align-items-center mb-4 fade-in-up">
    <div>
        <h1 class="h3 mb-1" style="letter-spacing: -0.02em;">Bộ thẻ của tôi</h1>
        <p style="color: #787774; font-size: 0.9375rem;">Quản lý tất cả bộ thẻ bạn đã tạo.</p>
    </div>
    <a href="/Set/Create" class="btn-primary-custom">
        <i class="ph ph-plus-circle"></i> Tạo mới
    </a>
</div>

@if (!Model.Any())
{
    <div class="text-center py-5" style="color: #787774;">
        <i class="ph ph-book-open" style="font-size: 3rem;"></i>
        <p class="mt-3">Bạn chưa có bộ thẻ nào.</p>
        <a href="/Set/Create" class="btn-primary-custom mt-2 d-inline-block">Tạo bộ thẻ đầu tiên</a>
    </div>
}

<div class="row g-4">
    @foreach (var set in Model)
    {
        <div class="col-md-6 col-lg-4 stagger-item">
            <div class="card-custom h-100">
                <div class="d-flex justify-content-between align-items-start mb-2">
                    <h5 class="mb-0" style="font-weight: 600;">@set.Title</h5>
                    <span class="tag @(set.IsPublic ? "tag-new" : "")">
                        @(set.IsPublic ? "Công khai" : "Riêng tư")
                    </span>
                </div>
                @if (!string.IsNullOrEmpty(set.Description))
                {
                    <p style="color: #787774; font-size: 0.875rem;">@set.Description</p>
                }
                <div class="mt-3 d-flex gap-2">
                    <a href="/Study/@set.Id/Flashcard" class="btn-primary-custom" style="padding: 4px 16px; font-size: 0.8125rem;">
                        <i class="ph ph-play-circle"></i> Học
                    </a>
                    <a href="/Set/@set.Id/Edit" class="btn-secondary-custom" style="padding: 4px 16px; font-size: 0.8125rem;">
                        <i class="ph ph-pencil"></i> Sửa
                    </a>
                    <form asp-action="Delete" asp-route-id="@set.Id" method="post" class="d-inline" onsubmit="return confirm('Xóa bộ thẻ này?');">
                        <button type="submit" class="btn-secondary-custom" style="padding: 4px 16px; font-size: 0.8125rem; color: #9F2F2D;">
                            <i class="ph ph-trash"></i> Xóa
                        </button>
                    </form>
                </div>
            </div>
        </div>
    }
</div>
```

- [ ] **Step 3: Create Views/FlashcardSet/Create.cshtml**

```html
@model CreateSetViewModel
@{
    ViewData["Title"] = "Tạo bộ thẻ mới";
}

<div class="row justify-content-center fade-in-up">
    <div class="col-md-8 col-lg-6">
        <div class="card-custom">
            <h1 class="h3 mb-1" style="letter-spacing: -0.02em;">Tạo bộ thẻ mới</h1>
            <p style="color: #787774; font-size: 0.9375rem;">Đặt tiêu đề và mô tả cho bộ thẻ của bạn.</p>

            <form asp-action="Create" method="post" class="mt-4">
                <div asp-validation-summary="ModelOnly" class="mb-3"></div>

                <div class="mb-3">
                    <label asp-for="Title" class="form-label" style="font-size: 0.875rem; color: #787774;"></label>
                    <input asp-for="Title" class="form-input-custom" placeholder="Ví dụ: Từ vựng Unit 1" />
                    <span asp-validation-for="Title"></span>
                </div>

                <div class="mb-3">
                    <label asp-for="Description" class="form-label" style="font-size: 0.875rem; color: #787774;"></label>
                    <textarea asp-for="Description" class="form-input-custom" rows="3" placeholder="Mô tả ngắn về bộ thẻ..."></textarea>
                </div>

                <div class="mb-4 form-check">
                    <input asp-for="IsPublic" class="form-check-input" />
                    <label asp-for="IsPublic" style="font-size: 0.875rem; color: #787774;">Công khai — mọi người có thể xem và học</label>
                </div>

                <button type="submit" class="btn-primary-custom w-100">Tiếp tục — Thêm thẻ</button>
            </form>
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

- [ ] **Step 4: Create Views/FlashcardSet/Edit.cshtml**

```html
@model EditSetViewModel
@{
    ViewData["Title"] = "Sửa bộ thẻ";
    var cards = ViewBag.Cards as List<ltwnc.Models.Entities.Flashcard> ?? new();
}

<div class="row justify-content-center fade-in-up">
    <div class="col-md-10 col-lg-8">
        <div class="card-custom mb-4">
            <h1 class="h3 mb-1" style="letter-spacing: -0.02em;">Sửa bộ thẻ</h1>

            <form asp-action="Edit" asp-route-id="@Model.Id" method="post" class="mt-4">
                <div asp-validation-summary="ModelOnly" class="mb-3"></div>

                <div class="mb-3">
                    <label asp-for="Title" class="form-label" style="font-size: 0.875rem; color: #787774;"></label>
                    <input asp-for="Title" class="form-input-custom" />
                    <span asp-validation-for="Title"></span>
                </div>

                <div class="mb-3">
                    <label asp-for="Description" class="form-label" style="font-size: 0.875rem; color: #787774;"></label>
                    <textarea asp-for="Description" class="form-input-custom" rows="3"></textarea>
                </div>

                <div class="mb-4 form-check">
                    <input asp-for="IsPublic" class="form-check-input" />
                    <label asp-for="IsPublic" style="font-size: 0.875rem; color: #787774;">Công khai</label>
                </div>

                <button type="submit" class="btn-primary-custom">Lưu thay đổi</button>
                <a href="/Set" class="btn-secondary-custom ms-2">Hủy</a>
            </form>
        </div>

        <!-- Card list -->
        <div class="card-custom">
            <div class="d-flex justify-content-between align-items-center mb-3">
                <h5 class="mb-0" style="font-weight: 600;">Thẻ trong bộ</h5>
            </div>

            @if (!cards.Any())
            {
                <p style="color: #787774;">Chưa có thẻ nào.</p>
            }

            <div class="mb-4" id="cardList">
                @foreach (var card in cards.OrderBy(c => c.OrderIndex))
                {
                    <div class="d-flex align-items-center gap-2 mb-2 p-3" style="border: 1px solid #EAEAEA; border-radius: 8px;">
                        <span style="color: #787774; font-size: 0.8125rem; min-width: 24px;">@card.OrderIndex</span>
                        <span style="flex: 1; font-weight: 500;">@card.FrontText</span>
                        <i class="ph ph-arrow-right" style="color: #787774;"></i>
                        <span style="flex: 1;">@card.BackText</span>
                        <form asp-action="DeleteCard" asp-route-id="@card.Id" method="post" class="d-inline" onsubmit="return confirm('Xóa thẻ này?');">
                            <button type="submit" class="btn-secondary-custom" style="padding: 2px 8px; font-size: 0.75rem; color: #9F2F2D;">
                                <i class="ph ph-x"></i>
                            </button>
                        </form>
                    </div>
                }
            </div>

            <!-- Add card form -->
            <h6 class="mb-2" style="font-weight: 600;">Thêm thẻ mới</h6>
            <form asp-action="AddCard" asp-route-setId="@Model.Id" method="post" class="row g-2">
                <div class="col-md-5">
                    <input type="text" name="frontText" class="form-input-custom" placeholder="Tiếng Anh" required />
                </div>
                <div class="col-md-5">
                    <input type="text" name="backText" class="form-input-custom" placeholder="Nghĩa tiếng Việt" required />
                </div>
                <div class="col-md-2">
                    <button type="submit" class="btn-primary-custom w-100">
                        <i class="ph ph-plus"></i>
                    </button>
                </div>
            </form>
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

- [ ] **Step 5: Create Views/FlashcardSet/Details.cshtml**

```html
@model SetDetailViewModel
@{
    ViewData["Title"] = Model.Title;
}

<div class="fade-in-up">
    <div class="d-flex justify-content-between align-items-start mb-4">
        <div>
            <h1 class="h3 mb-1" style="letter-spacing: -0.02em;">@Model.Title</h1>
            @if (!string.IsNullOrEmpty(Model.Description))
            {
                <p style="color: #787774;">@Model.Description</p>
            }
            <small style="color: #787774;">
                <i class="ph ph-cards"></i> @Model.Flashcards.Count thẻ
                @if (Model.IsPublic)
                {
                    <span class="tag tag-new ms-2">Công khai</span>
                }
            </small>
        </div>
        <div class="d-flex gap-2">
            @if (Model.IsOwner)
            {
                <a href="/Set/@Model.Id/Edit" class="btn-secondary-custom">
                    <i class="ph ph-pencil"></i> Sửa
                </a>
            }
            <a href="/Study/@Model.Id/Flashcard" class="btn-primary-custom">
                <i class="ph ph-play-circle"></i> Học ngay
            </a>
        </div>
    </div>

    @if (Model.Flashcards.Any())
    {
        <div class="card-custom">
            <h5 class="mb-3" style="font-weight: 600;">Danh sách thẻ</h5>
            @foreach (var card in Model.Flashcards.OrderBy(c => c.OrderIndex))
            {
                <div class="d-flex align-items-center gap-3 py-3" style="border-bottom: 1px solid #EAEAEA;">
                    <span style="color: #787774; font-size: 0.8125rem; min-width: 28px;">@card.OrderIndex</span>
                    <span style="flex: 1; font-weight: 500; font-size: 1.0625rem;">@card.FrontText</span>
                    <i class="ph ph-arrow-right" style="color: #787774;"></i>
                    <span style="flex: 1; font-size: 1.0625rem;">@card.BackText</span>
                </div>
            }
        </div>
    }
    else
    {
        <div class="text-center py-5" style="color: #787774;">
            <i class="ph ph-cards" style="font-size: 3rem;"></i>
            <p class="mt-3">Bộ thẻ này chưa có thẻ nào.</p>
        </div>
    }
</div>
```

- [ ] **Step 6: Verify build**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 12: Study Controller & Views

**Files:**
- Create: `Controllers/StudyController.cs`
- Create: `Views/Study/Index.cshtml`
- Create: `Views/Study/Flashcard.cshtml`

- [ ] **Step 1: Create StudyController**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Services;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Controllers;

[Authorize]
public class StudyController : Controller
{
    private readonly IStudyService _studyService;
    private readonly IFlashcardSetService _setService;
    private readonly IAccountService _accountService;

    public StudyController(IStudyService studyService, IFlashcardSetService setService, IAccountService accountService)
    {
        _studyService = studyService;
        _setService = setService;
        _accountService = accountService;
    }

    [Route("/Study/{setId}")]
    public async Task<IActionResult> Index(int setId)
    {
        var set = await _setService.GetSetByIdAsync(setId);
        if (set == null) return NotFound();
        ViewBag.SetTitle = set.Title;
        ViewBag.SetId = setId;
        return View();
    }

    [Route("/Study/{setId}/Flashcard")]
    public async Task<IActionResult> Flashcard(int setId, int index = 0)
    {
        var set = await _setService.GetSetByIdAsync(setId);
        if (set == null) return NotFound();

        var cards = await _studyService.GetFlashcardsForStudyAsync(setId);
        if (!cards.Any())
        {
            TempData["Message"] = "Bộ thẻ này chưa có thẻ nào.";
            return RedirectToAction("Index", new { setId });
        }

        var model = new FlashcardStudyViewModel
        {
            SetId = setId,
            SetTitle = set.Title,
            Flashcards = cards,
            CurrentIndex = Math.Clamp(index, 0, cards.Count - 1)
        };

        return View(model);
    }

    [HttpPost]
    [Route("/Study/{setId}/Flashcard/Mark")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkLearned(int setId, int cardId, bool learned)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        await _studyService.MarkLearnedAsync(user.Id, cardId, learned);
        return RedirectToAction("Flashcard", new { setId });
    }

    [HttpPost]
    [Route("/Study/{setId}/Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int setId)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        await _studyService.CompleteSessionAsync(user.Id, setId, Models.Entities.StudyMode.Flashcard);
        TempData["Success"] = "Hoàn thành buổi học!";
        return RedirectToAction("Index", new { setId });
    }
}
```

- [ ] **Step 2: Create Views/Study/Index.cshtml**

```html
@{
    ViewData["Title"] = "Chọn chế độ học";
    var setId = ViewBag.SetId as int? ?? 0;
    var setTitle = ViewBag.SetTitle as string ?? "";
}

<div class="row justify-content-center fade-in-up">
    <div class="col-md-8">
        <div class="text-center mb-5">
            <h1 class="h3" style="letter-spacing: -0.02em;">@setTitle</h1>
            <p style="color: #787774;">Chọn chế độ học.</p>
        </div>

        <div class="row g-4 justify-content-center">
            <div class="col-md-6">
                <a href="/Study/@setId/Flashcard" class="text-decoration-none">
                    <div class="card-custom text-center py-5">
                        <i class="ph ph-cards" style="font-size: 3rem;"></i>
                        <h5 class="mt-3 mb-1" style="font-weight: 600;">Flashcard</h5>
                        <p style="color: #787774; font-size: 0.875rem; margin: 0;">Lật thẻ và ghi nhớ</p>
                    </div>
                </a>
            </div>

            <div class="col-md-6">
                <div class="card-custom text-center py-5" style="opacity: 0.5;">
                    <i class="ph ph-question" style="font-size: 3rem;"></i>
                    <h5 class="mt-3 mb-1" style="font-weight: 600;">Trắc nghiệm</h5>
                    <p style="color: #787774; font-size: 0.875rem; margin: 0;">(Sắp ra mắt)</p>
                </div>
            </div>

            <div class="col-md-6">
                <div class="card-custom text-center py-5" style="opacity: 0.5;">
                    <i class="ph ph-pencil" style="font-size: 3rem;"></i>
                    <h5 class="mt-3 mb-1" style="font-weight: 600;">Viết</h5>
                    <p style="color: #787774; font-size: 0.875rem; margin: 0;">(Sắp ra mắt)</p>
                </div>
            </div>

            <div class="col-md-6">
                <div class="card-custom text-center py-5" style="opacity: 0.5;">
                    <i class="ph ph-shuffle" style="font-size: 3rem;"></i>
                    <h5 class="mt-3 mb-1" style="font-weight: 600;">Ghép đôi</h5>
                    <p style="color: #787774; font-size: 0.875rem; margin: 0;">(Sắp ra mắt)</p>
                </div>
            </div>
        </div>
    </div>
</div>
```

- [ ] **Step 3: Create Views/Study/Flashcard.cshtml**

```html
@model FlashcardStudyViewModel
@{
    ViewData["Title"] = "Học flashcard";
    var total = Model.Flashcards.Count;
    var current = Model.Flashcards.ElementAtOrDefault(Model.CurrentIndex);
    var progress = total > 0 ? (double)(Model.CurrentIndex + 1) / total * 100 : 0;
}

<div class="fade-in-up">
    <div class="text-center mb-4">
        <h1 class="h4" style="letter-spacing: -0.02em;">@Model.SetTitle</h1>
        <p style="color: #787774; font-size: 0.875rem;">Thẻ @(Model.CurrentIndex + 1) / @total</p>
        <div class="progress-custom" style="max-width: 400px; margin: 0 auto;">
            <div class="progress-custom-bar" style="width: @progress%;"></div>
        </div>
    </div>

    @if (current != null)
    {
        <div class="flashcard-container" onclick="toggleFlip()">
            <div class="flashcard" id="flashcard">
                <div class="flashcard-front">
                    <span style="font-size: 2rem; font-weight: 600;">@current.FrontText</span>
                </div>
                <div class="flashcard-back">
                    <div>
                        <span style="font-size: 2rem; font-weight: 500;">@current.BackText</span>
                        <p style="color: #787774; margin-top: 16px;">Nhấn để lật thẻ</p>
                    </div>
                </div>
            </div>
        </div>

        <div class="d-flex justify-content-center gap-3 mt-4">
            <form asp-action="MarkLearned" asp-route-setId="@Model.SetId" asp-route-cardId="@current.Id" asp-route-learned="false" method="post" style="display: inline;">
                <button type="submit" class="btn-secondary-custom" style="border-color: #FDEBEC; color: #9F2F2D;">
                    <i class="ph ph-x-circle"></i> Chưa biết
                </button>
            </form>

            <form asp-action="MarkLearned" asp-route-setId="@Model.SetId" asp-route-cardId="@current.Id" asp-route-learned="true" method="post" style="display: inline;">
                <button type="submit" class="btn-secondary-custom" style="border-color: #EDF3EC; color: #346538;">
                    <i class="ph ph-check-circle"></i> Đã biết
                </button>
            </form>
        </div>

        <div class="d-flex justify-content-center gap-3 mt-3">
            @if (Model.CurrentIndex > 0)
            {
                <a href="/Study/@Model.SetId/Flashcard?index=@(Model.CurrentIndex - 1)" class="btn-secondary-custom">
                    <i class="ph ph-caret-left"></i> Trước
                </a>
            }
            @if (Model.CurrentIndex < total - 1)
            {
                <a href="/Study/@Model.SetId/Flashcard?index=@(Model.CurrentIndex + 1)" class="btn-primary-custom">
                    Sau <i class="ph ph-caret-right"></i>
                </a>
            }
            else
            {
                <form asp-action="Complete" asp-route-setId="@Model.SetId" method="post">
                    <button type="submit" class="btn-primary-custom">
                        <i class="ph ph-check"></i> Hoàn thành
                    </button>
                </form>
            }
        </div>
    }
    else
    {
        <div class="text-center py-5" style="color: #787774;">
            <i class="ph ph-cards" style="font-size: 3rem;"></i>
            <p class="mt-3">Không có thẻ nào để học.</p>
            <a href="/Study/@Model.SetId" class="btn-primary-custom">Quay lại</a>
        </div>
    }

    @if (TempData["Success"] != null)
    {
        <div class="alert text-center mt-4" style="background: #EDF3EC; color: #346538; border: none; border-radius: 8px;">
            @TempData["Success"]
        </div>
    }
</div>

@section Scripts {
    <script>
        // Keyboard navigation
        document.addEventListener('keydown', function(e) {
            if (e.key === ' ' || e.key === 'Enter') {
                e.preventDefault();
                toggleFlip();
            }
        });
    </script>
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 13: Add connection string and finalize

**Files:**
- Modify: `appsettings.json`

- [ ] **Step 1: Add connection string**

Edit `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=LTWNC-English;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

- [ ] **Step 2: Verify final build**

Run: `dotnet build`
Expected: Build succeeds with no errors.

- [ ] **Step 3: Run application**

Run: `dotnet run`
Expected: App starts and opens on localhost. Home page loads with design system visible.
