# Custom Cookie Auth Design (Replace ASP.NET Identity)

**Date:** 2026-07-16  
**Status:** Approved for planning  
**Goal:** Remove ASP.NET Identity entirely and replace it with a small custom user store, PBKDF2 password hashing, and ASP.NET Core cookie authentication while keeping existing string `UserId` business data and MVC UX.

## Context

LTWNC currently uses:

- Package `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- `AppDbContext : IdentityDbContext`
- `UserManager<IdentityUser>` / `SignInManager<IdentityUser>` in `AccountController` and most other controllers
- `IdentityUser?` navigation properties on several entities
- Business tables store `UserId` as **string** (Identity-style GUIDs)

The product only needs email/username + password register/login/logout, cookie session, and `[Authorize]`. Full Identity (roles tables, claims tables, token providers, UserManager API) is unnecessary coupling.

## Goals

1. Remove ASP.NET Identity package, `IdentityDbContext`, `UserManager`, `SignInManager`, and `AspNet*` tables.
2. Own `AppUser` entity in table `Users`.
3. Cookie authentication with claims (`NameIdentifier` = user id).
4. Controllers obtain current user id via `ICurrentUser` (not Identity).
5. Keep all business `UserId` columns as **string** (no int conversion).
6. Preserve UX: register, login (RememberMe 30d / else 1d), logout, login redirect paths.
7. Dev database strategy: **reset** (drop + recreate). No AspNetUsers data migration.

## Non-goals

- Roles, policies beyond authenticated/anonymous, 2FA, lockout, external OAuth
- Password reset via email
- JWT / SPA token auth
- Changing business `UserId` to `int`
- Migrating existing Identity users into the new table
- Multi-tenant auth

## Decisions

| Topic | Choice |
| --- | --- |
| Approach | Custom cookie auth (Approach 1) |
| Password API | Own PBKDF2 hasher (no Identity package) |
| User id type | `string` GUID on register |
| Existing users | Discard / reset DB |
| Controllers | `ICurrentUser` instead of `UserManager` |
| Entity navigations | Remove `IdentityUser?` navs; keep `UserId` string only |

## Architecture

```text
Browser
  │
  ▼
AccountController ──► IAuthService ──► AppDbContext (Users)
       │                    │
       │                    └── Cookie SignIn / SignOut
       │
Other Controllers ──► ICurrentUser ──► ClaimsPrincipal
       │
       └── I* domain services (unchanged UserId string)
