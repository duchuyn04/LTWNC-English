# Thiết kế: Thay ASP.NET Identity bằng auth tự quản nhẹ

Ngày: 2026-07-21
Trạng thái: Đã duyệt thiết kế (brainstorming), chờ review spec

## 1. Bối cảnh & mục tiêu

Project hiện dùng `AddIdentityCore<IdentityUser>` + `AddEntityFrameworkStores<AppDbContext>` (`Program.cs:46-66`), cookie auth, 1 role `Admin`. Ngườoi dùng thấy Identity **quá nặng/rườm rà**: `UserManager`/`SignInManager` khó mock khi test, schema `AspNet*` 7 bảng thừa so với nhu cầu thực tế.

Mục tiêu: thay toàn bộ Identity bằng auth tự quản tối giản — 1 bảng user, 1 service auth — trong khi tái dùng các primitive bảo mật đã kiểm chứng của ASP.NET (`PasswordHasher<T>`, cookie authentication middleware).

Quyết định đã chốt với ngườoi dùng:

- Bỏ hẳn `UserManager`/`SignInManager`/Identity stores (phương án custom auth nhẹ).
- Bảng roles bị loại bỏ; phân quyền admin bằng cột `IsAdmin` trên bảng user.
- **Reset dữ liệu auth**: drop bảng `AspNet*`, user đăng ký lại từ đầu. Không migrate mật khẩu cũ.
- Bỏ hẳn cơ chế `AdminBootstrap` (xóa `AdminRoleBootstrapper`, config `AdminBootstrap:*`). Admin đầu tiên set tay bằng SQL.

### Phạm vi tính năng phải giữ lại

- Lockout: sai password 5 lần → khóa 15 phút; admin khóa/mở khóa vĩnh viễn user; thu hồi phiên qua security stamp.
- Remember-me: cookie 1 ngày / 30 ngày; sliding expiration; validate principal mỗi request.
- Đăng ký email/username theo `UsernamePolicy`; tự động tạo `UserProfile`; auto sign-in sau đăng ký.
- Đổi password ở Profile; đổi password rotate security stamp.
- Phân quyền `/Admin` (policy `AdminAreaAccess`), redirect admin sau login, audit đăng nhập admin.
- AJAX trả 401 thay vì redirect; truy cập `/Admin` bị cấm trả 403.

### Không tái hiện (hiện tại cũng không dùng)

Password reset, email confirmation, 2FA, external login, token providers.

## 2. Kiến trúc

### 2.1. Thành phần mới

**`Models/Entities/AppUser.cs`** — entity auth duy nhất:

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | string (GUID) | Giữ dạng GUID string để không phải đổi kiểu cột `UserId` ở `UserProfile` và các bảng FK khác |
| `Email`, `NormalizedEmail` | string | Unique filtered index trên `NormalizedEmail` (giữ tinh thần `EmailIndex` hiện tại ở `AppDbContext.cs:41-45`) |
| `UserName`, `NormalizedUserName` | string | |
| `PasswordHash` | string | Format của `PasswordHasher<T>` |
| `SecurityStamp` | string | Rotate khi đổi password/username, admin lock/unlock |
| `LockoutEnd` | DateTimeOffset? | Lockout tạm 15 phút hoặc khóa vĩnh viễn (ngày tương lai xa) do admin |
| `AccessFailedCount` | int | Reset khi login thành công hoặc khi bị lockout |
| `IsAdmin` | bool | Thay role `Admin` |

**`Services/Auth/IAuthService.cs` + `AuthService.cs`** — thay `UserManager` + `SignInManager`:

- `RegisterAsync(email, userName, password)` → tạo `AppUser` + `UserProfile` trong cùng transaction → auto sign-in (cookie 1 ngày).
- `ValidateLoginAsync(email, password)` → kiểm lockout, verify password, cộng `AccessFailedCount`, lockout 5 lần/15 phút.
- `SignInAsync(user, rememberMe)` / `SignOutAsync()` → phát/thu hồi cookie với claims `NameIdentifier`, `Name`, `IsAdmin`, security stamp.
- `ChangePasswordAsync(userId, current, new)` → verify + hash mới + rotate stamp.
- `SetLockoutAsync(userId, locked)` (cho admin) → set/clear `LockoutEnd` + rotate stamp.
- `RefreshSignInAsync(user)` → giữ phiên hiện tại sau khi đổi password/profile.

