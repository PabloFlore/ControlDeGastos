(function () {
    window.rpgEffects = {
        damageFlash: function () {
            var el = document.createElement('div');
            el.className = 'rpg-damage-flash';
            document.body.appendChild(el);
            setTimeout(function () { el.remove(); }, 600);
        },
        levelUpFlash: function () {
            var el = document.createElement('div');
            el.className = 'rpg-levelup-flash';
            document.body.appendChild(el);
            setTimeout(function () { el.remove(); }, 800);
        }
    };
})();
