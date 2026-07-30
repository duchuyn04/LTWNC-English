(function () {
    const canvases = document.querySelectorAll('[data-admin-dashboard-chart]');
    const dataElement = document.getElementById('admin-dashboard-chart-data');
    if (canvases.length === 0 || !dataElement || typeof Chart === 'undefined') {
        return;
    }

    let data;
    try {
        data = JSON.parse(dataElement.textContent);
    } catch {
        return;
    }

    const styles = getComputedStyle(document.body);
    function color(token) {
        return styles.getPropertyValue(token).trim();
    }

    function createGradient(ctx, chartArea, colorTop, colorBottom) {
        if (!chartArea) return colorBottom;
        const gradient = ctx.createLinearGradient(0, chartArea.top, 0, chartArea.bottom);
        gradient.addColorStop(0, colorTop);
        gradient.addColorStop(1, colorBottom);
        return gradient;
    }

    function tickDensity(width) {
        if (width < 360) return { rotation: 90, size: 8 };
        if (width < 700) return { rotation: 45, size: 9 };
        return { rotation: 0, size: 10 };
    }

    function updateDensity(chart, width) {
        const density = tickDensity(width);
        chart.options.scales.x.ticks.maxRotation = density.rotation;
        chart.options.scales.x.ticks.minRotation = density.rotation;
        chart.options.scales.x.ticks.font.size = density.size;
        chart.data.datasets.forEach(function (ds) {
            ds.pointRadius = width < 420 ? 2 : 3;
            ds.pointHoverRadius = width < 420 ? 5 : 6;
        });
    }

    function dataset(label, values, borderColor, gradientTop, gradientBottom, options) {
        return Object.assign({
            label: label,
            data: values,
            borderColor: borderColor,
            backgroundColor: function (context) {
                const chart = context.chart;
                const ctx = chart.ctx;
                const area = chart.chartArea;
                return createGradient(ctx, area, gradientTop, gradientBottom);
            },
            borderWidth: 2.5,
            pointBackgroundColor: color('--surface'),
            pointBorderColor: borderColor,
            pointBorderWidth: 2,
            pointRadius: 3,
            pointHoverRadius: 6,
            pointHoverBorderWidth: 2.5,
            pointHoverBackgroundColor: color('--surface'),
            pointHitRadius: 12,
            tension: 0.38,
            fill: true
        }, options || {});
    }

    function chartOptions(canvas) {
        const initialDensity = tickDensity(canvas.parentElement.clientWidth);
        return {
            responsive: true,
            maintainAspectRatio: false,
            animation: {
                duration: 600,
                easing: 'easeOutQuart'
            },
            normalized: true,
            locale: 'vi-VN',
            interaction: {
                mode: 'index',
                intersect: false
            },
            layout: {
                padding: {
                    top: 12,
                    right: 8,
                    bottom: 4,
                    left: 4
                }
            },
            onResize: function (chart, size) {
                updateDensity(chart, size.width);
            },
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    backgroundColor: color('--ink'),
                    titleColor: color('--paper'),
                    bodyColor: color('--paper'),
                    borderColor: color('--line-strong'),
                    borderWidth: 1,
                    cornerRadius: 10,
                    padding: { top: 10, bottom: 10, left: 14, right: 14 },
                    titleFont: { weight: '600', size: 12 },
                    bodyFont: { size: 12 },
                    usePointStyle: true,
                    pointStyleWidth: 8,
                    boxPadding: 4,
                    caretSize: 6,
                    caretPadding: 8,
                    displayColors: true,
                    callbacks: {
                        label: function (context) {
                            return ' ' + context.dataset.label + ': ' + context.formattedValue;
                        }
                    }
                }
            },
            scales: {
                x: {
                    offset: false,
                    grid: {
                        display: false
                    },
                    border: {
                        display: false
                    },
                    ticks: {
                        autoSkip: true,
                        maxTicksLimit: 14,
                        color: color('--muted-light'),
                        maxRotation: initialDensity.rotation,
                        minRotation: initialDensity.rotation,
                        padding: 8,
                        font: {
                            size: initialDensity.size,
                            weight: '500'
                        }
                    }
                },
                y: {
                    beginAtZero: true,
                    grace: '10%',
                    grid: {
                        color: color('--line'),
                        lineWidth: 1,
                        drawTicks: false
                    },
                    border: {
                        display: false
                    },
                    ticks: {
                        color: color('--muted-light'),
                        precision: 0,
                        padding: 12,
                        font: {
                            size: 10,
                            weight: '500'
                        },
                        maxTicksLimit: 5
                    }
                }
            }
        };
    }

    canvases.forEach(function (canvas) {
        const kind = canvas.dataset.adminDashboardChart;
        var datasets;

        if (kind === 'activity') {
            datasets = [
                dataset(
                    'Hoàn thành',
                    data.completed,
                    color('--success'),
                    'oklch(48% 0.075 150 / 0.25)',
                    'oklch(48% 0.075 150 / 0.02)'
                ),
                dataset(
                    'Bỏ dở',
                    data.abandoned,
                    color('--brass-deep'),
                    'oklch(50% 0.11 73 / 0.15)',
                    'oklch(50% 0.11 73 / 0.02)',
                    { borderDash: [6, 4], tension: 0.3 }
                )
            ];
        } else if (kind === 'new-users') {
            datasets = [
                dataset(
                    'Người dùng mới',
                    data.newUsers,
                    color('--brass-deep'),
                    'oklch(50% 0.11 73 / 0.25)',
                    'oklch(50% 0.11 73 / 0.02)',
                    { borderWidth: 2.5 }
                )
            ];
        } else if (kind === 'reports') {
            datasets = [
                dataset(
                    'Báo cáo',
                    data.reports,
                    color('--error'),
                    'oklch(50% 0.12 28 / 0.22)',
                    'oklch(50% 0.12 28 / 0.02)',
                    { borderWidth: 2.5 }
                )
            ];
        } else {
            return;
        }

        new Chart(canvas, {
            type: 'line',
            data: {
                labels: data.labels,
                datasets: datasets
            },
            options: chartOptions(canvas)
        });
    });
}());
