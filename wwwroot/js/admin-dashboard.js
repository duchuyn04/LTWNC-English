(function () {
    const DEFAULT_INTERVAL_MS = 30000;

    let timerId = null;
    let inFlight = false;
    let abortController = null;
    let rootElement = null;
    let snapshotUrl = null;
    let intervalMs = DEFAULT_INTERVAL_MS;

    // Khởi động polling cho dashboard; options chỉ dùng để test trình duyệt với chu kỳ ngắn.
    function start(options) {
        stop();
        rootElement = document.querySelector('[data-dashboard-live]');
        if (!rootElement) {
            return;
        }

        snapshotUrl = rootElement.getAttribute('data-snapshot-url');
        if (!snapshotUrl) {
            return;
        }

        intervalMs = DEFAULT_INTERVAL_MS;
        if (options && Number.isFinite(options.intervalMs) && options.intervalMs > 0) {
            intervalMs = options.intervalMs;
        }

        document.addEventListener('visibilitychange', handleVisibilityChange);
        window.addEventListener('pagehide', stop);
        if (!document.hidden) {
            refresh();
            scheduleNextRefresh();
        }
    }

    // Dừng polling và hủy request đang chạy khi rời trang.
    function stop() {
        stopTimerOnly();
        document.removeEventListener('visibilitychange', handleVisibilityChange);
        window.removeEventListener('pagehide', stop);
        if (abortController) {
            abortController.abort();
            abortController = null;
        }

        inFlight = false;
    }

    // Chỉ dừng timer để pause khi tab ẩn nhưng vẫn giữ dữ liệu đang hiển thị.
    function stopTimerOnly() {
        if (timerId) {
            window.clearTimeout(timerId);
            timerId = null;
        }
    }

    // Khi tab hiện lại thì làm mới ngay, không chờ hết chu kỳ cũ.
    function handleVisibilityChange() {
        if (document.hidden) {
            stopTimerOnly();
            return;
        }

        refresh();
        scheduleNextRefresh();
    }

    // Lên lịch lần refresh kế tiếp bằng setTimeout để không tạo request chồng nhau.
    function scheduleNextRefresh() {
        stopTimerOnly();
        if (document.hidden) {
            return;
        }

        timerId = window.setTimeout(function () {
            refresh();
            scheduleNextRefresh();
        }, intervalMs);
    }

    // Gọi endpoint snapshot; nếu request cũ chưa xong thì bỏ qua chu kỳ mới.
    async function refresh() {
        if (inFlight || !snapshotUrl) {
            return;
        }

        inFlight = true;
        abortController = new AbortController();
        try {
            setStatus('Đang cập nhật...');
            const response = await fetch(snapshotUrl, {
                headers: {
                    'Accept': 'application/json'
                },
                cache: 'no-store',
                signal: abortController.signal
            });
            if (!response.ok) {
                throw new Error('HTTP ' + response.status);
            }

            const snapshot = await response.json();
            renderSnapshot(snapshot);
        } catch (error) {
            if (!error || error.name !== 'AbortError') {
                setStatus('Không cập nhật được dữ liệu mới. Đang giữ số liệu gần nhất.');
            }
        } finally {
            inFlight = false;
            abortController = null;
        }
    }

    // Cập nhật KPI, cảnh báo và thời điểm sinh snapshot từ contract JSON.
    function renderSnapshot(snapshot) {
        if (!snapshot) {
            return;
        }

        renderKpis(snapshot.kpis || []);
        renderAlerts(snapshot.alerts || []);
        renderPeriod(snapshot.period);
    }

    // Cập nhật từng KPI theo vị trí để layout card không bị tạo lại toàn bộ.
    function renderKpis(kpis) {
        kpis.forEach(function (kpi, index) {
            const card = rootElement.querySelector('[data-kpi-index="' + index + '"]');
            if (!card) {
                return;
            }

            setText(card.querySelector('[data-kpi-label]'), kpi.label);
            setText(card.querySelector('[data-kpi-value]'), kpi.value);
            setText(card.querySelector('[data-kpi-detail]'), kpi.detail);
            setText(card.querySelector('[data-kpi-comparison]'), kpi.comparison);
            card.className = 'admin-kpi-card admin-kpi-card--' + normalizeKpiTone(kpi.tone);

            const icon = card.querySelector('[data-kpi-icon]');
            if (icon && kpi.icon) {
                icon.className = 'ph ' + kpi.icon;
            }
        });
    }

    // Render cảnh báo từ trạng thái hiện tại; không có nút đóng vì cảnh báo tự biến mất.
    function renderAlerts(alerts) {
        const container = rootElement.querySelector('[data-dashboard-alerts]');
        if (!container) {
            return;
        }

        container.replaceChildren();
        if (alerts.length === 0) {
            container.appendChild(createAlert({
                tone: 'success',
                title: 'Không có cảnh báo vận hành',
                detail: 'Dữ liệu hiện tại chưa ghi nhận việc cần xử lý ngay.',
                actionText: '',
                href: ''
            }));
            return;
        }

        alerts.forEach(function (alert) {
            container.appendChild(createAlert(alert));
        });
    }

    // Tạo một alert card bằng DOM API để tránh đưa HTML từ endpoint vào innerHTML.
    function createAlert(alert) {
        const article = document.createElement('article');
        article.className = 'admin-live-alert admin-live-alert--' + normalizeAlertTone(alert.tone);

        const body = document.createElement('div');
        const title = document.createElement('h3');
        title.textContent = alert.title || 'Cảnh báo vận hành';
        const detail = document.createElement('p');
        detail.textContent = alert.detail || '';
        body.appendChild(title);
        body.appendChild(detail);
        article.appendChild(body);

        if (alert.href && alert.actionText) {
            const link = document.createElement('a');
            link.href = alert.href;
            link.textContent = alert.actionText;
            article.appendChild(link);
        }

        return article;
    }

    // Cập nhật thời gian snapshot bằng định dạng ngắn gọn tiếng Việt.
    function renderPeriod(period) {
        if (!period || !period.generatedAtVietnam) {
            return;
        }

        const generatedAt = new Date(period.generatedAtVietnam);
        const formatted = formatVietnamDateTime(generatedAt);
        setStatus('Cập nhật lúc ' + formatted + ' giờ Việt Nam.');
        setText(rootElement.querySelector('[data-dashboard-updated]'), 'Cập nhật lúc ' + formatted + ' giờ Việt Nam.');
    }

    // Ghi text an toàn cho các node có thể không tồn tại.
    function setText(element, value) {
        if (!element || value === undefined || value === null) {
            return;
        }

        element.textContent = value;
    }

    // Ghi trạng thái live mà không xóa số liệu đang hiển thị.
    function setStatus(message) {
        setText(rootElement.querySelector('[data-dashboard-live-status]'), message);
    }

    // Chuẩn hóa tone KPI về các class CSS đã có trên card.
    function normalizeKpiTone(tone) {
        if (tone === 'positive' || tone === 'negative' || tone === 'neutral') {
            return tone;
        }

        return 'neutral';
    }

    // Chuẩn hóa tone cảnh báo về các class CSS đã biết.
    function normalizeAlertTone(tone) {
        if (tone === 'positive' || tone === 'success') {
            return 'success';
        }

        if (tone === 'negative' || tone === 'danger') {
            return 'danger';
        }

        if (tone === 'warning') {
            return 'warning';
        }

        return 'neutral';
    }

    // Định dạng ngày giờ theo locale Việt Nam cho phần trạng thái cập nhật.
    function formatVietnamDateTime(value) {
        return value.toLocaleString('vi-VN', {
            hour: '2-digit',
            minute: '2-digit',
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour12: false,
            timeZone: 'Asia/Ho_Chi_Minh'
        });
    }

    window.AdminDashboardLive = {
        start: start,
        stop: stop
    };

    document.addEventListener('DOMContentLoaded', function () {
        start();
    });
}());
