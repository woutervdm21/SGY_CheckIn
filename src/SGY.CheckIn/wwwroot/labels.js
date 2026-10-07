// Printing Kids' check-in labels from the browser to the DYMO LabelWriter.
//
// Only the computer the printer is plugged into should print, so each device remembers
// for itself whether it's "the label printer" (localStorage: phones and tablets stay off).
// Printing loads the label page into a hidden frame and prints just that frame; with
// Chrome started with --kiosk-printing it goes straight to the default printer, no dialog.
window.sgLabels = {
    key: 'sg-label-printer',

    isPrinter() {
        try { return localStorage.getItem(this.key) === '1'; } catch { return false; }
    },

    setPrinter(on) {
        try {
            if (on) localStorage.setItem(this.key, '1');
            else localStorage.removeItem(this.key);
        } catch { /* private window: the switch just won't be remembered */ }
    },

    // Resolves true once the label is loaded and handed to the printer, false if it
    // couldn't be loaded. Doesn't wait for the print dialog: that can stay open for
    // minutes, longer than the app waits for an answer.
    async print(url) {
        // No following redirects: a sign-in that has run out redirects to the login page,
        // which must never come out of the printer as a child's label.
        const response = await fetch(url, { credentials: 'same-origin', redirect: 'manual' });
        if (!response.ok) return false;
        const html = await response.text();

        const frame = document.createElement('iframe');
        frame.setAttribute('aria-hidden', 'true');
        frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;';
        document.body.appendChild(frame);

        await new Promise(resolve => {
            frame.onload = resolve;
            frame.srcdoc = html;
        });

        const remove = () => frame.remove();
        frame.contentWindow.addEventListener('afterprint', () => setTimeout(remove, 1000));
        setTimeout(remove, 5 * 60 * 1000); // in case afterprint never fires
        setTimeout(() => {
            frame.contentWindow.focus();
            frame.contentWindow.print();
        });
        return true;
    },
};
