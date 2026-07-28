import { expect, test } from '@playwright/test';
import fs from 'fs';
import path from 'path';

const cssDirectory = path.resolve('../../wwwroot/css/admin');
const shellScriptPath = path.resolve('../../wwwroot/js/admin-shell.js');
const readAdminCss = () => ['shell.css', 'dashboard.css', 'components.css', 'responsive.css']
    .map(file => fs.readFileSync(path.join(cssDirectory, file), 'utf8'))
    .join('\n');

test.describe('Admin shell and shared controls', () => {
    test('mobile drawer supports pointer, keyboard, Escape, backdrop, and focus restoration', async ({ page }) => {
        await page.setViewportSize({ width: 375, height: 760 });
        await page.setContent(shellHarness(readAdminCss()));
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
        await page.setContent(controlHarness(readAdminCss()));

        const input = page.locator('#long-input');
        const select = page.locator('#long-select');
        await input.fill(longValue);

        await expect(input).toHaveValue(longValue);
        expect(await select.evaluate(element => getComputedStyle(element).textOverflow)).toBe('ellipsis');
        expect(await input.evaluate(element => getComputedStyle(element).textOverflow)).toBe('ellipsis');
        expect((await input.boundingBox())?.height).toBeGreaterThanOrEqual(44);
        expect((await select.boundingBox())?.height).toBeGreaterThanOrEqual(44);
        expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(360);
    });

    test('admin shell has no horizontal overflow at supported widths', async ({ page }) => {
        const adminCss = readAdminCss();
        for (const width of [360, 375, 768, 1024, 1440]) {
            await page.setViewportSize({ width, height: 800 });
            await page.setContent(shellHarness(adminCss));
            expect(await page.evaluate(() => document.documentElement.scrollWidth), `viewport ${width}px`)
                .toBeLessThanOrEqual(width);
        }
    });

    test('dashboard workbench prioritizes completion without overflowing mobile', async ({ page }) => {
        const adminCss = readAdminCss();
        await page.setViewportSize({ width: 1280, height: 800 });
        await page.setContent(dashboardHarness(adminCss));
        const completionWidth = (await page.locator('[data-kpi-index="3"]').boundingBox())?.width ?? 0;
        const sessionWidth = (await page.locator('[data-kpi-index="2"]').boundingBox())?.width ?? 0;
        expect(completionWidth).toBeGreaterThan(sessionWidth);

        await page.setViewportSize({ width: 320, height: 800 });
        expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(320);
        expect((await page.locator('[data-kpi-index="3"]').boundingBox())?.width)
            .toBeLessThanOrEqual(280);
    });

    test('content inventory uses compact rows on desktop and cards on mobile', async ({ page }) => {
        const adminCss = readAdminCss();
        await page.setViewportSize({ width: 1280, height: 800 });
        await page.setContent(contentTableHarness(adminCss));
        expect((await page.locator('.admin-content-table tbody tr').first().boundingBox())?.height)
            .toBeLessThan(80);

        await page.setViewportSize({ width: 375, height: 800 });
        expect(await page.locator('.admin-content-table tbody tr').first().evaluate(element =>
            getComputedStyle(element).display)).toBe('grid');
        expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(375);
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
                    <main class="admin-main"><div class="admin-kpi-grid">${'<article class="admin-kpi-card"><div class="admin-kpi-content"><h3>Chỉ số</h3><strong>12</strong></div></article>'.repeat(6)}</div></main>
                </div>
            </div>
        </body>`;
}

function dashboardHarness(adminCss: string) {
    const cards = [3, 2, 0, 1, 4, 5].map(index => `
        <article class="admin-kpi-card" data-kpi-index="${index}">
            <div class="admin-kpi-icon">●</div>
            <div class="admin-kpi-content"><h3>Chỉ số</h3><strong>68.4%</strong><p>Chi tiết</p></div>
            <a class="admin-kpi-action">Xem chi tiết</a>
        </article>`).join('');
    return `
        <style>${baseTokens()}${adminCss}</style>
        <body class="admin-body">
            <main class="admin-main">
                <section class="admin-kpi-section"><div class="admin-kpi-grid">${cards}</div></section>
            </main>
        </body>`;
}

function contentTableHarness(adminCss: string) {
    const row = `
        <tr>
            <td class="admin-content-table-title" data-label="Bộ thẻ"><a class="admin-text-link">Tiếng Anh ở quán cà phê</a><small class="admin-muted-line">#12</small></td>
            <td data-label="Chủ sở hữu">learner@example.com</td>
            <td data-label="Hiển thị">Công khai</td>
            <td data-label="Trạng thái"><span class="admin-status admin-status--success">Đang hoạt động</span></td>
            <td data-label="Số thẻ">5</td>
            <td data-label="Báo cáo"><span class="admin-muted-value">0</span></td>
            <td data-label="Cập nhật">24/07/2026</td>
            <td class="admin-content-table-action" data-label="Thao tác"><a class="admin-row-action">Xem chi tiết</a></td>
        </tr>`;
    return `
        <style>${baseTokens()}${adminCss}</style>
        <body class="admin-body">
            <main class="admin-main">
                <section class="admin-panel">
                    <div class="admin-table-wrapper admin-content-table-wrapper">
                        <table class="admin-table admin-content-table">
                            <thead><tr><th>Bộ thẻ</th><th>Chủ sở hữu</th><th>Hiển thị</th><th>Trạng thái</th><th>Số thẻ</th><th>Báo cáo</th><th>Cập nhật</th><th>Thao tác</th></tr></thead>
                            <tbody>${row.repeat(2)}</tbody>
                        </table>
                    </div>
                </section>
            </main>
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
