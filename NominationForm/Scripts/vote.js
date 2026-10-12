/* ================================================================
   Staff Welfare Fund - Online Voting Portal
   Ballot page logic + confirmation page effects
   Needs: SweetAlert2 (loaded in _VoteLayout)
   ================================================================ */
(function () {
    'use strict';

    var BN = ['০', '১', '২', '৩', '৪', '৫', '৬', '৭', '৮', '৯'];

    function bn(n) {
        return String(n).replace(/\d/g, function (d) { return BN[+d]; });
    }

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // ------------------------------------------------------------ SweetAlert helpers
    var BRAND = '#063a32';

    function toast(icon, title) {
        if (!window.Swal) { window.alert(title); return; }
        window.Swal.fire({
            toast: true,
            position: 'top',
            icon: icon,
            title: title,
            showConfirmButton: false,
            timer: 2600,
            timerProgressBar: true,
            customClass: { popup: 'swf-toast' }
        });
    }

    // ------------------------------------------------------------ scroll reveal (both pages)
    function initReveal() {
        var items = document.querySelectorAll('.reveal');
        if (!items.length) return;

        if (!('IntersectionObserver' in window) || reduceMotion) {
            for (var i = 0; i < items.length; i++) items[i].classList.add('in');
            return;
        }

        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (e) {
                if (e.isIntersecting) {
                    e.target.classList.add('in');
                    io.unobserve(e.target);
                }
            });
        }, { threshold: 0.08, rootMargin: '0px 0px -40px 0px' });

        for (var j = 0; j < items.length; j++) io.observe(items[j]);
    }

    // ================================================================
    //  BALLOT PAGE
    // ================================================================
    function initBallot(app) {
        var form = document.getElementById('voteForm');
        var tokenEl = form.querySelector('input[name="__RequestVerificationToken"]');
        var submitUrl = app.getAttribute('data-submit-url');
        var electionId = app.getAttribute('data-election');
        var total = parseInt(app.getAttribute('data-total'), 10) || 0;

        var el = {
            pgCount: document.getElementById('pgCount'),
            pgFill: document.getElementById('pgFill'),
            pgLeft: document.getElementById('pgLeft'),
            pgPct: document.getElementById('pgPct'),
            barVotes: document.getElementById('barVotes'),
            barPosts: document.getElementById('barPosts'),
            btnSubmit: document.getElementById('btnSubmit'),
            btnReset: document.getElementById('btnReset')
        };

        var locked = false;      // true after a successful submit (disables "leave page" warning)
        var shownVotes = 0;

        // ---- collect sections
        var sections = [];
        var secEls = app.querySelectorAll('.v-post');
        for (var i = 0; i < secEls.length; i++) {
            (function (secEl) {
                sections.push({
                    el: secEl,
                    id: secEl.getAttribute('data-post-id'),
                    name: secEl.getAttribute('data-name'),
                    max: parseInt(secEl.getAttribute('data-max'), 10) || 1,
                    cards: [].slice.call(secEl.querySelectorAll('.v-cand')),
                    state: secEl.querySelector('.js-state'),
                    chip: secEl.querySelector('.js-chip-count'),
                    selected: []                      // candidate ids
                });
            })(secEls[i]);
        }

        function indexOf(sec, id) { return sec.selected.indexOf(id); }

        function setCard(card, on) {
            card.classList.toggle('selected', on);
            card.setAttribute('aria-checked', on ? 'true' : 'false');
        }

        // ---- animated number
        function tweenCount(to) {
            var node = el.pgCount;
            if (!node) return;
            var from = shownVotes;
            shownVotes = to;

            if (reduceMotion || from === to) { node.textContent = bn(to); return; }

            node.classList.remove('bump');
            void node.offsetWidth;
            node.classList.add('bump');

            var start = null, dur = 420;
            function step(ts) {
                if (start === null) start = ts;
                var p = Math.min((ts - start) / dur, 1);
                node.textContent = bn(Math.round(from + (to - from) * p));
                if (p < 1) window.requestAnimationFrame(step);
            }
            window.requestAnimationFrame(step);
        }

        // ---- refresh every counter on the page
        function update() {
            var votes = 0, donePosts = 0;

            sections.forEach(function (s) {
                var n = s.selected.length;
                var done = n === s.max;
                votes += n;
                if (done) donePosts++;

                s.el.classList.toggle('is-done', done);

                if (s.max > 1) {
                    if (s.state) {
                        s.state.textContent = bn(n) + ' / ' + bn(s.max);
                        s.state.classList.toggle('is-done', done);
                    }
                    if (s.chip) s.chip.textContent = bn(n);
                } else if (s.state) {
                    var was = s.state.classList.contains('is-done');
                    s.state.textContent = done ? 'নির্বাচিত ✓' : 'অনির্বাচিত';
                    s.state.classList.toggle('is-done', done);
                    if (done && !was) {
                        s.state.style.animation = 'none';
                        void s.state.offsetWidth;
                        s.state.style.animation = '';
                    }
                }
            });

            var pct = total ? Math.round(votes * 100 / total) : 0;
            var left = sections.length - donePosts;

            tweenCount(votes);
            if (el.pgFill) el.pgFill.style.width = pct + '%';
            if (el.pgLeft) el.pgLeft.textContent = bn(left);
            if (el.pgPct) el.pgPct.textContent = bn(pct) + '%';

            if (el.barVotes) {
                el.barVotes.textContent = bn(votes) + '/' + bn(total);
                var pill = el.barVotes.parentNode;
                pill.classList.toggle('full', votes === total && total > 0);
                pill.classList.remove('bump');
                void pill.offsetWidth;
                pill.classList.add('bump');
            }
            if (el.barPosts) el.barPosts.textContent = bn(sections.length) + 'টি পদের মধ্যে ' + bn(donePosts) + 'টি সম্পন্ন';

            if (el.btnSubmit) el.btnSubmit.classList.toggle('ready', votes === total && total > 0);
        }

        // ---- click / key on a candidate card
        function ripple(card, ev) {
            if (reduceMotion) return;
            var r = card.getBoundingClientRect();
            var size = Math.max(r.width, r.height) * 1.25;
            var x = (ev && ev.clientX ? ev.clientX - r.left : r.width / 2) - size / 2;
            var y = (ev && ev.clientY ? ev.clientY - r.top : r.height / 2) - size / 2;
            var s = document.createElement('span');
            s.className = 'v-ripple';
            s.style.width = s.style.height = size + 'px';
            s.style.left = x + 'px';
            s.style.top = y + 'px';
            card.appendChild(s);
            window.setTimeout(function () { if (s.parentNode) s.parentNode.removeChild(s); }, 700);
        }

        function toggle(sec, card, ev) {
            var id = card.getAttribute('data-id');
            var at = indexOf(sec, id);

            if (at > -1) {                                     // un-select
                sec.selected.splice(at, 1);
                setCard(card, false);
            } else {
                if (sec.max === 1) {                           // single choice: replace
                    sec.cards.forEach(function (c) { setCard(c, false); });
                    sec.selected = [];
                } else if (sec.selected.length >= sec.max) {   // multi choice: limit reached
                    card.classList.remove('shake');
                    void card.offsetWidth;
                    card.classList.add('shake');
                    toast('warning', 'এই পদে সর্বোচ্চ ' + bn(sec.max) + ' জনকে ভোট দেওয়া যাবে। আগে একজনের নির্বাচন বাতিল করুন।');
                    return;
                }
                sec.selected.push(id);
                setCard(card, true);
                ripple(card, ev);
            }
            update();
        }

        sections.forEach(function (sec) {
            sec.cards.forEach(function (card) {
                card.addEventListener('click', function (ev) { toggle(sec, card, ev); });
                card.addEventListener('keydown', function (ev) {
                    if (ev.key === 'Enter' || ev.key === ' ') {
                        ev.preventDefault();
                        toggle(sec, card, null);
                    }
                });
            });
        });

        // ---- helpers for submit
        function missingSections() {
            return sections.filter(function (s) { return s.selected.length !== s.max; });
        }

        function allIds() {
            var ids = [];
            sections.forEach(function (s) { ids = ids.concat(s.selected); });
            return ids;
        }

        function focusSection(sec) {
            var top = sec.el.getBoundingClientRect().top + window.pageYOffset - 80;
            window.scrollTo({ top: top, behavior: reduceMotion ? 'auto' : 'smooth' });
            sec.el.classList.remove('flash');
            void sec.el.offsetWidth;
            sec.el.classList.add('flash');
            window.setTimeout(function () { sec.el.classList.remove('flash'); }, 1500);
        }

        function nameOf(sec, id) {
            for (var i = 0; i < sec.cards.length; i++) {
                if (sec.cards[i].getAttribute('data-id') === id) return sec.cards[i].getAttribute('data-name');
            }
            return '';
        }

        function send() {
            var body = new URLSearchParams();
            body.append('__RequestVerificationToken', tokenEl ? tokenEl.value : '');
            body.append('electionId', electionId);
            allIds().forEach(function (id) { body.append('candidateIds', id); });

            return fetch(submitUrl, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: body.toString()
            }).then(function (r) {
                return r.json();
            }).catch(function () {
                return { success: false, message: 'সার্ভারের সাথে যোগাযোগ করা যায়নি। ইন্টারনেট সংযোগ দেখে আবার চেষ্টা করুন।' };
            });
        }

        // ---- SUBMIT button
        el.btnSubmit.addEventListener('click', function () {
            if (!window.Swal) { window.alert('পেজ সম্পূর্ণ লোড হয়নি, রিফ্রেশ করুন।'); return; }

            var miss = missingSections();

            // 1) validation: every position must have the exact number of votes
            if (miss.length) {
                var html = '<div class="swf-miss">' + miss.map(function (s) {
                    var n = s.selected.length;
                    var need = s.max - n;
                    var txt = n < s.max
                        ? bn(need) + ' জন প্রার্থী নির্বাচন বাকি'
                        : bn(n - s.max) + ' জন বেশি নির্বাচিত';
                    return '<div class="row"><span>' + esc(s.name) + '</span><b>' + txt + '</b></div>';
                }).join('') + '</div>';

                window.Swal.fire({
                    icon: 'error',
                    title: 'ভোট অসম্পূর্ণ!',
                    html: '<p style="margin:0 0 10px;color:#5b6d69;font-size:14px">সব পদে নির্ধারিত সংখ্যক ভোট না দিলে ব্যালট গ্রহণ করা হবে না।</p>' + html,
                    confirmButtonText: 'ঠিক আছে, ভোট সম্পন্ন করি',
                    confirmButtonColor: BRAND,
                    customClass: { popup: 'swf-popup' }
                }).then(function () { focusSection(miss[0]); });
                return;
            }

            // 2) sure ? (Yes -> send to server -> confirmation page / No -> stay here)
            var summary = '<div class="swf-sum">' + sections.map(function (s) {
                return '<div class="row"><span>' + esc(s.name) + '</span><b>' +
                    s.selected.map(function (id) { return esc(nameOf(s, id)); }).join('، ') + '</b></div>';
            }).join('') + '</div>';

            window.Swal.fire({
                icon: 'question',
                title: 'আপনি কি নিশ্চিত?',
                html: '<p style="margin:0 0 10px;color:#5b6d69;font-size:14px">জমা দেওয়ার পর আপনার ভোট আর পরিবর্তন বা প্রত্যাহার করা যাবে না।</p>' + summary,
                showCancelButton: true,
                confirmButtonText: 'হ্যাঁ, ভোট জমা দিন',
                cancelButtonText: 'না, ফিরে যাই',
                confirmButtonColor: BRAND,
                cancelButtonColor: '#8a9894',
                reverseButtons: true,
                focusCancel: true,
                showLoaderOnConfirm: true,
                allowOutsideClick: function () { return !window.Swal.isLoading(); },
                allowEscapeKey: function () { return !window.Swal.isLoading(); },
                customClass: { popup: 'swf-popup' },
                preConfirm: function () {
                    el.btnSubmit.disabled = true;
                    return send().then(function (res) {
                        if (!res || !res.success) {
                            el.btnSubmit.disabled = false;
                            window.Swal.showValidationMessage((res && res.message) || 'ভোট জমা দেওয়া যায়নি।');
                            return false;
                        }
                        return res;
                    });
                }
            }).then(function (result) {
                if (result.isConfirmed && result.value && result.value.success) {
                    locked = true;
                    window.location.href = result.value.redirectUrl;
                }
            });
        });

        // ---- RESET button
        el.btnReset.addEventListener('click', function () {
            var any = sections.some(function (s) { return s.selected.length > 0; });
            if (!any) { toast('info', 'আপনি এখনো কোনো প্রার্থী নির্বাচন করেননি।'); return; }
            if (!window.Swal) return;

            window.Swal.fire({
                icon: 'warning',
                title: 'সব নির্বাচন মুছে ফেলবেন?',
                text: 'আপনার এখন পর্যন্ত করা সব নির্বাচন রিসেট হয়ে যাবে।',
                showCancelButton: true,
                confirmButtonText: 'হ্যাঁ, রিসেট করুন',
                cancelButtonText: 'না',
                confirmButtonColor: BRAND,
                reverseButtons: true,
                customClass: { popup: 'swf-popup' }
            }).then(function (r) {
                if (!r.isConfirmed) return;
                sections.forEach(function (s) {
                    s.cards.forEach(function (c) { setCard(c, false); });
                    s.selected = [];
                });
                update();
                window.scrollTo({ top: 0, behavior: reduceMotion ? 'auto' : 'smooth' });
            });
        });

        // ---- warn before leaving with an unsent ballot
        window.addEventListener('beforeunload', function (e) {
            if (locked || window.__voteClosed) return;
            var any = sections.some(function (s) { return s.selected.length > 0; });
            if (any) {
                e.preventDefault();
                e.returnValue = '';
            }
        });

        update();
        shownVotes = 0;
    }

    // ================================================================
    //  CONFIRMATION PAGE
    // ================================================================
    function initDone(card) {
        var codeEl = document.getElementById('receiptCode');
        var btnCopy = document.getElementById('btnCopy');
        var btnPrint = document.getElementById('btnPrint');

        if (btnCopy && codeEl) {
            btnCopy.addEventListener('click', function () {
                var text = codeEl.textContent.trim();

                function ok() {
                    var label = btnCopy.querySelector('span');
                    var old = label.textContent;
                    btnCopy.classList.add('copied');
                    label.textContent = 'কপি হয়েছে ✓';
                    toast('success', 'রশিদ নম্বর কপি করা হয়েছে।');
                    window.setTimeout(function () {
                        btnCopy.classList.remove('copied');
                        label.textContent = old;
                    }, 2200);
                }

                function fallback() {
                    var ta = document.createElement('textarea');
                    ta.value = text;
                    ta.style.position = 'fixed';
                    ta.style.opacity = '0';
                    document.body.appendChild(ta);
                    ta.select();
                    try { document.execCommand('copy'); ok(); } catch (e) { toast('error', 'কপি করা যায়নি।'); }
                    document.body.removeChild(ta);
                }

                if (navigator.clipboard && window.isSecureContext) {
                    navigator.clipboard.writeText(text).then(ok, fallback);
                } else {
                    fallback();
                }
            });
        }

        if (btnPrint) {
            btnPrint.addEventListener('click', function () { window.print(); });
        }

        if (!reduceMotion) confetti(card);
    }

    // lightweight confetti (no library)
    function confetti(card) {
        var cv = document.getElementById('confetti');
        if (!cv || !cv.getContext) return;

        var ctx = cv.getContext('2d');
        var w = cv.width = card.offsetWidth;
        var h = cv.height = card.offsetHeight;
        var colors = ['#d9b25a', '#ecd391', '#1d6e5c', '#0c5b4e', '#9bd3bd', '#f4e7bd'];
        var parts = [];

        for (var i = 0; i < 90; i++) {
            parts.push({
                x: w / 2 + (Math.random() - 0.5) * 80,
                y: 120,
                vx: (Math.random() - 0.5) * 9,
                vy: -Math.random() * 8 - 3,
                s: Math.random() * 6 + 4,
                r: Math.random() * Math.PI,
                vr: (Math.random() - 0.5) * 0.3,
                c: colors[(Math.random() * colors.length) | 0]
            });
        }

        var frames = 0;
        (function tick() {
            ctx.clearRect(0, 0, w, h);
            parts.forEach(function (p) {
                p.vy += 0.22;
                p.x += p.vx;
                p.y += p.vy;
                p.r += p.vr;
                ctx.save();
                ctx.translate(p.x, p.y);
                ctx.rotate(p.r);
                ctx.globalAlpha = Math.max(0, 1 - frames / 170);
                ctx.fillStyle = p.c;
                ctx.fillRect(-p.s / 2, -p.s / 4, p.s, p.s / 2);
                ctx.restore();
            });
            frames++;
            if (frames < 170) window.requestAnimationFrame(tick);
            else ctx.clearRect(0, 0, w, h);
        })();
    }

    // ------------------------------------------------------------ boot
    document.addEventListener('DOMContentLoaded', function () {
        initReveal();

        var app = document.getElementById('voteApp');
        if (app && document.getElementById('voteForm')) initBallot(app);

        var done = document.getElementById('doneCard');
        if (done) initDone(done);
    });
})();
