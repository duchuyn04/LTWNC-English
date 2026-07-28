import { test, expect } from '@playwright/test';

test.describe('Unified Flashcard Editor', () => {
    test.beforeEach(async ({ page }) => {
        await page.goto('/flashcardset/editor');
    });

    test('creates a set and saves a card', async ({ page }) => {
        await page.fill('#set-title', 'E2E Test Set');
        await page.fill('.flashcard-card:first-child .input-front', 'hello');
        await page.fill('.flashcard-card:first-child .input-back', 'xin chào');
        await page.keyboard.press('Tab');

        await expect(page.locator('#save-status')).toHaveText('Đã lưu', { timeout: 5000 });
        await expect(page.locator('.flashcard-card:first-child')).toHaveAttribute('data-id', /^(?!new-).+/);
    });

    test('expands and collapses cards', async ({ page }) => {
        await page.fill('#set-title', 'Expand Test');
        await page.fill('.flashcard-card:first-child .input-front', 'term');
        await page.fill('.flashcard-card:first-child .input-back', 'def');
        await page.click('.flashcard-card:first-child .btn-toggle');
        await expect(page.locator('.flashcard-card:first-child')).toHaveClass(/collapsed/);
        await page.click('.flashcard-card:first-child');
        await expect(page.locator('.flashcard-card:first-child')).toHaveClass(/expanded/);
    });

    test('imports cards from a CSV file', async ({ page }) => {
        await page.fill('#set-title', 'Import Test');
        await page.click('#btn-import');
        await page.setInputFiles('#import-file', {
            name: 'cards.csv',
            mimeType: 'text/csv',
            buffer: Buffer.from(
                'Thuật ngữ,ĐỊNH NGHĨA,IPA,LOẠI TỪ,VÍ DỤ TIẾNG ANH,NGHĨA VÍ DỤ TIẾNG VIỆT\n' +
                'apple,quả táo,/ˈæp.əl/,noun,I ate an apple.,Tôi đã ăn một quả táo.\n' +
                'banana,quả chuối,/bəˈnɑː.nə/,noun,The banana is ripe.,Quả chuối đã chín.\n',
                'utf8'
            )
        });
        await page.click('#btn-import-confirm');
        await expect(page.locator('.flashcard-card')).toHaveCount(2);
    });
});
