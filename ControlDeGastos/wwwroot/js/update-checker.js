window.forzarRecarga = async function () {
    var keys = await caches.keys();
    var appCaches = keys.filter(function (k) { return k.startsWith('offline-cache-'); });
    await Promise.all(appCaches.map(function (key) { return caches.delete(key); }));
    var reg = await navigator.serviceWorker.getRegistration();
    if (reg) {
        if (reg.waiting) {
            reg.waiting.postMessage({ type: 'SKIP_WAITING' });
        }
        reg.update();
    }
    window.location.href = window.location.href.split('?')[0] + '?t=' + Date.now();
};

window.verificarActualizacionPendiente = async function () {
    if (!('serviceWorker' in navigator)) return false;

    try {
        var resp = await fetch('/service-worker.js', { cache: 'no-store' });
        var serverText = await resp.text();
        var serverHash = await hashString(serverText);

        var reg = await navigator.serviceWorker.getRegistration();
        if (!reg || !reg.active) return false;

        var cachedResp = await fetch(reg.active.scriptURL, { cache: 'no-store' });
        var cachedBody = await cachedResp.text();
        var cachedHash = await hashString(cachedBody);

        return serverHash !== cachedHash;
    } catch {}

    return false;
};

async function hashString(str) {
    var buf = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(str));
    return Array.from(new Uint8Array(buf)).map(function (b) { return b.toString(16).padStart(2, '0'); }).join('');
}

window.registrarVisibilidad = function (dotNetRef) {
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible') {
            dotNetRef.invokeMethodAsync('OnPageVisibleAsync');
        }
    });
};
