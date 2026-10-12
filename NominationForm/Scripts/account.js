/* Staff Welfare Fund - login / sign up pages. Vanilla JS, no jQuery (fast load). */
(function () {
    'use strict';

    function byId(id) { return document.getElementById(id); }

    // ---- password show / hide
    var toggles = document.querySelectorAll('[data-toggle-pw]');
    Array.prototype.forEach.call(toggles, function (btn) {
        btn.addEventListener('click', function () {
            var input = byId(btn.getAttribute('data-toggle-pw'));
            if (!input) return;
            var show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            btn.textContent = show ? 'লুকান' : 'দেখান';
        });
    });

    // ---- Voter / Member toggle : sodosso number box dekhano
    var memberBox = byId('memberBox');
    var radios = document.querySelectorAll('input[name="AccountType"]');

    function syncType() {
        if (!memberBox) return;
        var isMember = false;
        Array.prototype.forEach.call(radios, function (r) {
            if (r.checked && r.value === 'Member') isMember = true;
        });
        memberBox.classList.toggle('is-hidden', !isMember);
    }

    Array.prototype.forEach.call(radios, function (r) { r.addEventListener('change', syncType); });
    syncType();

    // ---- password strength + match hint (sign up page)
    var pw = byId('Password');
    var cp = byId('ConfirmPassword');
    var meter = byId('pwMeter');
    var match = byId('pwMatch');

    function score(v) {
        var s = 0;
        if (v.length >= 8) s++;
        if (/[A-Za-z]/.test(v) && /[0-9]/.test(v)) s++;
        if (v.length >= 12) s++;
        if (/[^A-Za-z0-9]/.test(v) && v.length >= 8) s++;
        return s;
    }

    function checkMatch() {
        if (!cp || !match) return;
        if (!cp.value) { match.textContent = ''; match.className = 'a-hint'; return; }
        var ok = cp.value === pw.value;
        match.textContent = ok ? 'পাসওয়ার্ড মিলেছে' : 'পাসওয়ার্ড মিলছে না';
        match.className = 'a-hint ' + (ok ? 'ok' : 'bad');
    }

    if (pw && meter) {
        pw.addEventListener('input', function () {
            meter.setAttribute('data-s', pw.value ? String(score(pw.value)) : '0');
            checkMatch();
        });
    }
    if (cp) cp.addEventListener('input', checkMatch);

    // ---- double submit atkano
    var forms = document.querySelectorAll('form.acc-form');
    Array.prototype.forEach.call(forms, function (f) {
        f.addEventListener('submit', function () {
            var b = f.querySelector('button[type="submit"]');
            if (!b || b.disabled) return;
            b.setAttribute('data-label', b.textContent);
            b.textContent = b.getAttribute('data-busy') || 'অপেক্ষা করুন...';
            // submit shesh howar por disable (nahole form data jay na)
            setTimeout(function () { b.disabled = true; }, 0);
        });
    });

    // browser back button e page cache theke fire elo: button abar thik kore dei
    window.addEventListener('pageshow', function (e) {
        if (!e.persisted) return;
        Array.prototype.forEach.call(document.querySelectorAll('button[type="submit"][data-label]'), function (b) {
            b.disabled = false;
            b.textContent = b.getAttribute('data-label');
        });
    });
})();
