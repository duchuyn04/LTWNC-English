# Home Index Redesign Design

## Goal

Redesign the home index page as a service-style hybrid landing page for LTWNC English. The page should feel like a polished service/product landing page while keeping the actual product focus on flashcard-based English vocabulary learning.

## Scope

Redesign only the home index page and its page-specific styling.

Do not add new backend workflows. Existing search/public set data may still appear if useful, but the landing page does not need new database fields, new services, or a quote submission endpoint.

## Direction

Use a minimal, professional landing page layout with these sections:

1. Two-tier header
2. CTA quote strip
3. Split hero with stats
4. Four benefit cards
5. Three-column services grid
6. Product/app feature block
7. Software/module logos
8. Why choose us checklist
9. Three-step onboarding
10. Testimonials carousel-style static cards
11. Blog cards
12. Team gallery
13. Accreditation/quality block
14. Quote form CTA
15. Multi-column footer

The page can reduce item counts inside sections to stay readable. Prefer 3-4 items per section over long lists.

## Content Model

This is a hybrid service-style page, but all content should stay truthful to the app.

- Services mean app capabilities:
  - Create vocabulary sets
  - Study with flashcards
  - Track learning progress
- Quote/CTA means starting a learning path:
  - Authenticated users go to `/Set/Create`
  - Anonymous users go to `/Account/Register`
- Accreditation means quality promises, not fake external certificates.
- Software logos mean app modules or learning tools, not real third-party endorsements.
- Team gallery may use illustrative learning/team roles, not fabricated real people.

## Visual Style

Match the current app's clean Vietnamese learning-product feel:

- Light neutral background, restrained contrast, clear black text.
- Avoid heavy gradients, decorative blobs, and oversized marketing fluff.
- Cards should be compact, readable, and not nested.
- Use Phosphor icons already loaded by the layout.
- Keep section widths consistent with the existing Bootstrap container rhythm.
- Mobile layout must stack cleanly without text overlap.

## Header

The home page may use its own full-width landing header inside the page body instead of modifying the shared layout.

- Top tier: short trust/status line and small links.
- Main tier: brand, navigation anchors, and CTA.
- Avoid changing `_Layout.cshtml` unless needed.

## Hero

Split hero:

- Left: headline, supporting copy, primary CTA, secondary CTA.
- Right: app preview card showing a flashcard or learning dashboard mock.
- Stats row below or inside hero with 3 metrics.

Primary CTA:

- Logged-in: `/Set/Create`
- Logged-out: `/Account/Register`

Secondary CTA:

- Public set discovery/search area or `/Set` when logged in.

## Existing Public Sets

If `Model.PublicSets` has content, include a compact "popular public sets" area near the lower half of the page or inside the blog/resources section. Keep it secondary so it does not break the landing flow.

If search `q` is present, the page should still show relevant public sets clearly.

## Quote Form

The quote form is a CTA form, not a backend contact form.

- Fields can be visual only or use simple client-side behavior.
- Submit should navigate to the primary CTA URL.
- No new controller action or database table.

## Error Handling

- If no public sets exist, hide the public sets block or show one concise empty state.
- If user is anonymous, CTAs should not point to authenticated-only pages.
- If user is authenticated, CTAs should avoid sending them back to registration.

## Testing

- Build the project.
- Load `/` logged out and verify CTAs point to registration.
- Load `/` logged in and verify CTAs point to set creation or owned sets.
- Search with `/?q=test` and verify public results are still accessible.
- Check desktop and mobile widths for no overlap.
- Verify no new backend route is required for quote form.
