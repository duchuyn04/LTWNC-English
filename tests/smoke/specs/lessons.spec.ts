import { test, expect, type Page } from '@playwright/test';

const LEARNER = { user: 'smoke_learner', pass: 'SmokeTest1a' };
const ADMIN = { user: 'smoke_admin', pass: 'SmokeTest1a' };
const LESSON_TITLE = 'Smoke Lesson';

async function login(page: Page, user: string, pass: string) {
  await page.goto('/Account/Login');
  await page.locator('#Username').fill(user);
  await page.locator('#Password').fill(pass);
  await page.getByRole('button', { name: /đăng nhập/i }).click();
  await expect(page).not.toHaveURL(/\/Account\/Login/i);
}

test.describe.configure({ mode: 'serial' });

test('learner opens lesson and completes practice (MCQ + writing)', async ({ page }) => {
  await login(page, LEARNER.user, LEARNER.pass);

  await page.goto('/Lessons');
  await expect(page.getByRole('heading', { name: 'Danh sách bài học' })).toBeVisible();
  await expect(page.getByRole('heading', { name: LESSON_TITLE })).toBeVisible();

  await page.getByRole('link', { name: new RegExp(LESSON_TITLE) }).click();
  await expect(page).toHaveURL(/\/Lessons\/\d+/);
  await expect(page.getByRole('heading', { name: LESSON_TITLE })).toBeVisible();
  await expect(page.getByText('Body for smoke lesson')).toBeVisible();

  const practice = page.getByRole('link', { name: 'Ôn tập' });
  await expect(practice).toBeVisible();
  await practice.click();
  await expect(page).toHaveURL(/\/Lessons\/\d+\/Practice/);
  await expect(page.getByText('Ôn tập')).toBeVisible();

  // MCQ first (sort 1)
  await expect(page.getByText('Smoke MCQ', { exact: false })).toBeVisible();
  await page.getByRole('button', { name: /right/i }).click();
  await expect(page.getByText('Đúng.')).toBeVisible();
  await page.getByRole('button', { name: 'Câu tiếp' }).click();

  // Writing second
  await expect(page.getByText('Smoke writing', { exact: false })).toBeVisible();
  await page.locator('#WrittenAnswer').fill('  WORKS ');
  await page.getByRole('button', { name: 'Kiểm tra' }).click();
  await expect(page.getByText('Đúng.')).toBeVisible();
  await page.getByRole('button', { name: 'Câu tiếp' }).click();

  await expect(page.getByText('Hoàn thành')).toBeVisible();
  await expect(page.getByText('2/2')).toBeVisible();
});

test('admin opens lessons list and questions page', async ({ page }) => {
  await login(page, ADMIN.user, ADMIN.pass);

  await page.goto('/Admin/Lessons');
  await expect(page.getByRole('heading', { name: /bài học/i }).first()).toBeVisible();
  await expect(page.getByText(LESSON_TITLE)).toBeVisible();

  await page.getByRole('link', { name: 'Câu hỏi' }).first().click();
  await expect(page).toHaveURL(/\/Admin\/Lessons\/\d+\/Questions/);
  await expect(page.getByRole('heading', { name: LESSON_TITLE })).toBeVisible();
  await expect(page.getByText('Thêm câu trắc nghiệm')).toBeVisible();
  await expect(page.getByText('Thêm câu viết')).toBeVisible();
  await expect(page.getByText('Smoke MCQ', { exact: false })).toBeVisible();
});
