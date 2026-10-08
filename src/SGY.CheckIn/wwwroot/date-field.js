// Types the slashes in a DD/MM/YYYY box (DateField.razor) as the date is typed: "12"
// becomes "12/", then "12/05/" and "12/05/2015". A slash typed after a single digit
// ("1/5/2015") is kept, and "-", "." or a space count as one. Done in the browser, on
// every key, so the caret never jumps; the server only sees the date once it's whole (or
// when the box loses focus). Listens on the document, so boxes Blazor adds later are covered too.
(() => {
    const separator = /[\/\-. ]/;
    const complete = /^\d{1,2}\/\d{1,2}\/\d{4}$/;

    function format(value) {
        let out = '';
        let part = 0;     // 0 day, 1 month, 2 year
        let length = 0;   // digits in the current part
        for (const ch of value) {
            if (ch >= '0' && ch <= '9') {
                if (part < 2 && length === 2) {
                    out += '/';
                    part++;
                    length = 0;
                }
                if (part === 2 && length === 4) {
                    break;
                }
                out += ch;
                length++;
            } else if (separator.test(ch) && part < 2 && length > 0) {
                out += '/';
                part++;
                length = 0;
            }
        }
        // A full day or month: on to the next part.
        if (part < 2 && length === 2) {
            out += '/';
        }
        return out;
    }

    document.addEventListener('input', e => {
        const box = e.target;
        if (!(box instanceof HTMLInputElement) || !box.hasAttribute('data-date-mask')) {
            return;
        }
        // Backspace and delete are left alone, so a slash can be deleted; so is typing
        // anywhere but the end, to fix one digit in the middle without the caret jumping.
        if (e.inputType?.startsWith('delete') || box.selectionStart !== box.value.length) {
            return;
        }
        const formatted = format(box.value);
        if (formatted !== box.value) {
            box.value = formatted;
            box.setSelectionRange(formatted.length, formatted.length);
        }
        // A whole date: hand it to the server now, so its checks ("not old enough yet")
        // show as the last digit is typed rather than when the box loses focus.
        if (complete.test(formatted)) {
            box.dispatchEvent(new Event('change', { bubbles: true }));
        }
    });
})();
