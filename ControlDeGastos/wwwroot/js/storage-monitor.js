window.storageMonitor = {
    async estimar() {
        if (!navigator.storage || !navigator.storage.estimate) {
            return { usage: 0, quota: 5242880 };
        }
        const estimacion = await navigator.storage.estimate();
        return {
            usage: estimacion.usage || 0,
            quota: estimacion.quota || 5242880,
        };
    },

    obtenerClaves(prefix) {
        const keys = [];
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (!prefix || key.startsWith(prefix)) {
                keys.push(key);
            }
        }
        return keys;
    },

    obtenerTamanioClave(key) {
        const value = localStorage.getItem(key);
        if (!value) return 0;
        return key.length + value.length;
    },

    obtenerRegistros(key) {
        try {
            const value = localStorage.getItem(key);
            if (!value) return 0;
            const parsed = JSON.parse(value);
            if (Array.isArray(parsed)) return parsed.length;
            if (typeof parsed === 'object' && parsed !== null) return 1;
            return 0;
        } catch {
            return 0;
        }
    },

    async obtenerTodasLasClaves() {
        return window.storageMonitor.obtenerClaves('cdg_');
    },

    async leerValor(key) {
        return localStorage.getItem(key);
    },

    async eliminarClave(key) {
        localStorage.removeItem(key);
    },
};
