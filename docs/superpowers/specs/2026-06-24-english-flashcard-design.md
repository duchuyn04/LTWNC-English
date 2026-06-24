# English Flashcard Learning Website - Design Spec

## Overview

A community-based English learning website similar to Quizlet, featuring flashcard creation, sharing, and multiple study modes. Built with ASP.NET MVC (.NET 10.0), SQL Server, and a minimalist UI design.

**Target Audience:** Community sharing — users create, share, and learn from each other's flashcard sets.

**Scope:** MVP — Authentication + Flashcard CRUD + Flashcard study mode. Additional study modes (Quiz, Write, Match) added incrementally after MVP.

---

## Architecture

### Tech Stack

| Layer | Technology |
|-------|------------|
| Framework | ASP.NET MVC (.NET 10.0) |
| Database | SQL Server |
| ORM | Entity Framework Core (Code-First) |
| Authentication | ASP.NET Identity |
| Frontend | Razor Views + jQuery + Bootstrap 5 |
| Icons | Phosphor Icons (Bold) |

### 3-Layer Architecture

```
Controllers (Request/Response) → Services (Business Logic) → Repositories (Data Access) → EF Core → SQL Server
```

**Dependency Injection:**
- Controller injects `IXxxService`
- Service injects `IXxxRepository`
- Repository injects `AppDbContext`
- Registered as Scoped in `Program.cs`

### Project Structure

```
ltwnc/
├── Controllers/
│   ├── AccountController.cs      # Register, Login, Logout, Profile
│   ├── HomeController.cs         # Home page, Search public sets
│   ├── FlashcardSetController.cs # CRUD flashcard sets (own)
│   └── StudyController.cs        # Study modes
├── Services/
│   ├── IAccountService.cs
│   ├── AccountService.cs
│   ├── IFlashcardSetService.cs
│   ├── FlashcardSetService.cs
│   ├── IStudyService.cs
│   └── StudyService.cs
├── Repositories/
│   ├── IUserRepository.cs
│   ├── UserRepository.cs
│   ├── IFlashcardSetRepository.cs
│   ├── FlashcardSetRepository.cs
│   ├── IFlashcardRepository.cs
│   ├── FlashcardRepository.cs
│   ├── IStudySessionRepository.cs
│   └── StudySessionRepository.cs
├── Models/
│   ├── Entities/
│   │   ├── User.cs
│   │   ├── FlashcardSet.cs
│   │   ├── Flashcard.cs
│   │   ├── StudySession.cs
│   │   └── UserProgress.cs
│   └── ViewModels/
│       ├── Account/
│       ├── FlashcardSet/
│       └── Study/
├── Data/
│   └── AppDbContext.cs
└── Views/
    ├── Shared/
    │   ├── _Layout.cshtml
    │   └── _ValidationScriptsPartial.cshtml
    ├── Account/
    ├── Home/
    ├── FlashcardSet/
    └── Study/
```

---

## Data Model

### Entities

#### User (extends IdentityUser)
| Field | Type | Description |
|-------|------|-------------|
| Id | string (PK) | Auto-generated |
| UserName | string | Display name |
| Email | string | Login email |
| PasswordHash | string | Hashed password |
| AvatarUrl | string? | Profile image URL |
| CreatedAt | DateTime | Registration date |

#### FlashcardSet
| Field | Type | Description |
|-------|------|-------------|
| Id | int (PK) | Auto-increment |
| Title | string | Set name (max 200 chars) |
| Description | string? | Set description |
| UserId | string (FK) | Owner |
| IsPublic | bool | Visible to community |
| CreatedAt | DateTime | Creation date |
| UpdatedAt | DateTime | Last modified |

#### Flashcard
| Field | Type | Description |
|-------|------|-------------|
| Id | int (PK) | Auto-increment |
| FlashcardSetId | int (FK) | Parent set |
| FrontText | string | Front side (English word) |
| BackText | string | Back side (Vietnamese meaning) |
| OrderIndex | int | Display order |

#### StudySession
| Field | Type | Description |
|-------|------|-------------|
| Id | int (PK) | Auto-increment |
| UserId | string (FK) | Learner |
| FlashcardSetId | int (FK) | Set studied |
| StudyMode | enum | Flashcard, Quiz, Write, Match |
| Score | int? | Score (for quiz/write/match) |
| CompletedAt | DateTime | Session end time |

#### UserProgress
| Field | Type | Description |
|-------|------|-------------|
| Id | int (PK) | Auto-increment |
| UserId | string (FK) | User |
| FlashcardId | int (FK) | Card |
| IsLearned | bool | Marked as learned |
| LastReviewed | DateTime | Last review date |

### Relationships

```
User 1──* FlashcardSet 1──* Flashcard
User 1──* StudySession *──1 FlashcardSet
User 1──* UserProgress *──1 Flashcard
```

---

## Controllers & Routes

### AccountController
| Route | Method | Description |
|-------|--------|-------------|
| `/Account/Register` | GET/POST | Registration form |
| `/Account/Login` | GET/POST | Login form |
| `/Account/Logout` | POST | Logout |
| `/Account/Profile` | GET | User profile |

### HomeController
| Route | Method | Description |
|-------|--------|-------------|
| `/` | GET | Home page with public sets |
| `/Home/Search?q=...` | GET | Search public sets |

