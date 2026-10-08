// Printing Kids' check-in labels from the browser to the DYMO LabelWriter.
//
// Each device remembers for itself (localStorage) what it does with a label: print it here
// (the computer with the DYMO, which then also prints the labels phones send it), send it
// to that computer (a phone or tablet), or nothing. LabelPrinting.cs does the rest.
// Printing loads the label page into a hidden frame and prints just that frame; with
// Chrome started with --kiosk-printing it goes straight to the default printer, no dialog.
window.sgLabels = {
    keys: { mode: 'sg-label-mode', name: 'sg-label-station-name', target: 'sg-label-target' },

    // A new device sends its labels to the print station; only the computer with the DYMO
    // is set to print them itself. Before there was a choice, devices just printed or not
    // ('sg-label-printer'): one that printed still does.
    settings() {
        const read = key => { try { return localStorage.getItem(key); } catch { return null; } };
        const mode = read(this.keys.mode) ?? (read('sg-label-printer') === '1' ? 'here' : 'station');
        return { mode, name: read(this.keys.name) ?? '', target: read(this.keys.target) ?? '' };
    },

    save(key, value) {
        try {
            localStorage.setItem(this.keys[key], value);
        } catch { /* private window: the setting just won't be remembered */ }
    },

    // Tells the app when another tab of this browser changes the setting, so every tab
    // follows it: otherwise a tab still open as a print station keeps its old name.
    watch(app) {
        const keys = Object.values(this.keys);
        window.addEventListener('storage', e => {
            if (e.key === null || keys.includes(e.key)) {
                app.invokeMethodAsync('ReloadAsync').catch(() => { });
            }
        });
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

        // The label sizes its text once its font has loaded; give that a moment, never forever.
        await Promise.race([frame.contentWindow.sgLabelReady, new Promise(r => setTimeout(r, 3000))]);

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
