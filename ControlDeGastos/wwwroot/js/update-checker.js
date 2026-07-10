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
    var reg = await navigator.serviceWorker.getRegistration();
    if (!reg) return false;

    try { await reg.update(); } catch {}

    await new Promise(function (r) { setTimeout(r, 1000); });

    if (reg.waiting) return true;

    try {
        var resp = await fetch('/service-worker-assets.js', { cache: 'no-store' });
        var text = await resp.text();
        var match = text.match(/version:\s*["']([^"']+)["']/);
        if (match) {
            var newVersion = match[1];
            var cacheKeys = await caches.keys();
            var hasCache = cacheKeys.some(function (k) { return k.indexOf(newVersion) !== -1; });
            if (!hasCache) return true;
        }
    } catch {}

    return false;
};

window.registrarVisibilidad = function (dotNetRef) {
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible') {
            dotNetRef.invokeMethodAsync('OnPageVisibleAsync');
        }
    });
};