Hash password dùng `PasswordHasher<AppUser>` từ package `Microsoft.Extensions.Identity.Core` (không kéo theo Identity stores).

**Kết quả trả về**: `AuthResult { Succeeded, Errors[] }` thay `IdentityResult`; controller map sang ModelState, giữ nguyên thông điệp hiển thị hiện tại.

**Interface `IAuthService`** để unit test mock gọn (thay việc mock `UserManager` qua `IUserStore`).

### 2.2. Thành phần thay đổi

- `Data/AppDbContext.cs`: `IdentityDbContext<IdentityUser>` → `DbContext` + `DbSet<AppUser>`; cấu hình `UserProfile → AppUser` FK (cascade delete giữ nguyên).
- `Controllers/AccountController.cs`: dùng `IAuthService` thay `UserManager`/`SignInManager`; password policy (≥8 ký tự, digit, upper, lower — hiện ở Identity options) chuyển thành validate tường minh.
- `Controllers/ProfileController.cs`, `Services/Profiles/ProfileService.cs`: đổi password/refresh sign-in qua `IAuthService`.
- `Services/AdminUsers/AdminUserAccountService.cs`: lock/unlock, liệt kê admin qua `AppDbContext` + `IAuthService` (`IsAdmin` thay `IsInRoleAsync`). Giữ `AdminUserLockCoordinator`.
- `Services/Auth/AdminRoleBootstrapper.cs`: **xóa**; gỡ block bootstrap trong `Program.cs:298-307` và tham chiếu config `AdminBootstrap:*`.
- `Program.cs`: gỡ `AddIdentityCore`/`.AddRoles`/`.AddEntityFrameworkStores`/`.AddSignInManager`/`.AddDefaultTokenProviders`; thay bằng `AddAuthentication().AddCookie()` + đăng ký `IAuthService`, `PasswordHasher<AppUser>`.
- `Areas/Admin/AdminAreaAuthorizationConvention.cs`: giữ nguyên (policy-based, không đụng Identity trực tiếp).

### 2.3. Thành phần KHÔNG đổi

- `ICurrentUser`/`CurrentUser` — claims phát ra giữ y hệt (`NameIdentifier`, `Name`).
- Mọi `[Authorize]` trên controllers; rate limiting `auth`/`ai`/`uploads` (key theo `ClaimTypes.NameIdentifier`).
- `UserProfile` và các bảng FK `UserId` khác (chỉ đổi target FK sang `AppUsers`).
- Views Account/Profile; form login (integration tests login qua form vẫn chạy).

### 2.4. Cookie & phân quyền admin

`AddAuthentication().AddCookie()` với behavior y hệt hiện tại:

- `LoginPath=/Account/Login`, `LogoutPath=/Account/Logout`.
- Expire 1 ngày (mặc định/sau register) / 30 ngày khi remember-me; sliding expiration.
- AJAX request trả 401 thay vì redirect; truy cập `/Admin` không đủ quyền trả 403 (giữ nguyên logic events hiện tại ở `Program.cs:76-141`).
- `OnValidatePrincipal`: load user mỗi request, reject nếu user bị xóa, đang bị lock (`LockoutEnd` trong tương lai), hoặc security stamp trong cookie ≠ DB (tương đương `SecurityStampValidator` với `ValidationInterval = Zero` hiện tại).
- Policy `AdminAreaAccess` đổi từ `RequireRole("Admin")` sang `RequireClaim("IsAdmin", "true")`; `AdminRoleBootstrapper.AdminRole` và mọi tham chiếu role bị xóa.

## 3. Hành vi nghiệp vụ tái hiện (port từ Identity)

