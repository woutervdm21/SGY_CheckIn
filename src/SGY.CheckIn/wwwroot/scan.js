// The Scan page (Scan.razor): keeps the phone's camera open and reads the QR codes on Kids
// labels, handing each one to the app, which checks the child out. The browser's own
// BarcodeDetector where there is one (Chrome on Android), otherwise jsQR (lib/jsqr), loaded
// only when the page opens. Browsers only allow the camera on HTTPS or on localhost.
window.sgScan = (() => {
    let stream = null;
    let timer = 0;
    let audio = null;

    const canUseCamera = () => window.isSecureContext && !!navigator.mediaDevices?.getUserMedia;

    function loadJsQR() {
        if (window.jsQR) return Promise.resolve();
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = '/lib/jsqr/jsQR.js';
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }

    async function makeReader(video) {
        if ('BarcodeDetector' in window) {
            try {
                const formats = await BarcodeDetector.getSupportedFormats();
                if (formats.includes('qr_code')) {
                    const detector = new BarcodeDetector({ formats: ['qr_code'] });
                    return async () => (await detector.detect(video))[0]?.rawValue ?? null;
                }
            } catch { /* fall back to jsQR */ }
        }
        await loadJsQR();
        const canvas = document.createElement('canvas');
        const context = canvas.getContext('2d', { willReadFrequently: true });
        return async () => {
            // A smaller copy of the frame: plenty for a label held up close, and quick.
            const scale = Math.min(1, 640 / Math.max(video.videoWidth, video.videoHeight));
            canvas.width = Math.round(video.videoWidth * scale);
            canvas.height = Math.round(video.videoHeight * scale);
            if (!canvas.width || !canvas.height) return null;
            context.drawImage(video, 0, 0, canvas.width, canvas.height);
            const image = context.getImageData(0, 0, canvas.width, canvas.height);
            return window.jsQR(image.data, image.width, image.height, { inversionAttempts: 'dontInvert' })?.data ?? null;
        };
    }

    return {
        // 'ok', 'insecure' (no camera on plain HTTP), 'denied' (not allowed), or 'none'.
        async start(video, app) {
            if (!canUseCamera()) return 'insecure';
            try {
                stream = await navigator.mediaDevices.getUserMedia({
                    video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
                    audio: false,
                });
            } catch (e) {
                return e?.name === 'NotAllowedError' ? 'denied' : 'none';
            }
            video.srcObject = stream;
            video.setAttribute('playsinline', '');
            await video.play().catch(() => { });

            const read = await makeReader(video);
            let busy = false;
            timer = setInterval(async () => {
                if (busy || video.readyState < 2) return;
                busy = true;
                try {
                    const text = await read();
                    if (text) await app.invokeMethodAsync('OnScannedAsync', text);
                } catch { /* a frame that can't be read: try the next */ }
                busy = false;
            }, 200);
            return 'ok';
        },

        stop() {
            clearInterval(timer);
            timer = 0;
            stream?.getTracks().forEach(t => t.stop());
            stream = null;
        },

        // A short beep (high for done, low for a problem) and a buzz, so the volunteer
        // needn't look at the screen.
        signal(ok) {
            try {
                audio ??= new AudioContext();
                const tone = audio.createOscillator();
                const gain = audio.createGain();
                tone.frequency.value = ok ? 1320 : 330;
                gain.gain.setValueAtTime(0.25, audio.currentTime);
                gain.gain.exponentialRampToValueAtTime(0.001, audio.currentTime + (ok ? 0.15 : 0.4));
                tone.connect(gain).connect(audio.destination);
                tone.start();
                tone.stop(audio.currentTime + (ok ? 0.15 : 0.4));
            } catch { /* no sound: the screen still says */ }
            navigator.vibrate?.(ok ? 80 : [80, 60, 80]);
        },
    };
})();
