window.forzarRecarga = async function () {
    const keys = await caches.keys();
    await Promise.all(keys.map(key => caches.delete(key)));
    const reg = await navigator.serviceWorker.getRegistration();
    if (reg) await reg.unregister();
    window.location.reload();
};
