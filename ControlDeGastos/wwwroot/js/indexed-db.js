window.indexedDbStorage = {
    _dbName: 'ControlDeGastos',
    _dbVersion: 1,
    _storeName: 'cdg_data',
    _db: null,

    async _open() {
        if (this._db) return this._db;
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(this._dbName, this._dbVersion);
            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                if (!db.objectStoreNames.contains(this._storeName)) {
                    db.createObjectStore(this._storeName);
                }
            };
            request.onsuccess = (event) => {
                this._db = event.target.result;
                resolve(this._db);
            };
            request.onerror = (event) => {
                reject(event.target.error);
            };
        });
    },

    async getItem(key) {
        const db = await this._open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(this._storeName, 'readonly');
            const store = tx.objectStore(this._storeName);
            const request = store.get(key);
            request.onsuccess = () => resolve(request.result || null);
            request.onerror = () => reject(request.error);
        });
    },

    async setItem(key, value) {
        const db = await this._open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(this._storeName, 'readwrite');
            const store = tx.objectStore(this._storeName);
            const request = store.put(value, key);
            request.onsuccess = () => resolve();
            request.onerror = () => reject(request.error);
        });
    },

    async removeItem(key) {
        const db = await this._open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(this._storeName, 'readwrite');
            const store = tx.objectStore(this._storeName);
            const request = store.delete(key);
            request.onsuccess = () => resolve();
            request.onerror = () => reject(request.error);
        });
    },

    async clear() {
        const db = await this._open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(this._storeName, 'readwrite');
            const store = tx.objectStore(this._storeName);
            const request = store.clear();
            request.onsuccess = () => resolve();
            request.onerror = () => reject(request.error);
        });
    },

    async keyExists(key) {
        const db = await this._open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(this._storeName, 'readonly');
            const store = tx.objectStore(this._storeName);
            const request = store.getKey(key);
            request.onsuccess = () => resolve(request.result !== undefined);
            request.onerror = () => reject(request.error);
        });
    },

    async getAllKeys() {
        const db = await this._open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(this._storeName, 'readonly');
            const store = tx.objectStore(this._storeName);
            const request = store.getAllKeys();
            request.onsuccess = () => resolve(request.result || []);
            request.onerror = () => reject(request.error);
        });
    },

    async getSize() {
        const db = await this._open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(this._storeName, 'readonly');
            const store = tx.objectStore(this._storeName);
            const request = store.getAll();
            request.onsuccess = () => {
                const items = request.result || [];
                const totalBytes = items.reduce((sum, item) => {
                    if (typeof item === 'string') return sum + item.length * 2;
                    return sum + JSON.stringify(item).length * 2;
                }, 0);
                resolve(totalBytes);
            };
            request.onerror = () => reject(request.error);
        });
    },
};
