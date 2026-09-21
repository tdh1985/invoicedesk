// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

// small browser-side helpers; everything else lives in c#
window.invoicedesk = {
    init(ref) {
        this.ref = ref;
        document.addEventListener('keydown', (e) => {
            const mod = e.ctrlKey || e.metaKey;
            const key = e.key.toLowerCase();
            if (mod && !e.shiftKey && !e.altKey && (key === 'k' || key === 'n' || key === 's')) {
                e.preventDefault();
                ref.invokeMethodAsync('OnShortcut', key);
            } else if (e.key === 'Escape') {
                ref.invokeMethodAsync('OnShortcut', 'escape');
            }
        });
        // a file dropped outside a drop zone would otherwise open in the webview
        const outsideDropZone = (e) => !(e.target instanceof Element && e.target.closest('input[type=file]'));
        document.addEventListener('dragover', (e) => {
            if (outsideDropZone(e)) {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'none';
            }
        });
        document.addEventListener('drop', (e) => { if (outsideDropZone(e)) e.preventDefault(); });
    },

    applyTheme(theme) {
        try { localStorage.setItem('invoicedesk-theme', theme); } catch (e) { }
        const media = matchMedia('(prefers-color-scheme: dark)');
        const paint = () => {
            const dark = theme === 'dark' || (theme === 'system' && media.matches);
            document.documentElement.dataset.theme = dark ? 'dark' : 'light';
            this.ref?.invokeMethodAsync('ThemeResolved', dark);
        };
        media.onchange = theme === 'system' ? paint : null;
        paint();
    },

    focus(selector) {
        const el = document.querySelector(selector);
        if (!el) return;
        el.focus();
        if (typeof el.select === 'function' && el.tagName === 'INPUT') el.select();
    },

    // scales the a4 preview to whatever width its pane has
    fitPreview(el) {
        if (!el || el._fit) return;
        const fit = () => {
            const width = el.clientWidth - 48;
            el.style.setProperty('--scale', Math.max(0.3, Math.min(1, width / 794)).toFixed(4));
        };
        el._fit = new ResizeObserver(fit);
        el._fit.observe(el);
        fit();
    },

    scrollTop(selector) {
        document.querySelector(selector)?.scrollTo({ top: 0 });
    },
};
