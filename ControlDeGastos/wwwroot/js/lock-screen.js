window.pinLock = {
    _timeoutId: null,
    _delaySegundos: 30,

    iniciar: function (dotNetHelper, delaySegundos) {
        window.pinLock._delaySegundos = delaySegundos || 30;
        window.pinLock._dotNetHelper = dotNetHelper;

        document.addEventListener('visibilitychange', function () {
            if (document.hidden) {
                window.pinLock._iniciarContador();
            } else {
                window.pinLock._verificarContador();
            }
        });
    },

    _iniciarContador: function () {
        if (window.pinLock._timeoutId) {
            clearTimeout(window.pinLock._timeoutId);
        }
        window.pinLock._timestampOculto = Date.now();
        window.pinLock._timeoutId = setTimeout(function () {
            if (window.pinLock._dotNetHelper) {
                window.pinLock._dotNetHelper.invokeMethodAsync('CerrarSesion');
            }
        }, window.pinLock._delaySegundos * 1000);
    },

    _verificarContador: function () {
        if (window.pinLock._timeoutId) {
            clearTimeout(window.pinLock._timeoutId);
            window.pinLock._timeoutId = null;
        }
        if (window.pinLock._timestampOculto) {
            var transcurridos = (Date.now() - window.pinLock._timestampOculto) / 1000;
            if (transcurridos >= window.pinLock._delaySegundos) {
                if (window.pinLock._dotNetHelper) {
                    window.pinLock._dotNetHelper.invokeMethodAsync('CerrarSesion');
                }
            }
        }
        window.pinLock._timestampOculto = null;
    },

    actualizarDelay: function (nuevosSegundos) {
        window.pinLock._delaySegundos = nuevosSegundos;
    },

    limpiar: function () {
        if (window.pinLock._timeoutId) {
            clearTimeout(window.pinLock._timeoutId);
            window.pinLock._timeoutId = null;
        }
        window.pinLock._timestampOculto = null;
        window.pinLock._dotNetHelper = null;
    }
};
