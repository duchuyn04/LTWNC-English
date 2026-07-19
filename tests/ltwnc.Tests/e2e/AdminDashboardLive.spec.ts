import { expect, test } from '@playwright/test';
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
        await page.waitForTimeout(140);
        expect(requestCount).toBe(1);

        if (releaseFirstResponse != null) {
            releaseFirstResponse();
        }
        await page.waitForTimeout(90);

        expect(requestCount).toBeGreaterThanOrEqual(2);
        await expect(page.locator('[data-dashboard-live-status]')).toContainText('Cập nhật lúc');

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
        await page.waitForTimeout(30);
        expect(requestCount).toBe(1);

        await page.evaluate(() => window.dispatchEvent(new Event('pagehide')));
        await page.waitForTimeout(120);
        expect(requestCount).toBe(1);
    });
});

async function loadDashboardHarness(page) {
    await page.route('/Admin', async route => {
        await route.fulfill({
            contentType: 'text/html',
            body: `
        <section data-dashboard-live data-snapshot-url="/Admin/Snapshot?days=30">
            <div data-dashboard-live-status></div>
            <div data-dashboard-alerts></div>
            <article data-kpi-index="0" class="admin-kpi-card admin-kpi-card--neutral">
                <strong data-kpi-value></strong>
                <p data-kpi-detail></p>
                <span data-kpi-comparison></span>
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
