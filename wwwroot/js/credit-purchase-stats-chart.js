(function () {
    const dataEl = document.getElementById('credit-purchase-stats-data');
    if (!dataEl || typeof Chart === 'undefined') return;

    let data;
    try {
        data = JSON.parse(dataEl.textContent);
    } catch {
        return;
    }

    const revenueCanvas = document.querySelector('[data-credit-stats-chart="revenue"]');
    if (revenueCanvas && data.labels && data.labels.length) {
        new Chart(revenueCanvas, {
            type: 'bar',
            data: {
                labels: data.labels,
                datasets: [
                    {
                        type: 'bar',
                        label: 'Tiền (VND)',
                        data: data.paidVnd,
                        yAxisID: 'y',
                        order: 2,
                        backgroundColor: 'rgba(59, 130, 246, 0.55)',
                        borderColor: 'rgba(59, 130, 246, 1)',
                        borderWidth: 1
                    },
                    {
                        type: 'line',
                        label: 'Số đơn',
                        data: data.paidCount,
                        yAxisID: 'y1',
                        order: 1,
                        tension: 0.3,
                        borderColor: 'rgba(245, 158, 11, 1)',
                        backgroundColor: 'rgba(245, 158, 11, 0.15)',
                        borderWidth: 2,
                        pointRadius: 3
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: { legend: { position: 'bottom' } },
                scales: {
                    y: {
                        beginAtZero: true,
                        position: 'left',
                        ticks: {
                            callback: function (value) {
                                return Number(value).toLocaleString('vi-VN');
                            }
                        }
                    },
                    y1: {
                        beginAtZero: true,
                        position: 'right',
                        grid: { drawOnChartArea: false },
                        ticks: { precision: 0 }
                    }
                }
            }
        });
    }

    const pkgCanvas = document.querySelector('[data-credit-stats-chart="packages"]');
    if (pkgCanvas && data.packages && data.packages.length) {
        new Chart(pkgCanvas, {
            type: 'doughnut',
            data: {
                labels: data.packages.map(function (p) { return p.label; }),
                datasets: [{
                    data: data.packages.map(function (p) { return p.vnd; }),
                    backgroundColor: [
                        'rgba(59, 130, 246, 0.75)',
                        'rgba(34, 197, 94, 0.75)',
                        'rgba(245, 158, 11, 0.75)',
                        'rgba(139, 92, 246, 0.75)',
                        'rgba(239, 68, 68, 0.75)',
                        'rgba(14, 165, 233, 0.75)'
                    ]
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { position: 'bottom' },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const item = data.packages[context.dataIndex];
                                const vnd = Number(item.vnd).toLocaleString('vi-VN');
                                return ' ' + item.label + ': ' + vnd + ' đ (' + item.count + ' đơn)';
                            }
                        }
                    }
                }
            }
        });
    }

    const statusCanvas = document.querySelector('[data-credit-stats-chart="status"]');
    if (statusCanvas && data.statuses && data.statuses.length) {
        new Chart(statusCanvas, {
            type: 'bar',
            data: {
                labels: data.statuses.map(function (s) { return s.label; }),
                datasets: [{
                    label: 'Đơn',
                    data: data.statuses.map(function (s) { return s.count; }),
                    backgroundColor: 'rgba(100, 116, 139, 0.65)'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    y: { beginAtZero: true, ticks: { precision: 0 } }
                }
            }
        });
    }
}());
