import { expect, test } from '@playwright/test';
import fs from 'fs';
import path from 'path';

test.describe('Admin dashboard live polling', () => {
    test('refreshes on interval without overlapping requests', async ({ page }) => {
        let requestCount = 0;
        let releaseFirstResponse: (() => void) | null = null;

        await page.route('/Admin/Snapshot?days=30', async route => {
            requestCount += 1;
            if (requestCount === 1) {
                await new Promise<void>(resolve => {
                    releaseFirstResponse = resolve;
                });
            }

            await route.fulfill({
                contentType: 'application/json',
                body: JSON.stringify(snapshotPayload('Không có cảnh báo'))
            });
        });
        await loadDashboardHarness(page);

        await page.evaluate(() => window.AdminDashboardLive.start({ intervalMs: 50 }));
        await expect.poll(() => requestCount).toBeGreaterThanOrEqual(1);
        await page.waitForTimeout(140);
        expect(requestCount).toBe(1);

        if (releaseFirstResponse != null) {
            releaseFirstResponse();
        }
        await expect.poll(() => requestCount).toBeGreaterThanOrEqual(2);
        await expect(page.locator('[data-dashboard-live-status]')).toHaveText('Cập nhật 12:00');

        await page.evaluate(() => window.AdminDashboardLive.stop());
    });

    test('pauses while hidden, refreshes when visible, and stops on pagehide', async ({ page }) => {
        let requestCount = 0;
        await page.route('/Admin/Snapshot?days=30', async route => {
            requestCount += 1;
            await route.fulfill({
                contentType: 'application/json',
                body: JSON.stringify(snapshotPayload('Đang ổn định'))
            });
        });
        await loadDashboardHarness(page);

        await page.evaluate(() => {
            window.__dashboardHidden = true;
            Object.defineProperty(document, 'hidden', {
                configurable: true,
                get: () => window.__dashboardHidden
            });
            window.AdminDashboardLive.start({ intervalMs: 50 });
        });
        await page.waitForTimeout(100);
        expect(requestCount).toBe(0);

        await page.evaluate(() => {
            window.__dashboardHidden = false;
            document.dispatchEvent(new Event('visibilitychange'));
        });
        await expect.poll(() => requestCount).toBeGreaterThanOrEqual(1);

        await page.evaluate(() => window.dispatchEvent(new Event('pagehide')));
        const requestCountAfterPageHide = requestCount;
        await page.waitForTimeout(120);
        expect(requestCount).toBe(requestCountAfterPageHide);
    });

    test('keeps the last snapshot and its server-rendered CTA when refresh fails', async ({ page }) => {
        let requestCount = 0;
        await page.route('/Admin/Snapshot?days=30', async route => {
            requestCount += 1;
            if (requestCount === 1) {
                await route.fulfill({
                    contentType: 'application/json',
                    body: JSON.stringify(snapshotPayload('Đang ổn định'))
                });
                return;
            }

            await route.fulfill({ status: 503, body: 'Unavailable' });
        });
        await loadDashboardHarness(page);

        await page.evaluate(() => window.AdminDashboardLive.start({ intervalMs: 50 }));
        await expect(page.locator('[data-kpi-value]')).toHaveText('12');
        await expect.poll(() => requestCount).toBeGreaterThanOrEqual(2);
        await expect(page.locator('[data-dashboard-live-status]'))
            .toHaveText('Không thể cập nhật · đang hiển thị số liệu lúc 12:00');
        await expect(page.locator('[data-kpi-value]')).toHaveText('12');
        await expect(page.locator('[data-kpi-action]')).toHaveAttribute('href', '/Admin/Learning');

        await page.evaluate(() => window.AdminDashboardLive.stop());
    });

    test('keeps keyboard focus visible at 360px and respects reduced motion', async ({ page }) => {
        const adminCss = fs.readFileSync(path.resolve('../../wwwroot/css/admin-dashboard.css'), 'utf8');
        await page.setViewportSize({ width: 360, height: 740 });
        await page.emulateMedia({ reducedMotion: 'reduce' });
        await page.setContent(`
            <style>
                :root {
                    --paper: #f7f3e9;
                    --ink: #293226;
                    --brass: #c79636;
                    --surface: #fffdf7;
                    --line: #ddd6c7;
                    --radius-control: 8px;
                    --duration-fast: 120ms;
                    --ease-out: ease-out;
                }
                body {
                    margin: 0;
                }
                ${adminCss}
            </style>
            <body class="admin-body">
                <a class="admin-skip-link" href="#admin-main">Bỏ qua điều hướng</a>
                <div class="admin-shell">
                    <aside class="admin-sidebar" aria-label="Điều hướng quản trị">
                        <nav class="admin-navigation">
                            <a href="/Admin">Tổng quan</a>
                            <a href="/Admin/Users">Người dùng</a>
                        </nav>
                    </aside>
                    <div class="admin-workspace">
                        <header class="admin-topbar">
                            <form class="admin-global-search" method="get" action="/Admin/Search" role="search">
                                <label for="admin-global-search-input">Tìm kiếm toàn cục</label>
                                <input id="admin-global-search-input" name="q" type="search" />
                                <button type="submit">Tìm</button>
                            </form>
                        </header>
                        <main id="admin-main" class="admin-main" tabindex="-1">
                            <section data-dashboard-live>
                                <small data-dashboard-live-status role="status" aria-live="polite">Cập nhật lúc 07:00 19/07/2026 giờ Việt Nam.</small>
                            </section>
                        </main>
                    </div>
                </div>
            </body>
        `);

        await page.locator('#admin-global-search-input').focus();
        const focusOutline = await page.locator('#admin-global-search-input').evaluate(element => {
            return window.getComputedStyle(element).outlineStyle;
        });
        const transitionDurationSeconds = await page.locator('.admin-navigation a').first().evaluate(element => {
            const duration = window.getComputedStyle(element).transitionDuration.split(',')[0].trim();
            if (duration.endsWith('ms')) {
                return Number.parseFloat(duration) / 1000;
            }

            return Number.parseFloat(duration);
        });
        const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);

        expect(focusOutline).not.toBe('none');
        expect(transitionDurationSeconds).toBeLessThanOrEqual(0.001);
        expect(scrollWidth).toBeLessThanOrEqual(360);
        await expect(page.locator('[data-dashboard-live-status]')).toHaveAttribute('aria-live', 'polite');
    });
});

