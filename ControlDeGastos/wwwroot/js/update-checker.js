window.updateChecker = {
    _registration: null,
    _waitingWorker: null,
    _hayUpdate: false,

    iniciar: async function () {
        try {
            const reg = await navigator.serviceWorker.getRegistration();
            if (!reg) return false;
            window.updateChecker._registration = reg;

            if (reg.waiting) {
                window.updateChecker._waitingWorker = reg.waiting;
                window.updateChecker._hayUpdate = true;
                return true;
            }

            reg.addEventListener('updatefound', function () {
                const nuevo = reg.installing;
                if (!nuevo) return;
                nuevo.addEventListener('statechange', function () {
                    if (nuevo.state === 'installed' && navigator.serviceWorker.controller) {
                        window.updateChecker._waitingWorker = nuevo;
                        window.updateChecker._hayUpdate = true;
                    }
                });
            });
            return false;
        } catch { return false; }
    },

    hayActualizacion: function () {
        return window.updateChecker._hayUpdate;
    },

    activar: async function () {
        if (!window.updateChecker._waitingWorker) return false;
        return new Promise(function (resolve) {
            navigator.serviceWorker.addEventListener('controllerchange', function () {
                window.location.reload();
                resolve(true);
            });
            window.updateChecker._waitingWorker.postMessage({ type: 'SKIP_WAITING' });
        });
    }
};
