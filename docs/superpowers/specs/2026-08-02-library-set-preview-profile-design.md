# Library set preview and author profile design

## Goal

From the public `/Library` page, users can:

1. Open a public set and inspect the complete card content inline on `/Set/{id}`.
2. Open the set author's public profile from both the library card and the set detail page.

The change preserves the existing MVC routes, access rules, copy/study actions, and visual language.

## Current context

- `LibraryController` and `PublicLibraryService` already expose public, active sets.
- Library cards already contain the author's username in `AuthorName`.
- `/Set/{id}` already loads accessible sets with ordered cards and renders front/back previews.
- Public profiles use the named `PublicProfile` route at `/{username}`.
- `FlashcardViewModel` already contains all fields needed for the preview.

## Design

### Data flow

- Keep `/Library` on the existing `PublicLibraryResult` flow. Use the existing `AuthorName` username to generate the named `PublicProfile` route.
- Keep `/Set/{id}` as the only set detail route. After `GetAccessibleSetWithCardsAsync` confirms access, resolve the owner's username through the existing `IAuthService` and map it to a new `SetDetailViewModel.AuthorUsername` property.
- Reuse the existing `FlashcardViewModel` fields. Do not add database columns, migrations, APIs, or dependencies.
- Keep access checks before owner lookup. Public active sets and the current user's own sets remain accessible; inaccessible or missing sets remain `404`.
- If the owner record cannot be resolved, render a plain fallback author label without a broken profile link.

### UI behavior

- On each library set card, make the avatar and author name one accessible link to `/{username}`.
- On the set detail hero, show the author's initials and username with a link to the same profile route. Existing management, duplicate, copy, report, and study actions remain unchanged.
- Render every card as a semantic `article` with:
  - front text;
  - back text;
  - pronunciation when present;
  - part of speech when present;
  - example sentence and example meaning when present;
  - synonyms when present;
  - image when present, preferring `UploadedImagePath` over `ImageUrl`.
- Omit empty optional fields rather than rendering empty labels.
- Use a two-column front/back layout on desktop, with supporting metadata below. Collapse to one column below the existing mobile breakpoint.
- Do not expose `IsStarred`, since it is not part of the public preview.
- Use existing colors, radii, focus treatment, and responsive conventions in `library.css` and `set-management.css`.

### Privacy and failure behavior

- The existing profile route decides whether a profile is public or private. No profile visibility rules are duplicated in the library or set detail views.
- A private set belonging to another user is rejected before any card or author data is rendered.
- Missing optional card content produces no empty row.
- Missing set or denied access keeps the existing `404` behavior.
- Server-side rendering means no new loading state or client-side error flow is needed.

## Testing

Follow red-green-refactor:

1. Add failing view tests for the library profile link, set detail profile link, complete card fields, image fallback, and omission of empty optional fields.
2. Run the focused tests and verify they fail for the missing feature.
3. Implement the smallest MVC/view/CSS changes.
4. Run the focused tests, then the full test project and application build.

## Acceptance criteria

- A user can click an author avatar/name in `/Library` and reach that author's profile.
- A user can click the author link on `/Set/{id}` and reach the same profile.
- `/Set/{id}` shows all populated public card fields and images.
- Empty optional card fields do not leave blank labels or broken layout.
- Public/private/moderation access behavior is unchanged.
- Existing actions continue to work.
- Focus states and mobile layout remain usable.
- No new migration, package, API endpoint, or JavaScript dependency is introduced.

## Out of scope

- A separate library preview route.
- AJAX, modal, accordion, or card-detail endpoint.
- Editing cards from the public preview.
- Public display of per-card starred state.
- Changes to profile privacy settings or URL structure.
