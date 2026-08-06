# Lessons Playwright smoke

Thin browser smoke for Lessons (ticket 04).

Throwaway `wwwroot/preview/lessons-ui.html` was removed after tickets 01–04 (ticket 05).

## One command

```bash
cd tests/smoke
npm install
npx playwright install chromium
npm test
```

Playwright starts the app on `http://127.0.0.1:5055` with:

- `SMOKE_FIXTURES=1` (seed learner/admin/lesson/MCQ/writing; **does not** run full migrate — run `dotnet ef database update` first)
- Uses `appsettings` DefaultConnection unless `SMOKE_CONNECTION` is set

## Accounts (seeded)

| Role | Username | Password |
|------|----------|----------|
| Learner | `smoke_learner` | `SmokeTest1a` |
| Admin | `smoke_admin` | `SmokeTest1a` |

## What it checks

1. Learner login → `/Lessons` → open **Smoke Lesson** → **Ôn tập** → MCQ + writing → `2/2`
2. Admin login → `/Admin/Lessons` → **Câu hỏi** → forms visible

## Env

- `SMOKE_BASE_URL` — default `http://127.0.0.1:5055`
- `SMOKE_CONNECTION` — SQL connection string for smoke DB
- `CI=1` — do not reuse an already-running server
