/* ================================================================
   Staff Welfare Fund - Election Settings page
   - Bangla date/time display + live duration
   - Save / Create (AJAX)        -> Admin/ElectionSettingsSave
   - Emergency close (AJAX)      -> Admin/ElectionEmergencyClose
   - Election switcher (dropdown) + "new election" mode
   ================================================================ */
(function () {
    'use strict';

    var root = document.getElementById('esRoot');
    if (!root) return;

    var SAVE_URL = root.getAttribute('data-save-url');
    var CLOSE_URL = root.getAttribute('data-close-url');
    var BASE_URL = root.getAttribute('data-base-url') || '';
    var LOCKED = root.getAttribute('data-locked') === '1';

    var BN_DIGITS = ['০', '১', '২', '৩', '৪', '৫', '৬', '৭', '৮', '৯'];
    var BN_MONTHS = ['জানুয়ারি', 'ফেব্রুয়ারি', 'মার্চ', 'এপ্রিল', 'মে', 'জুন',
        'জুলাই', 'আগস্ট', 'সেপ্টেম্বর', 'অক্টোবর', 'নভেম্বর', 'ডিসেম্বর'];

    function $(id) { return document.getElementById(id); }

    var el = {
        title: $('esTitle'),
        term: $('esTerm'),
        committee: $('esCommittee'),
        note: $('esNote'),
        start: $('esStart'),
        end: $('esEnd'),
        startBox: $('esStartBox'),
        endBox: $('esEndBox'),
        startText: $('esStartText'),
        endText: $('esEndText'),
        duration: $('esDuration'),
        durationText: $('esDurationText'),
        cfgPill: $('esCfgPill'),
        cfgText: $('esCfgText'),
        heroTitle: $('esHeroTitle'),
        heroSub: $('esHeroSub'),
        ctlNote: $('esCtlNote'),
        emergency: $('esEmergency'),
        emergencyLabel: $('esEmergencyLabel'),
        cancel: $('esCancel'),
        dirty: $('esDirty'),
        picker: $('esPicker'),
        newBtn: $('esNewBtn')
    };

    var phasePills = document.querySelectorAll('.js-phase');
    var phaseTexts = document.querySelectorAll('.js-phase-text');
    var saveButtons = document.querySelectorAll('.js-save');

    // ---- server theke ashe current state
    var state = {
        electionId: parseInt(root.getAttribute('data-election-id'), 10) || 0,
        isNew: root.getAttribute('data-new') === '1',
        phase: root.getAttribute('data-phase') || 'none',
        phaseText: root.getAttribute('data-phase-text') || '',
        isOpen: root.getAttribute('data-open') === '1'
    };

    var baseline = null;      // last saved values (cancel / dirty check er jonno)
    var busy = false;
    var leaving = false;      // page chere jachhe - beforeunload prompt dekhabe na

    // ================================================================ helpers
    function bn(n) {
        return String(n).replace(/\d/g, function (d) { return BN_DIGITS[+d]; });
    }

    function pad2(n) { return (n < 10 ? '0' : '') + n; }

    /** "2027-03-15T09:00" -> {y,m,d,h,mi,ts} (browser timezone er upor nirbhor kore na) */
    function parseLocal(v) {
        var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(v || '');
        if (!m) return null;
        var o = { y: +m[1], m: +m[2], d: +m[3], h: +m[4], mi: +m[5] };
        o.ts = Date.UTC(o.y, o.m - 1, o.d, o.h, o.mi);
        return o;
    }

    /** "১৫ মার্চ, ২০২৭ • ০৯:০০ AM" */
    function fmt(v) {
        var p = parseLocal(v);
        if (!p) return '';
        var h12 = p.h % 12;
        if (h12 === 0) h12 = 12;
        var ap = p.h < 12 ? 'AM' : 'PM';
        return bn(p.d) + ' ' + BN_MONTHS[p.m - 1] + ', ' + bn(p.y) + ' \u2022 ' +
            bn(pad2(h12)) + ':' + bn(pad2(p.mi)) + ' ' + ap;
    }

    function snapshot() {
        return {
            title: el.title.value,
            term: el.term.value,
            committee: el.committee.value,
            note: el.note.value,
            start: el.start.value,
            end: el.end.value
        };
    }

    function isDirty() {
        if (!baseline) return false;
        var s = snapshot();
        for (var k in s) {
            if (s.hasOwnProperty(k) && s[k] !== baseline[k]) return true;
        }
        return false;
    }

    function updateDirty() {
        if (el.dirty) el.dirty.classList.toggle('is-on', isDirty());
    }

    function token() {
        var t = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
        return t ? t.value : '';
    }

    // ---- SweetAlert2 (layout e already load kora ache); na thakle browser dialog
    function toast(icon, msg) {
        if (window.Swal) {
            window.Swal.fire({
                toast: true, position: 'top-end', icon: icon, title: msg,
                showConfirmButton: false, timer: 3200, timerProgressBar: true
            });
        } else {
            window.alert(msg);
        }
    }

    function alertBox(icon, title, text) {
        if (window.Swal) {
            window.Swal.fire({ icon: icon, title: title, text: text || '', confirmButtonText: 'ঠিক আছে', confirmButtonColor: '#0c3d33' });
        } else {
            window.alert(title + (text ? '\n' + text : ''));
        }
    }

    function confirmBox(title, text, okText, danger) {
        if (!window.Swal) {
            return Promise.resolve(window.confirm(title + '\n' + text));
        }
        return window.Swal.fire({
            title: title, text: text, icon: 'question',
            showCancelButton: true, reverseButtons: true, focusCancel: true,
            confirmButtonText: okText, cancelButtonText: 'না, থাক',
            confirmButtonColor: danger ? '#c62828' : '#0c3d33'
        }).then(function (r) { return !!r.isConfirmed; });
    }

    function navigate(url) {
        leaving = true;
        window.location.href = url;
    }

    // ================================================================ date fields + duration
    function refreshDates() {
        var sTxt = fmt(el.start.value);
        var eTxt = fmt(el.end.value);

        el.startText.textContent = sTxt || 'তারিখ ও সময় নির্বাচন করুন';
        el.startText.classList.toggle('is-empty', !sTxt);
        el.endText.textContent = eTxt || 'তারিখ ও সময় নির্বাচন করুন';
        el.endText.classList.toggle('is-empty', !eTxt);

        el.startBox.classList.remove('is-invalid');
        el.endBox.classList.remove('is-invalid');

        refreshDuration();
        updateDirty();
    }

    function setDuration(text, cls) {
        el.durationText.textContent = text;
        el.duration.className = 'es-pill ' + cls;
    }

    function refreshDuration() {
        var s = parseLocal(el.start.value);
        var e = parseLocal(el.end.value);

        if (!s || !e) {
            setDuration('মোট স্থায়িত্বকাল: \u2014', 'es-pill-muted');
            return;
        }

        if (e.ts <= s.ts) {
            setDuration('সমাপ্তির সময় শুরুর পরে হতে হবে', 'es-pill-red');
            return;
        }

        var mins = Math.round((e.ts - s.ts) / 60000);
        var hours = Math.floor(mins / 60);
        var rem = mins % 60;

        // দিন = ভোট যতগুলো ক্যালেন্ডার দিনের উপর দিয়ে চলবে (শুরু ও শেষের দিনসহ)
        var days = Math.round((Date.UTC(e.y, e.m - 1, e.d) - Date.UTC(s.y, s.m - 1, s.d)) / 86400000) + 1;

        var hText = hours > 0 ? bn(hours) + ' ঘণ্টা' : '';
        if (rem > 0) hText += (hText ? ' ' : '') + bn(rem) + ' মিনিট';

        setDuration('মোট স্থায়িত্বকাল: ' + bn(days) + ' দিন (' + hText + ')', 'es-pill-amber');
    }

    function openPicker(inp) {
        try {
            if (typeof inp.showPicker === 'function') {
                inp.showPicker();
                return;
            }
        } catch (e) { /* fallthrough */ }
        inp.focus();
    }

    function wireDateBox(box, inp) {
        box.addEventListener('click', function () { if (!LOCKED) openPicker(inp); });

        inp.addEventListener('keydown', function (ev) {
            if (ev.key === 'Enter' || ev.key === ' ' || ev.key === 'ArrowDown') {
                ev.preventDefault();
                openPicker(inp);
            }
        });

        inp.addEventListener('input', refreshDates);
        inp.addEventListener('change', refreshDates);
    }

    // ================================================================ hero + emergency card
    function heroSubText() {
        var parts = [];
        if (el.term.value.trim()) parts.push(el.term.value.trim());
        if (el.committee.value.trim()) parts.push(el.committee.value.trim());
        return parts.join('  \u2022  ');
    }

    function renderHero() {
        var has = state.electionId > 0;

        Array.prototype.forEach.call(phasePills, function (p) {
            p.className = 'es-pill es-phase es-phase-' + state.phase + ' js-phase';
        });
        Array.prototype.forEach.call(phaseTexts, function (t) {
            t.textContent = state.phaseText;
        });

        el.cfgText.textContent = has ? 'সক্রিয় কনফিগারেশন' : 'নতুন কনফিগারেশন';
    }

    function renderControl() {
        var has = state.electionId > 0;

        renderHero();

        if (!has) {
            el.emergency.disabled = true;
            el.emergencyLabel.textContent = 'জরুরি ভোট বন্ধ করুন';
            el.ctlNote.textContent = 'নির্বাচন তৈরি ও সংরক্ষণ করার পর এখানে জরুরি বন্ধের সুবিধা পাওয়া যাবে।';
            return;
        }

        if (state.isOpen) {
            el.emergency.disabled = busy || LOCKED;
            el.emergencyLabel.textContent = 'জরুরি ভোট বন্ধ করুন';

            if (state.phase === 'scheduled') {
                el.ctlNote.textContent = 'ভোটগ্রহণ নির্ধারিত শুরুর সময়ের অপেক্ষায় আছে। প্রয়োজনে এখনই বন্ধ করে দিতে পারেন।';
            } else if (state.phase === 'ended') {
                el.ctlNote.textContent = 'নির্ধারিত সময় শেষ হয়ে গেছে, তাই এখন কেউ ভোট দিতে পারছেন না। চাইলে নির্বাচনটি বন্ধ করে চূড়ান্ত করতে পারেন।';
            } else {
                el.ctlNote.textContent = 'এখন সদস্যরা ভোট দিতে পারছেন। জরুরি প্রয়োজনে বন্ধ করলে সঙ্গে সঙ্গে ভোটগ্রহণ থেমে যাবে।';
            }
        } else {
            el.emergency.disabled = true;
            el.emergencyLabel.textContent = 'ভোটগ্রহণ বন্ধ আছে';
            el.ctlNote.textContent = 'ভোটগ্রহণ এখন বন্ধ আছে। আবার চালু করতে সময়সূচি দিয়ে (প্রয়োজনে সময় পরিবর্তন করে) সংরক্ষণ করুন।';
        }
    }

    // ================================================================ apply state from server
    function applyServerState(st, keepForm) {
        state.electionId = st.electionId || 0;
        state.isNew = false;
        state.phase = st.phase || 'none';
        state.phaseText = st.phaseText || '';
        state.isOpen = !!st.isOpen;

        if (!keepForm) {
            el.title.value = st.title || '';
            el.term.value = st.term || '';
            el.committee.value = st.committee || '';
            el.note.value = st.note || '';
            el.start.value = st.startValue || '';
            el.end.value = st.endValue || '';

            baseline = snapshot();
            el.heroTitle.textContent = st.title || 'নির্বাচন';
            el.heroSub.textContent = heroSubText();
            refreshDates();
        }

        renderControl();
        updateDirty();
    }

    // ================================================================ validation
    function clearErrors() {
        [el.title, el.term, el.committee, el.note].forEach(function (x) { x.classList.remove('is-invalid'); });
        el.startBox.classList.remove('is-invalid');
        el.endBox.classList.remove('is-invalid');
    }

    function bad(node, msg) {
        node.classList.add('is-invalid');
        alertBox('warning', msg);
        if (node.focus && node.tagName !== 'DIV') node.focus();
        return false;
    }

    function validate() {
        clearErrors();

        if (!el.title.value.trim()) {
            return bad(el.title, 'নির্বাচনের নাম ও টাইটেল দিন।');
        }

        // ASP.NET request validation html-er moto lekha (<a, &#) ferot dey, tai age-i ber kore dei
        var all = [el.title.value, el.term.value, el.committee.value, el.note.value].join(' ');
        if (/<[a-z!\/?]|&#/i.test(all)) {
            return bad(el.note, '"<" বা "&#" চিহ্ন ব্যবহার করা যাবে না।');
        }

        var s = el.start.value, e = el.end.value;

        if (s && !e) return bad(el.endBox, 'ভোট সমাপ্তির তারিখ ও সময় নির্ধারণ করুন।');
        if (!s && e) return bad(el.startBox, 'ভোট শুরুর তারিখ ও সময় নির্ধারণ করুন।');

        if (s && e && parseLocal(e).ts <= parseLocal(s).ts) {
            el.startBox.classList.add('is-invalid');
            return bad(el.endBox, 'ভোট সমাপ্তির সময় অবশ্যই শুরুর সময়ের পরে হতে হবে।');
        }

        return true;
    }

    // ================================================================ network
    function post(url, params) {
        var body = new URLSearchParams();
        body.append('__RequestVerificationToken', token());
        Object.keys(params).forEach(function (k) { body.append(k, params[k]); });

        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: body.toString()
        }).then(function (res) {
            return res.text().then(function (txt) {
                try { return JSON.parse(txt); }
                catch (e) { throw new Error('bad-response'); }
            });
        });
    }

    function setBusy(on, label) {
        busy = on;

        Array.prototype.forEach.call(saveButtons, function (b) {
            b.disabled = on || LOCKED;
            var l = b.querySelector('.es-btn-label');
            if (l) {
                if (!l.hasAttribute('data-orig')) l.setAttribute('data-orig', l.textContent);
                l.textContent = on ? label : l.getAttribute('data-orig');
            }
        });

        el.cancel.disabled = on || LOCKED;
        renderControl();
    }

    // ================================================================ actions
    function save() {
        if (busy || LOCKED) return;
        if (!validate()) return;

        var creating = state.electionId <= 0;
        setBusy(true, creating ? 'তৈরি হচ্ছে...' : 'সংরক্ষণ হচ্ছে...');

        post(SAVE_URL, {
            electionId: state.electionId,
            title: el.title.value,
            term: el.term.value,
            committee: el.committee.value,
            note: el.note.value,
            startAt: el.start.value,
            endAt: el.end.value
        }).then(function (res) {
            if (res && res.success) {
                toast('success', res.message || 'সংরক্ষণ করা হয়েছে।');

                // notun election toiri hole oi election er page e niye jai (list o refresh hoy)
                if (res.created && res.redirectUrl) {
                    baseline = snapshot();
                    updateDirty();
                    setTimeout(function () { navigate(res.redirectUrl); }, 700);
                    return;
                }

                setBusy(false);
                applyServerState(res.state, false);
            } else {
                setBusy(false);
                alertBox('error', (res && res.message) || 'সংরক্ষণ করা যায়নি।');
            }
        }).catch(function () {
            setBusy(false);
            alertBox('error', 'সংরক্ষণ করা যায়নি।', 'ইন্টারনেট সংযোগ বা সেশন যাচাই করে আবার চেষ্টা করুন।');
        });
    }

    function cancelChanges() {
        if (busy || LOCKED) return;

        if (!isDirty()) {
            toast('info', 'কোনো অসংরক্ষিত পরিবর্তন নেই।');
            return;
        }

        confirmBox('পরিবর্তন বাতিল করবেন?', 'সংরক্ষণ না করা সব পরিবর্তন মুছে যাবে।', 'হ্যাঁ, বাতিল করুন', true)
            .then(function (ok) {
                if (!ok) return;
                el.title.value = baseline.title;
                el.term.value = baseline.term;
                el.committee.value = baseline.committee;
                el.note.value = baseline.note;
                el.start.value = baseline.start;
                el.end.value = baseline.end;
                clearErrors();
                refreshDates();
            });
    }

    function emergencyClose() {
        if (busy || LOCKED || state.electionId <= 0 || !state.isOpen) return;

        confirmBox('জরুরি ভিত্তিতে ভোট বন্ধ করবেন?',
            'বন্ধ করলে সঙ্গে সঙ্গে আর কেউ ভোট দিতে পারবেন না। আবার চালু করতে সময়সূচি দিয়ে সংরক্ষণ করতে হবে।',
            'হ্যাঁ, এখনই বন্ধ করুন', true)
            .then(function (ok) {
                if (!ok) return;

                busy = true;
                renderControl();

                post(CLOSE_URL, { electionId: state.electionId })
                    .then(function (res) {
                        busy = false;
                        if (res && res.success) {
                            // form er lekha (asonkhito) jaate na mushe, shudhu status bodlai
                            applyServerState(res.state, true);
                            toast('success', res.message);
                        } else {
                            renderControl();
                            alertBox('error', (res && res.message) || 'ভোটগ্রহণ বন্ধ করা যায়নি।');
                        }
                    })
                    .catch(function () {
                        busy = false;
                        renderControl();
                        alertBox('error', 'ভোটগ্রহণ বন্ধ করা যায়নি।', 'ইন্টারনেট সংযোগ বা সেশন যাচাই করে আবার চেষ্টা করুন।');
                    });
            });
    }

    // ================================================================ election switcher
    function goTo(url, revert) {
        if (busy) { if (revert) revert(); return; }

        if (!isDirty()) {
            navigate(url);
            return;
        }

        confirmBox('অসংরক্ষিত পরিবর্তন আছে', 'পেজ ছেড়ে গেলে সংরক্ষণ না করা পরিবর্তনগুলো মুছে যাবে। চালিয়ে যাবেন?',
            'হ্যাঁ, চালিয়ে যান', true)
            .then(function (ok) {
                if (ok) navigate(url);
                else if (revert) revert();
            });
    }

    function initSwitcher() {
        if (el.picker) {
            var initial = el.picker.value;

            el.picker.addEventListener('change', function () {
                var v = el.picker.value;
                var url = v === 'new' ? BASE_URL + '?mode=new' : BASE_URL + '?id=' + encodeURIComponent(v);
                goTo(url, function () { el.picker.value = initial; });
            });
        }

        if (el.newBtn) {
            el.newBtn.addEventListener('click', function (ev) {
                ev.preventDefault();
                goTo(el.newBtn.getAttribute('href'));
            });
        }
    }

    // ================================================================ init
    if (!('showPicker' in HTMLInputElement.prototype)) {
        root.classList.add('es-nopicker');     // purono browser: native picker field er upor bose thake
    }

    wireDateBox(el.startBox, el.start);
    wireDateBox(el.endBox, el.end);

    [el.title, el.term, el.committee, el.note].forEach(function (x) {
        x.addEventListener('input', function () {
            x.classList.remove('is-invalid');
            updateDirty();
        });
    });

    Array.prototype.forEach.call(saveButtons, function (b) { b.addEventListener('click', save); });
    el.cancel.addEventListener('click', cancelChanges);
    el.emergency.addEventListener('click', emergencyClose);
    initSwitcher();

    window.addEventListener('beforeunload', function (ev) {
        if (!leaving && !busy && baseline && isDirty()) {
            ev.preventDefault();
            ev.returnValue = '';
        }
    });

    if (LOCKED) {
        [el.title, el.term, el.committee, el.note, el.start, el.end].forEach(function (x) { x.disabled = true; });
        Array.prototype.forEach.call(saveButtons, function (b) { b.disabled = true; });
        el.cancel.disabled = true;
    }

    baseline = snapshot();
    refreshDates();
    renderControl();
    updateDirty();
})();
