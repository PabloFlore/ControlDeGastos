(function () {
    window.cajaSorpresa = {
        startReel: function (elementId, itemHeight, targetIndex, totalItems) {
            var strip = document.getElementById(elementId);
            if (!strip) return Promise.resolve();
            var targetY = targetIndex * itemHeight;
            strip.style.transition = 'transform 2.5s cubic-bezier(0.15, 0.85, 0.35, 1.0)';
            strip.style.transform = 'translateY(-' + targetY + 'px)';
            return new Promise(function (resolve) {
                var fallback = setTimeout(function () { resolve(); }, 3200);
                var handler = function () {
                    clearTimeout(fallback);
                    strip.removeEventListener('transitionend', handler);
                    resolve();
                };
                strip.addEventListener('transitionend', handler);
            });
        }
    };
})();
