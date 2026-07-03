(function () {
    var origUnobserve = ResizeObserver.prototype.unobserve;
    ResizeObserver.prototype.unobserve = function (el) {
        if (!(el instanceof Element)) return;
        return origUnobserve.call(this, el);
    };
})();
