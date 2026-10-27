// ══ ARDAK REAL ESTATE — SITE JAVASCRIPT v2.0 ══════════════════

'use strict';

/* ── Active Nav Highlighting ──────────────────────────────── */
(function () {
    const currentPath = window.location.pathname.toLowerCase();
    const navLinks = document.querySelectorAll('.nav-link.ardak-nav-item');
    navLinks.forEach(link => {
        const href = link.getAttribute('href');
        if (href && href !== '/' && currentPath.startsWith(href.toLowerCase())) {
            link.classList.add('active');
        }
    });
})();

/* ── Fade-in Intersection Observer ───────────────────────── */
(function () {
    if (!('IntersectionObserver' in window)) return;
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.08 });

    document.querySelectorAll('.fade-in').forEach(el => {
        el.style.opacity = '0';
        el.style.transform = 'translateY(8px)';
        el.style.transition = 'opacity 0.35s ease-out, transform 0.35s ease-out';
        observer.observe(el);
    });
})();

/* ── Auto-dismiss Alerts ──────────────────────────────────── */
(function () {
    const alerts = document.querySelectorAll('.alert:not(.alert-permanent)');
    alerts.forEach(alert => {
        setTimeout(() => {
            if (alert && alert.parentNode) {
                const bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
                if (bsAlert) bsAlert.close();
            }
        }, 6000);
    });
})();

/* ── Confirm Delete / Archive ────────────────────────────── */
(function () {
    // Already handled inline in views via onclick; this is a fallback
})();

/* ── Table Row Clickable ─────────────────────────────────── */
(function () {
    const rows = document.querySelectorAll('tr[data-href]');
    rows.forEach(row => {
        row.style.cursor = 'pointer';
        row.addEventListener('click', (e) => {
            if (!e.target.closest('a, button, form, input, select')) {
                window.location.href = row.dataset.href;
            }
        });
    });
})();

/* ── Responsive Table: Prevent overflow on mobile ─────── */
(function () {
    const tables = document.querySelectorAll('.table-responsive');
    tables.forEach(t => {
        if (t.scrollWidth > t.clientWidth) {
            t.classList.add('has-scroll');
        }
    });
})();