async function loadDashboardHarness(page) {
    await page.route('/Admin', async route => {
        await route.fulfill({
            contentType: 'text/html',
            body: `
        <section data-dashboard-live data-snapshot-url="/Admin/Snapshot?days=30">
            <div data-dashboard-live-status role="status" aria-live="polite"></div>
            <div data-dashboard-alerts role="status" aria-live="polite"></div>
            <article data-kpi-index="0" class="admin-kpi-card admin-kpi-card--neutral">
                <strong data-kpi-value></strong>
                <p data-kpi-detail></p>
                <span data-kpi-comparison></span>
                <a data-kpi-action href="/Admin/Learning">Xem phiên học</a>
            </article>
        </section>
    `
        });
    });
    await page.goto('/Admin');
    await page.addScriptTag({ path: path.resolve('../../wwwroot/js/admin-dashboard.js') });
}

function snapshotPayload(statusText: string) {
    return {
        days: 30,
        period: {
            startVietnam: '2026-07-01T00:00:00+07:00',
            endVietnam: '2026-07-19T23:59:59+07:00',
            generatedAtVietnam: '2026-07-19T12:00:00+07:00'
        },
        kpis: [
            {
                label: 'Phiên học',
                value: '12',
                detail: 'phiên bắt đầu trong khoảng',
                comparison: '+1 so với kỳ trước',
                tone: 'positive',
                icon: 'ph-graduation-cap'
            }
        ],
        aiStatus: {
            summary: 'AI ổn định',
            totalProviders: 1,
            readyProviders: 1,
            unstableProviders: 0,
            errorRatePercent: 0,
            sampleSize: 20,
            minimumSampleSize: 20
        },
        contentReports: {
            pendingCount: 0,
            overdueCount: 0
        },
        alerts: [
            {
                code: 'ok',
                tone: 'success',
                title: statusText,
                detail: 'Không có việc cần xử lý',
                actionText: 'Mở dashboard',
                href: '/Admin'
            }
        ]
    };
}

declare global {
    interface Window {
        AdminDashboardLive: {
            start: (options?: { intervalMs?: number }) => void;
            stop: () => void;
        };
        __dashboardHidden: boolean;
    }
}