- **Register**: validate `UsernamePolicy` + password policy → tạo `AppUser` + `UserProfile` (transaction) → auto sign-in cookie 1 ngày. Lỗi trùng email/username trả thông điệp giữ nguyên.
- **Login**: tìm theo `NormalizedEmail`. Đang lockout → từ chối, thông điệp lockout giữ nguyên. Sai password → `AccessFailedCount++`; đến 5 → `LockoutEnd = now + 15 phút`, reset count. Đúng → reset count, phát cookie 1 ngày / 30 ngày. Admin đăng nhập → redirect `/Admin` + ghi audit (giữ nguyên).
- **Security stamp mỗi request**: đổi password, đổi username, admin lock/unlock đều rotate stamp → mọi phiên cũ chết ngay ở request kế tiếp.
- **Admin lock/unlock**: set `LockoutEnd` xa trong tương lai / clear + rotate stamp, đi qua `AdminUserLockCoordinator` như hiện tại.
- **Đổi password**: verify password cũ → hash mới + rotate stamp → `RefreshSignInAsync` giữ phiên hiện tại.
- **Tạo admin đầu tiên**: thủ công bằng SQL, ví dụ `UPDATE AppUsers SET IsAdmin = 1 WHERE NormalizedEmail = 'ADMIN@EXAMPLE.COM'`. Hướng dẫn ghi vào README.

## 4. Migration & dữ liệu

1 migration EF duy nhất:

1. Tạo bảng `AppUsers` (các cột mục 2.1, unique filtered index `NormalizedEmail`).
2. Drop 7 bảng `AspNet*` (AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens, AspNetRoleClaims). **Dữ liệu auth bị reset — đã chốt với ngườoi dùng.**
3. Đổi FK `UserProfile.UserId` (và mọi FK khác trỏ tới `AspNetUsers`, nếu rà soát phát hiện thêm) sang `AppUsers`.
4. `ltwnc.csproj`: gỡ `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, thêm `Microsoft.Extensions.Identity.Core`.

Lưu ý lịch sử: repo từng có migration `ReplaceIdentityWithAppUsers` rồi `RestoreIdentityAuth` — lần này xóa luôn cặp migration đó khỏi chuỗi nếu việc squash được quyết định ở plan; mặc định giữ lịch sử migration nguyên vẹn và chỉ thêm 1 migration mới.

## 5. Chiến lược tests

- `tests/ltwnc.Tests/Infrastructure/AdminWebApplicationFactory.cs`: seed user bằng `AppDbContext` + `PasswordHasher<AppUser>` trực tiếp (thay `UserManager`/`RoleManager`); helper login qua form `/Account/Login` giữ nguyên → các integration test (AdminLoginFlow, AdminUserAccount, AdminContentModeration, AdminExport, AdminDashboardKpi, AdminGlobalSearch, AdminEnglishMission, ContentReport, StaleAuthenticationCookie) gần như không đổi.
- Viết lại unit tests đang mock `UserManager`: `AccountControllerTests`, `ProfileControllerTests`, `ProfileServiceTests` — mock `IAuthService`. Xóa `AdminRoleBootstrapperTests`.
- `StaleAuthenticationCookieTests`: viết lại kiểm chứng đổi security stamp trong DB → request tiếp theo bị đăng xuất.
- Thêm unit test cho `AuthService`: lockout 5 lần/15 phút, reset count khi login đúng, rotate stamp khi đổi password.

## 6. Thứ tự triển khai dự kiến

1. Entity `AppUser` + đổi `AppDbContext` + migration.
2. `IAuthService`/`AuthService` + `AuthResult`.
3. Rewire `AccountController`, `ProfileController`, `ProfileService`, `AdminUserAccountService`.
4. Cookie config + policy `AdminAreaAccess` + gỡ bootstrap trong `Program.cs`.
5. Tests: factory → integration → unit.
6. Dọn package, xóa `AdminRoleBootstrapper`, cập nhật README (cách set admin thủ công).

## 7. Rủi ro & giảm thiểu

- **Viết lại tests là khối lượng lớn nhất** (UserManager hiện xuất hiện 123 lần, phần lớn trong tests) — giảm thiểu bằng `IAuthService` gọn và factory seed trực tiếp.
- **Tự chịu trách nhiệm lockout/security stamp** — giảm thiểu bằng port nguyên logic hiện có và test `StaleAuthenticationCookieTests`.
- **Quên set admin sau reset DB** → không vào được `/Admin` — giảm thiểu bằng hướng dẫn SQL trong README.
- **Một số bảng FK tới user có thể bị sót** — rà soát toàn bộ `UserId` FK trong migration và khi build.
