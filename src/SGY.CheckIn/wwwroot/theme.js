// Light or dark mode for the app: light unless this device has been switched to dark. Each
// device remembers its own choice (localStorage), and it's applied here in <head>, before the
// page draws, so the page never flashes the other colours.
(function () {
    const key = 'sg-theme';
    const colours = { dark: '#0e1015', light: '#f6f5f1' };

    function saved() {
        try { return localStorage.getItem(key) === 'dark' ? 'dark' : 'light'; } catch { return 'light'; }
    }

    function apply(theme) {
        document.documentElement.dataset.theme = theme;
        document.querySelector('meta[name="theme-color"]')?.setAttribute('content', colours[theme]);
    }

    apply(saved());

    window.sgTheme = {
        toggle() {
            const next = document.documentElement.dataset.theme === 'light' ? 'dark' : 'light';
            try { localStorage.setItem(key, next); } catch { /* private window: just this page */ }
            apply(next);
        },
    };

    // A page load Blazor does itself can replace <html>'s attributes; put the theme back.
    document.addEventListener('DOMContentLoaded', () => window.Blazor?.addEventListener?.('enhancedload', () => apply(saved())));
})();