### FlashcardSetController `[Authorize]`
| Route | Method | Description |
|-------|--------|-------------|
| `/Set` | GET | My sets (dashboard) |
| `/Set/Create` | GET/POST | Create new set |
| `/Set/{id}` | GET | View set details |
| `/Set/{id}/Edit` | GET/POST | Edit set |
| `/Set/{id}/Delete` | POST | Delete set |
| `/Set/{id}/Cards/Create` | POST | Add card to set |
| `/Cards/{id}/Edit` | POST | Edit card |
| `/Cards/{id}/Delete` | POST | Delete card |

### StudyController `[Authorize]`
| Route | Method | Description |
|-------|--------|-------------|
| `/Study/{setId}` | GET | Choose study mode |
| `/Study/{setId}/Flashcard` | GET | Flashcard mode |
| `/Study/{setId}/Quiz` | GET | Quiz mode (post-MVP) |
| `/Study/{setId}/Write` | GET | Write mode (post-MVP) |
| `/Study/{setId}/Match` | GET | Match mode (post-MVP) |

### Authorization
- **Public:** Home page, view public sets
- **Authenticated:** All CRUD operations, all study modes

---

## UI Design System

### Color Palette (Light Theme)

| Element | Color | Hex |
|---------|-------|-----|
| Background | Warm Off-White | `#F7F6F3` |
| Card Surface | White | `#FFFFFF` |
| Border/Divider | Ultra-light gray | `#EAEAEA` |
| Text Primary | Off-black | `#111111` |
| Text Secondary | Muted gray | `#787774` |
| Accent Green | Pale Green (learned) | `#EDF3EC` / text `#346538` |
| Accent Red | Pale Red (not learned) | `#FDEBEC` / text `#9F2F2D` |
| Accent Blue | Pale Blue (tags/links) | `#E1F3FE` / text `#1F6C9F` |
| Accent Yellow | Pale Yellow (highlight) | `#FBF3DB` / text `#956400` |

### Typography

| Usage | Font | Notes |
|-------|------|-------|
| Headings | SF Pro Display, Helvetica Neue, sans-serif | Tight tracking (`-0.02em`) |
| Body | SF Pro Display, sans-serif | `line-height: 1.6` |
| Mono | JetBrains Mono, monospace | Order numbers, metadata |

### Components

**Cards:**
- `border: 1px solid #EAEAEA`
- `border-radius: 12px`
- `padding: 24px-40px`
- Hover: `box-shadow: 0 2px 8px rgba(0,0,0,0.04)` transition `200ms`

**Buttons:**
- Primary: `background: #111111`, `color: #FFFFFF`, `border-radius: 6px`, no shadow
- Secondary: `border: 1px solid #EAEAEA`, `background: #FFFFFF`
- Active: `scale(0.98)`

**Tags:**
- Pill-shaped (`border-radius: 9999px`)
- `text-xs`, uppercase, wide tracking
- Muted pastel backgrounds

**Flashcard:**
- Large card (min-height: 300px)
- 3D flip animation: `rotateY(180deg)` with `transform-style: preserve-3d`
- Click or button to flip
- Front: English word (large, centered)
- Back: Vietnamese meaning (large, centered)

### Animations

| Animation | Implementation |
|-----------|---------------|
| Scroll entry | `translateY(12px)` + `opacity: 0` → visible, `600ms`, `cubic-bezier(0.16, 1, 0.3, 1)` |
| Hover cards | `box-shadow` transition `200ms` |
| Button active | `transform: scale(0.98)` |
| Flashcard flip | CSS 3D transform, `0.6s` ease |
| Staggered list items | `animation-delay: calc(var(--index) * 80ms)` |

### Page Layouts

**Home Page:**
- Hero section: Search bar + "Tạo bộ thẻ mới" CTA
- Bento grid of public flashcard sets
- Each set card: Title, description, card count, author

**Dashboard (My Sets):**
- List/grid of user's flashcard sets
- Quick actions: Edit, Delete, Study
- "Tạo bộ thẻ mới" button

**Create/Edit Set:**
- Form: Title, Description, Public/Private toggle
- Dynamic card list: Add/remove cards with jQuery
- Each card: Front text + Back text inputs

**Study - Flashcard Mode:**
- Large centered flashcard
- Flip animation on click
- Navigation: Previous / Next
- Progress bar
- Buttons: "Đã biết" (green) / "Chưa biết" (red)

---

## Error Handling & Security

### Authentication
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- Password hashing: PBKDF2 (automatic)
- Cookie-based authentication
- `[Authorize]` attribute for protected routes

### Validation
- Client-side: jQuery Validation
- Server-side: Data Annotations + Service layer validation
- Anti-forgery tokens for all POST forms

### Error Handling
- Global exception handler in `Program.cs`
- Custom error page (`/Home/Error`)
- Validation errors return to form with Vietnamese messages

### Security
- CSRF: Anti-forgery tokens
- SQL Injection: EF Core parameterized queries
- XSS: Razor auto-encoding
- Password: Min 8 chars, 1 uppercase, 1 digit

---

## MVP Scope

### Phase 1 (MVP)
1. User registration and login
2. Create/Edit/Delete flashcard sets
3. Add/Edit/Delete flashcards within sets
4. Browse public flashcard sets
5. Study flashcard mode (flip cards, mark learned/not learned)

### Phase 2 (Post-MVP)
1. Quiz mode (multiple choice)
2. Write mode (type answer)
3. Match mode (drag and drop)
4. User profile with study statistics
5. Search with filters
6. Favorite/bookmark sets

---

## Database Migrations

Use EF Core Code-First:
1. Create `AppDbContext` with all DbSets
2. `Add-Migration InitialCreate`
3. `Update-Database`

---

## Package Dependencies

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />
```
