import { expect, test } from '@playwright/test';
import fs from 'fs';
import path from 'path';

const cssPath = path.resolve('../../wwwroot/css/admin-dashboard.css');
const shellScriptPath = path.resolve('../../wwwroot/js/admin-shell.js');

test.describe('Admin shell and shared controls', () => {
    test('mobile drawer supports pointer, keyboard, Escape, backdrop, and focus restoration', async ({ page }) => {
        await page.setViewportSize({ width: 375, height: 760 });
        await page.setContent(shellHarness(fs.readFileSync(cssPath, 'utf8')));
        await page.addScriptTag({ path: shellScriptPath });

        const toggle = page.locator('[data-admin-menu-toggle]');
        const panel = page.locator('[data-admin-menu-panel]');
        const backdrop = page.locator('[data-admin-menu-backdrop]');

        await toggle.focus();
        await page.keyboard.press('Enter');
        await expect(toggle).toHaveAttribute('aria-expanded', 'true');
        await expect(panel).toHaveClass(/is-open/);
        await expect(page.locator('.admin-navigation a').first()).toBeFocused();

        await page.keyboard.press('Escape');
        await expect(toggle).toHaveAttribute('aria-expanded', 'false');
        await expect(toggle).toBeFocused();

        await toggle.click();
        await expect(backdrop).toBeVisible();
        await backdrop.click({ position: { x: 370, y: 20 } });
        await expect(toggle).toHaveAttribute('aria-expanded', 'false');
        await expect(toggle).toBeFocused();
    });

    test('long input values stay intact and long selects do not expand the page', async ({ page }) => {
        const longValue = 'admin-with-a-very-long-email-address-and-reference-code@example.com';
        await page.setViewportSize({ width: 360, height: 740 });
        await page.setContent(controlHarness(fs.readFileSync(cssPath, 'utf8')));

        const input = page.locator('#long-input');
        const select = page.locator('#long-select');
        await input.fill(longValue);

        await expect(input).toHaveValue(longValue);
        expect(await select.evaluate(element => getComputedStyle(element).textOverflow)).toBe('ellipsis');
        expect(await input.evaluate(element => getComputedStyle(element).textOverflow)).toBe('clip');
        expect((await input.boundingBox())?.height).toBeGreaterThanOrEqual(44);
        expect((await select.boundingBox())?.height).toBeGreaterThanOrEqual(44);
        expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(360);
    });

    test('admin shell has no horizontal overflow at supported widths', async ({ page }) => {
        const adminCss = fs.readFileSync(cssPath, 'utf8');
        for (const width of [360, 375, 768, 1024, 1440]) {
            await page.setViewportSize({ width, height: 800 });
            await page.setContent(shellHarness(adminCss));
            expect(await page.evaluate(() => document.documentElement.scrollWidth), `viewport ${width}px`)
                .toBeLessThanOrEqual(width);
        }
    });
});

function shellHarness(adminCss: string) {
    return `
        <style>${baseTokens()}${adminCss}</style>
        <body class="admin-body">
            <div class="admin-shell">
                <header class="admin-mobile-bar">
                    <a class="admin-mobile-brand" href="/Admin"><span>LT</span><strong>LTWNC Admin</strong></a>
                    <button type="button" data-admin-menu-toggle aria-expanded="false" aria-controls="admin-sidebar">Menu</button>
                </header>
                <aside id="admin-sidebar" class="admin-sidebar" data-admin-menu-panel>
                    <a class="admin-brand" href="/Admin"><span>LT</span><strong>LTWNC</strong><small>ADMIN</small></a>
                    <nav class="admin-navigation">
                        <a href="/Admin">Tổng quan</a>
                        <a href="/Admin/Users">Người dùng có tên hiển thị rất dài cần được thu gọn an toàn</a>
                    </nav>
                </aside>
                <button class="admin-sidebar-backdrop" type="button" data-admin-menu-backdrop hidden aria-label="Đóng menu"></button>
                <div class="admin-workspace">
                    <header class="admin-topbar"><div><h1>Tổng quan</h1></div></header>
                    <main class="admin-main"><div class="admin-kpi-grid">${'<article class="admin-kpi-card"><div class="admin-kpi-content"><h2>Chỉ số</h2><strong>12</strong></div></article>'.repeat(6)}</div></main>
                </div>
            </div>
        </body>`;
}

function controlHarness(adminCss: string) {
    return `
        <style>${baseTokens()}${adminCss}</style>
        <body class="admin-body">
            <main class="admin-main">
                <form class="admin-filter-bar">
                    <label class="admin-filter-field" for="long-input">
                        <span>Email hoặc mã</span>
                        <input id="long-input" type="text" placeholder="Email hoặc mã" />
                    </label>
                    <label class="admin-filter-field" for="long-select">
                        <span>Trạng thái</span>
                        <select id="long-select">
                            <option>Một lựa chọn có nội dung rất dài nhưng không được làm lệch hoặc mở rộng control</option>
                        </select>
                    </label>
                </form>
            </main>
        </body>`;
}

function baseTokens() {
    return `
        :root {
            --paper: #f7f3e9; --ink: #293226; --forest: #20392d; --moss: #44634e;
            --brass: #c79636; --surface: #fffdf7; --line: #ddd6c7; --muted: #6d756c;
            --radius-control: 8px; --duration-fast: 120ms; --ease-out: ease-out;
        }
        * { box-sizing: border-box; }
        html, body { margin: 0; }
    `;
}