```

Cookie authentication and `[Authorize]` remain ASP.NET Core features. Only the **Identity membership stack** is removed.

## Data model

### `AppUser` → table `Users`

| Property | Type | Notes |
| --- | --- | --- |
| `Id` | `string` | PK; `Guid.NewGuid().ToString()` on register |
| `UserName` | `string` | Required; unique index |
| `Email` | `string` | Required; unique index (normalize: trim + lower for lookup) |
| `PasswordHash` | `string` | Versioned PBKDF2 payload |
| `CreatedAt` | `DateTime` | UTC |

### Business entities

- Keep `UserId` string on `FlashcardSet`, `UserProgress`, `StudySession`, `UserStudySettings`, `UserAchievement`, `CardActionLog`, etc.
- **Remove** `IdentityUser? User` navigation properties and related `[ForeignKey]` attributes from:
  - `FlashcardSet`
  - `StudySession`
  - `UserProgress`
  - `UserStudySettings`
- Do **not** require EF FK from every business row to `Users` in the first migration (string ownership id is enough for this app). Optional FKs can be added later if needed.

### `AppDbContext`

```csharp
public class AppDbContext : DbContext
{
    public DbSet<AppUser> Users => Set<AppUser>();
    // existing DbSets unchanged
}
```

- Stop calling Identity `base.OnModelCreating` behavior for AspNet tables.
- Configure unique indexes on `Users.Email` and `Users.UserName`.

## Auth components

Place under `Services/Auth/`:

| Type | Responsibility |
| --- | --- |
| `IPasswordHasher` / `Pbkdf2PasswordHasher` | Hash password; verify password |
| `IAuthService` / `AuthService` | Register, login, logout |
| `ICurrentUser` / `CurrentUser` | Read `HttpContext.User` claims |

### Password hashing

- Algorithm: PBKDF2 (e.g. HMAC-SHA256) via `Microsoft.AspNetCore.Cryptography.KeyDerivation` (shared framework; **not** Identity EF package).
- Store a **versioned** string, e.g. `v1.{iterations}.{saltBase64}.{hashBase64}`.
- Verify: parse version → recompute → fixed-time compare.
- Unit-test: correct password verifies; wrong password fails.

### Password policy (match prior Identity UX)

- Minimum length 8
- Require digit, uppercase, lowercase
- Do **not** require non-alphanumeric (same as previous `Program.cs` Identity options)

### `IAuthService` surface

```csharp
Task<AuthResult> RegisterAsync(string userName, string email, string password);
Task<AuthResult> LoginAsync(string email, string password, bool rememberMe);
Task LogoutAsync();
```

`AuthResult`: success flag + error messages suitable for `ModelState` (no password in messages).

**Register**

1. Validate policy and non-empty fields.
2. Normalize email for uniqueness check.
3. Reject duplicate email or username.
4. Create `AppUser` with new GUID id and password hash.
5. Sign in cookie (persistent, ~1 day default for post-register — match current behavior of 1 day after register).

**Login**

1. Find user by normalized email.
2. Verify password; on failure return generic error: “Email hoặc mật khẩu không đúng.”
3. Sign in with claims; cookie lifetime: RememberMe → 30 days; else → 1 day; sliding expiration enabled.

**Logout**

- Cookie sign-out.

### Cookie claims

- `ClaimTypes.NameIdentifier` = `AppUser.Id` (**source of truth for UserId**)
- `ClaimTypes.Name` = `UserName`
- `ClaimTypes.Email` = `Email`

### `ICurrentUser`

```csharp
bool IsAuthenticated { get; }
string? UserId { get; }  // NameIdentifier
string? UserName { get; }
```

Implementation reads `IHttpContextAccessor.HttpContext?.User`. Registered scoped.

### `AccountController`

- Depends on `IAuthService` only (no Identity managers).
- Keep existing routes/views/ViewModels where fields still match (`RegisterViewModel`, `LoginViewModel`).
- Map `AuthResult` errors into `ModelState`.

### Other controllers

Replace:

```csharp
IdentityUser? user = await _userManager.GetUserAsync(User);
// use user.Id
```

with:

```csharp
string? userId = _currentUser.UserId;
if (userId == null) return Challenge(); // or existing null-user handling
```

Inject `ICurrentUser` instead of `UserManager<IdentityUser>`.

Keep `[Authorize]` / `[AllowAnonymous]` as today.

## Program.cs / packages

**Remove**

- `builder.Services.AddIdentity<...>()`
- `ConfigureApplicationCookie` that depends on Identity registration
- Package reference `Microsoft.AspNetCore.Identity.EntityFrameworkCore`

**Add**

```csharp
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
```

Keep:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

## Database strategy (dev reset)

Because existing Identity users are disposable:

1. Generate a new EF migration reflecting `DbContext` + `Users` and no Identity entities **or** reset migrations if the team prefers a single clean initial migration.
2. Document for developers:

```text
dotnet ef database drop --force
dotnet ef database update
```

3. No script to copy `AspNetUsers` → `Users`.

Production note: if production data ever exists later, this design does **not** cover Identity migration; that would be a separate project.

## Error handling

| Case | Behavior |
| --- | --- |
| Duplicate email/username | `AuthResult` failure → ModelState |
| Weak password | ModelState with clear rule messages |
| Bad login credentials | Generic Vietnamese message (no user enumeration) |
| Unauthorized page | Cookie middleware redirects to Login |
| Authorize action but missing NameIdentifier | Treat as unauthenticated → Challenge |

Never log raw passwords.

## Testing strategy

| Area | Change |
| --- | --- |
| New | `Pbkdf2PasswordHasher` unit tests |
| New | `AuthService` tests with EF InMemory (register/login/duplicate) |
| Controllers | Remove `UserManager` mocks; use claims principal and/or mock `ICurrentUser` |
| Copy / command tests | Stop creating `IdentityUser`; set `UserId` strings only |
| Packages | Tests project must not require Identity packages after main package removal |

Full suite must pass: `dotnet build` and `dotnet test`.

Manual smoke: register → login → `/Set` → logout → protected URL redirects to login.

## Documentation

Update `README.md`:

- Technology table: remove ASP.NET Identity; document custom cookie auth + `Users` table.
- Dev setup: drop DB + migrate after this change.
- Keep GoF / service structure docs unchanged except any Identity mentions.

## Rollout order

1. Add `AppUser`, hasher, auth services, `ICurrentUser`.
2. Change `AppDbContext` to `DbContext` + `DbSet<AppUser>`.
3. Remove Identity navigations from entities.
4. Wire cookie auth + DI in `Program.cs`; remove `AddIdentity`.
5. Rewrite `AccountController`.
6. Switch all controllers to `ICurrentUser`.
7. Remove Identity package from `ltwnc.csproj`.
8. EF migration + document DB reset.
9. Fix tests.
10. README.
11. Verify build/tests + smoke login.

## Success criteria

- [ ] No `Microsoft.AspNetCore.Identity*` package references required by the app for membership
- [ ] No `UserManager`, `SignInManager`, `IdentityUser`, `IdentityDbContext` in application code
- [ ] Register / login / logout work with cookie auth and `Users` table
- [ ] `[Authorize]` still protects routes
- [ ] Business services still receive string `UserId`
- [ ] `dotnet build` and `dotnet test` succeed
- [ ] README documents custom auth and dev DB reset

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Broken custom hash | Versioned format + unit tests |
| Half-migrated DB | Explicit drop + update for dev |
| Missed Identity reference | Grep + compile after package removal |
| Controller null user regressions | Same Challenge/Unauthorized paths as today |

## Out of scope follow-ups (explicit)

- Optional FK from business tables to `Users`
- Email confirmation / password reset
- Account lockout after failed attempts
- Admin roles
