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
