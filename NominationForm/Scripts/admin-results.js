/* ================================================================
   Admin > নির্বাচনী ফলাফল (live vote count + final result)
   - page load e prothom render (window.RESULTS_INIT)
   - Running hole 5 second por por /Admin/ResultsData poll kore
   - vote bodlale: count-up, bar animation, row flash, +N chip, lead-change toast
   - Ended hole polling bondho, final result thake
   ================================================================ */
(function () {
    'use strict';

    var cfg = window.RESULTS_CFG;
    var data = window.RESULTS_INIT;
    if (!cfg || !data) return;

    var PHASE = { None: 0, NotStarted: 1, Running: 2, Ended: 3 };
    var POLL_RUNNING = 5000;
    var POLL_WAITING = 15000;

    var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    var page = document.getElementById('rsPage');
    var elGrid = document.getElementById('rsGrid');
    var elStats = document.getElementById('rsStats');
    var elSummary = document.getElementById('rsSummary');
    var elTies = document.getElementById('rsTies');
    if (!page || !elGrid || !elStats || !elSummary) return;

    var state = {
        first: true,
        busy: false,
        timer: null,
        failures: 0,
        version: null,
        lastBallots: null,
        lastPhase: null,
        prevVotes: {},          // candidateId -> votes
        prevPct: {},            // candidateId -> percent
        leaders: {},            // postId -> candidateId
        expanded: {},           // postId -> true
        cards: {},              // postId -> element
        approvedAt: null
    };

    /* =========================================================
       Helpers
       ========================================================= */
    function $(id) { return document.getElementById(id); }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function bn(v) {
        return String(v == null ? '' : v).replace(/[0-9]/g, function (d) {
            return '০১২৩৪৫৬৭৮৯'.charAt(+d);
        });
    }

    function fmt(n) {
        return bn(Math.round(n).toLocaleString('en-US'));
    }

    function pct(p) {
        return bn((p || 0).toFixed(1)) + '%';
    }

    function pad2(n) {
        return n < 10 ? '0' + n : String(n);
    }

    var ORD = ['', '১ম', '২য়', '৩য়', '৪র্থ', '৫ম', '৬ষ্ঠ', '৭ম', '৮ম', '৯ম', '১০ম'];
    function ord(n) { return ORD[n] || (bn(n) + 'ম'); }

    var PREFIX = ['ড.', 'ডা.', 'ডাঃ', 'মোঃ', 'মো:', 'মো.', 'মোছাঃ', 'মোসাঃ', 'মোসা:', 'মোসাম্মৎ',
        'প্রফেসর', 'ইঞ্জিনিয়ার', 'ইঞ্জি.', 'জনাব', 'মিসেস', 'মিস', 'মিঃ',
        'dr.', 'md.', 'mr.', 'mrs.', 'engr.', 'prof.'];

    function firstGrapheme(w) {
        if (!w) return '';
        try {
            if (window.Intl && Intl.Segmenter) {
                var seg = new Intl.Segmenter('bn', { granularity: 'grapheme' }).segment(w)[Symbol.iterator]().next();
                if (seg && seg.value) return seg.value.segment;
            }
        } catch (e) { /* fallback below */ }
        return Array.from(w)[0] || '';
    }

    function initials(name) {
        var words = String(name || '').replace(/[()]/g, ' ').split(/\s+/).filter(Boolean);
        var clean = words.filter(function (w) { return PREFIX.indexOf(w.toLowerCase()) < 0; });
        if (!clean.length) clean = words;
        var out = firstGrapheme(clean[0]);
        if (clean.length > 1) out += firstGrapheme(clean[1]);
        return out.toUpperCase();
    }

    function photoSrc(c) {
        return String(cfg.photoUrl || '').replace(/\/+$/, '') + '/' + c.candidateId;
    }

    function avatar(c, small) {
        var img = c.hasPhoto
            ? '<img src="' + esc(photoSrc(c)) + '" alt="" loading="lazy" decoding="async" />'
            : '';
        return '<span class="rs-av' + (small ? ' is-sm' : '') + '">' + esc(initials(c.name)) + img + '</span>';
    }

    function postTitle(p) {
        return esc(p.displayName) + (p.displayNameEn ? ' (' + esc(p.displayNameEn) + ')' : '');
    }

    /* ---- icons ---- */
    var IC = {
        trophy: '<svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 21h8M12 17v4M7 4h10v5a5 5 0 0 1-10 0z"/><path d="M17 5h3v2a3 3 0 0 1-3 3M7 5H4v2a3 3 0 0 0 3 3"/></svg>',
        up: '<svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 17l6-6 4 4 8-8"/><path d="M15 7h6v6"/></svg>',
        ballot: '<svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="13" width="18" height="8" rx="2"/><path d="M8 13V8l4-4 4 4v5"/><path d="M10 17h4"/></svg>',
        list: '<svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6l2 2 3-3M3 13l2 2 3-3M3 20l2 2 3-3" transform="translate(0 -3)"/><path d="M12 6h9M12 13h9M12 20h9" transform="translate(0 -1)"/></svg>',
        gavel: '<svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="M14 13l-8.5 8.5a2.1 2.1 0 0 1-3-3L11 10"/><path d="M16 16l6-6M8 8l6-6M9 7l8 8M21 11l-8-8"/></svg>',
        table: '<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 10h18M9 4v16"/></svg>',
        check: '<svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M8.5 12.5l2.5 2.5 4.5-5"/></svg>'
    };

    /* =========================================================
       Number count-up
       ========================================================= */
    function countUp(el, from, to, dur) {
        if (!el) return;
        if (reduce || from === to) { el.textContent = fmt(to); return; }
        var start = null;
        dur = dur || 900;
        if (el._raf) cancelAnimationFrame(el._raf);

        function step(ts) {
            if (start === null) start = ts;
            var t = Math.min(1, (ts - start) / dur);
            var e = 1 - Math.pow(1 - t, 3);
            el.textContent = fmt(from + (to - from) * e);
            if (t < 1) el._raf = requestAnimationFrame(step);
            else el.textContent = fmt(to);
        }
        el.textContent = fmt(from);
        el._raf = requestAnimationFrame(step);
    }

    /* =========================================================
       Toast
       ========================================================= */
    var toastTimer = null;
    function toast(msg, isError) {
        var t = $('rsToast');
        if (!t) return;
        clearTimeout(toastTimer);
        t.textContent = msg;
        t.hidden = false;
        t.className = 'rs-toast show' + (isError ? ' is-error' : '');
        toastTimer = setTimeout(function () {
            t.className = 'rs-toast hide' + (isError ? ' is-error' : '');
            setTimeout(function () { t.hidden = true; }, 360);
        }, 3800);
    }

    /* =========================================================
       Header + stats
       ========================================================= */
    function buildStatsShell() {
        elStats.innerHTML =
            '<div class="rs-stat" style="--i:0">' +
            '  <div><div class="rs-stat-label">মোট প্রদত্ত / কাস্ট ভোট</div>' +
            '  <div class="rs-stat-main"><span class="rs-stat-num" id="stNum1">০</span><span class="rs-stat-tag" id="stTag1"></span></div>' +
            '  <div class="rs-stat-sub" id="stSub1"></div></div>' +
            '  <span class="rs-stat-ic is-gold">' + IC.ballot + '</span>' +
            '</div>' +
            '<div class="rs-stat" style="--i:1">' +
            '  <div><div class="rs-stat-label">গণনা সম্পন্ন পদ সমূহ</div>' +
            '  <div class="rs-stat-main"><span class="rs-stat-num"><span id="stNum2">০</span> / <span id="stTot2">০</span></span><span class="rs-stat-tag" id="stTag2"></span></div>' +
            '  <div class="rs-stat-sub" id="stSub2"></div></div>' +
            '  <span class="rs-stat-ic is-green">' + IC.list + '</span>' +
            '</div>' +
            '<div class="rs-stat" style="--i:2">' +
            '  <div><div class="rs-stat-label">নির্বাচনী নিরীক্ষা ও বৈধতা</div>' +
            '  <div class="rs-stat-main"><span class="rs-stat-num"><span id="stNum3">০</span> <span id="stWord3">আপত্তি</span></span><span class="rs-stat-tag" id="stTag3"></span></div>' +
            '  <div class="rs-stat-sub" id="stSub3"></div></div>' +
            '  <span class="rs-stat-ic">' + IC.gavel + '</span>' +
            '</div>';

        // halka 3D tilt (sudhu mouse thakle)
        if (!reduce && window.matchMedia && window.matchMedia('(hover: hover)').matches) {
            Array.prototype.forEach.call(elStats.querySelectorAll('.rs-stat'), function (c) {
                c.addEventListener('pointermove', function (e) {
                    var r = c.getBoundingClientRect();
                    var x = (e.clientX - r.left) / r.width - .5;
                    var y = (e.clientY - r.top) / r.height - .5;
                    c.style.transform = 'perspective(800px) rotateX(' + (-y * 4).toFixed(2) + 'deg) rotateY(' + (x * 5).toFixed(2) + 'deg) translateY(-2px)';
                });
                c.addEventListener('pointerleave', function () { c.style.transform = ''; });
            });
        }
    }

    function updateHeader(d) {
        var live = $('pillLive'), liveText = $('pillLiveText');
        live.className = 'rs-pill rs-pill-live';
        if (d.phase === PHASE.Running) {
            live.classList.add('is-live');
            liveText.textContent = 'লাইভ ভোট গণনা ও ফলাফল প্রকাশ';
        } else if (d.phase === PHASE.Ended) {
            live.classList.add('is-ended');
            liveText.textContent = 'ভোটগ্রহণ সমাপ্ত — চূড়ান্ত ফলাফল';
        } else {
            live.classList.add('is-wait');
            liveText.textContent = 'ভোটগ্রহণ এখনো শুরু হয়নি';
        }

        $('pillTimeText').textContent =
            (d.phase === PHASE.Running ? 'সর্বশেষ লাইভ আপডেট: ' : 'ফলাফল হালনাগাদ: ') + (d.updatedText || '—');

        var done = d.totalPosts > 0 ? Math.round(d.countedPosts * 100 / d.totalPosts) : 0;
        $('pillAuditText').textContent = bn(done) + '% ডিজিটাল নিরীক্ষিত' + (d.gazetteApproved ? ' · গেজেট অনুমোদিত' : '');

        $('sessionChip').textContent = bn(d.electionYear) + ' সেশন';

        $('rsDesc').textContent = (d.title ? d.title + '। ' : '') + 'সর্বমোট ' + bn(d.totalPosts) + 'টি পদের ' +
            (d.phase === PHASE.Ended ? 'পূর্ণাঙ্গ ও আনুষ্ঠানিক নিরীক্ষিত ফলাফল।' : 'সাময়িক ফলাফল — ভোট গণনা চলমান।');

        $('secTitle').textContent = 'নির্বাচনী পদভিত্তিক ফলাফল (সকল ' + bn(d.totalPosts) + 'টি পদ)';
        $('secNote').textContent = 'সর্বমোট ' + bn(d.totalPosts) + 'টি পদের আনুষ্ঠানিক প্রতিদ্বন্দ্বিতা ও ফলাফল চিত্র';

        page.classList.toggle('is-live', d.phase === PHASE.Running);

        var tb = $('btnTie');
        if (tb) {
            var tn = d.phase === PHASE.Ended ? d.issueCount : 0;
            tb.textContent = tn > 0 ? 'টাই সমাধান (' + bn(tn) + ')' : 'টাই সমাধান';
            tb.classList.toggle('is-alert', tn > 0);
        }

        var g = $('btnGazette');
        if (g) g.classList.toggle('is-locked', d.phase !== PHASE.Ended || d.issueCount > 0);
    }

    function updateStats(d) {
        var prevBallots = state.lastBallots == null ? 0 : state.lastBallots;
        countUp($('stNum1'), state.first ? 0 : prevBallots, d.totalBallots);

        if (d.eligibleVoters > 0) {
            $('stTag1').textContent = pct(d.turnout) + ' টার্নআউট';
            $('stSub1').textContent = 'মোট ভোটার: ' + fmt(d.eligibleVoters) + ' জনের মধ্যে প্রদত্ত';
        } else {
            $('stTag1').textContent = '';
            $('stSub1').textContent = 'এ পর্যন্ত জমা পড়া মোট ব্যালট';
        }

        var n2 = $('stNum2');
        countUp(n2, state.first ? 0 : (n2._v || 0), d.countedPosts, 700);
        n2._v = d.countedPosts;
        $('stTot2').textContent = bn(d.totalPosts);
        var cp = d.totalPosts > 0 ? Math.round(d.countedPosts * 100 / d.totalPosts) : 0;
        $('stTag2').textContent = bn(cp) + '% গণনাকৃত';
        $('stSub2').textContent = d.phase === PHASE.Ended
            ? 'সকল ' + bn(d.totalPosts) + 'টি পদের ব্যালট বাক্স খুলে গণনা সম্পন্ন'
            : 'ব্যালট জমা পড়ার সাথে সাথে লাইভ গণনা চলছে';

        var n3 = $('stNum3');
        countUp(n3, state.first ? 0 : (n3._v || 0), d.issueCount, 600);
        n3._v = d.issueCount;
        var tag3 = $('stTag3');
        if (d.issueCount > 0) {
            var endedNow = d.phase === PHASE.Ended;
            $('stWord3').textContent = 'টাই';
            tag3.textContent = endedNow ? 'সমাধান প্রয়োজন' : 'সাময়িক টাই';
            tag3.classList.add('is-warn');
            $('stSub3').textContent = endedNow
                ? 'টাই সমাধান না হওয়া পর্যন্ত গেজেট অনুমোদন করা যাবে না'
                : 'ভোট চলমান — এখন সমান ভোট আছে, চূড়ান্ত ফল নয়';
        } else {
            $('stWord3').textContent = 'আপত্তি';
            tag3.textContent = 'ক্লিয়ারড';
            tag3.classList.remove('is-warn');
            $('stSub3').textContent = 'রিটার্নিং অফিসার কর্তৃক পূর্ণাঙ্গ রেজাল্ট যাচাইয়ের জন্য প্রস্তুত';
        }
    }

    /* =========================================================
       Tie helpers (shudhu vote shesh hole tie pod alada dekhay)
       ========================================================= */
    function hideTie(p, d) {
        return d.phase === PHASE.Ended && !!p.isTie;
    }

    function remarkHtml(c) {
        if (!c) return '';
        var t = c.remarks || (c.isTie ? 'টাই' : '');
        if (!t) return '';
        return ' <span class="rs-remark' + (c.isTie ? ' is-tie' : '') + '">(' + esc(t) + ')</span>';
    }

    function renderTies(d) {
        if (!elTies) return;

        var list = d.phase === PHASE.Ended ? d.posts.filter(function (p) { return p.isTie; }) : [];
        if (!list.length) {
            elTies.hidden = true;
            elTies.innerHTML = '';
            return;
        }

        var rows = list.map(function (p) {
            var tied = p.candidates.filter(function (c) { return c.isTie; });
            return '<tr>' +
                '<td class="c-cand">' + postTitle(p) + '</td>' +
                '<td>' + tied.map(function (c) {
                    return '<span class="rs-tiechip">' + esc(c.name) +
                        ' <small>(ব্যালট ' + bn(pad2(c.ballotNo)) + ' · ' + fmt(c.votes) + ' ভোট)</small></span>';
                }).join('') + '</td>' +
                '<td>' + bn(p.seats) + 'টি</td>' +
                '<td><span class="rs-st rs-st-tie">টাই</span></td>' +
                '</tr>';
        }).join('');

        var href = cfg.tieUrl + (cfg.tieUrl.indexOf('?') < 0 ? '?' : '&') + 'electionId=' + encodeURIComponent(cfg.electionId);

        elTies.hidden = false;
        elTies.innerHTML =
            '<div class="rs-tie-head"><div>' +
            '<h3>টাই হওয়া পদসমূহ (' + bn(list.length) + 'টি)</h3>' +
            '<p>এই পদগুলোতে সমান ভোট হয়েছে। চূড়ান্ত ফলাফলের আগে টাই সমাধান করতে হবে।</p></div>' +
            '<a class="rs-btn rs-btn-gold" href="' + esc(href) + '">টাই সমাধান করুন</a></div>' +
            '<div class="rs-table-scroll"><table class="rs-table"><thead><tr>' +
            '<th>পদের নাম</th><th>টাই হওয়া প্রার্থী</th><th>আসন</th><th>অবস্থা</th>' +
            '</tr></thead><tbody>' + rows + '</tbody></table></div>';
    }

    /* =========================================================
       Post cards
       ========================================================= */
    function badgeHtml(p, phase) {
        var top = p.candidates[0];
        var multi = p.seats > 1;
        if (!top || p.validVotes === 0 || top.votes === 0) {
            return '<span class="rs-badge rs-badge-wait">অপেক্ষমাণ</span>';
        }
        if (p.isTie) {
            return '<span class="rs-badge rs-badge-tie">টাই</span>';
        }
        if (multi) {
            return '<span class="rs-badge rs-badge-win">শীর্ষ ' + bn(p.seats) + ' জন ' +
                (phase === PHASE.Ended ? 'নির্বাচিত' : 'এগিয়ে') + '</span>';
        }
        if (phase === PHASE.Ended) {
            return '<span class="rs-badge rs-badge-win">' + IC.trophy + 'বিজয়ী</span>';
        }
        return '<span class="rs-badge rs-badge-lead">' + IC.up + 'এগিয়ে</span>';
    }

    function otherRow(c) {
        return '<div class="rs-row" data-cid="' + c.candidateId + '">' +
            avatar(c, true) +
            '<div class="rs-row-info"><div class="rs-row-name">' + esc(c.name) + remarkHtml(c) + '</div>' +
            '<div class="rs-row-sub">ব্যালট ' + bn(pad2(c.ballotNo)) + '</div></div>' +
            '<div class="rs-row-v"><b data-num="' + c.votes + '" data-cid="' + c.candidateId + '">' + fmt(c.votes) + '</b>' +
            '<small>' + pct(c.percent) + '</small></div>' +
            '</div>';
    }

    function othersHtml(p, list, limit) {
        if (!list.length) return '';
        var open = !!state.expanded[p.postId];
        var shown = open ? list : list.slice(0, limit);
        var html = '<div class="rs-others">' + shown.map(otherRow).join('') + '</div>';
        if (list.length > limit) {
            html += '<button type="button" class="rs-more" data-more="' + p.postId + '">' +
                (open ? 'সংক্ষিপ্ত করুন' : 'আরও ' + bn(list.length - limit) + ' জন দেখুন') + '</button>';
        }
        return html;
    }

    function cardHtml(p, idx, phase) {
        var multi = p.seats > 1;
        var ended = phase === PHASE.Ended;
        var noVotes = p.validVotes === 0 || !p.candidates.length || p.candidates[0].votes === 0;
        var body = '';

        if (noVotes) {
            body += '<div class="rs-nodata">এখনো কোনো ভোট গণনা হয়নি</div>';
            body += othersHtml(p, p.candidates, 3);
        } else if (!multi) {
            var c = p.candidates[0];
            var gold = c.percent < 51;
            body +=
                '<div class="rs-leader" data-cid="' + c.candidateId + '">' +
                '  <div class="rs-leader-top">' + avatar(c, false) +
                '    <div class="rs-leader-info"><div class="rs-name-row"><span class="rs-name">' + esc(c.name) + '</span>' + remarkHtml(c) +
                '      <span class="rs-ballot">ব্যালট ' + bn(pad2(c.ballotNo)) + '</span></div>' +
                '      <div class="rs-desig">' + esc(c.designation || '') + '</div></div>' +
                '    <div class="rs-votes"><b data-num="' + c.votes + '" data-cid="' + c.candidateId + '">' + fmt(c.votes) + '</b>' +
                '      <small>' + pct(c.percent) + '</small></div>' +
                '  </div>' +
                '  <div class="rs-bar"><i class="' + (gold ? 'is-gold' : '') + '" data-pct="' + c.percent.toFixed(2) + '" data-cid="' + c.candidateId + '"></i></div>' +
                '  <div class="rs-leader-foot"><span>' +
                (p.isTie ? 'সমান ভোট — টাই' : '<b>+' + fmt(c.lead) + ' ভোট এগিয়ে</b>') +
                '  </span><span>প্রাপ্ত ভোট: ' + fmt(c.votes) + '</span></div>' +
                '</div>';
            body += othersHtml(p, p.candidates.slice(1), 3);
        } else {
            var winners = p.candidates.slice(0, p.seats);
            var rest = p.candidates.slice(p.seats);
            body += winners.map(function (c, i) {
                var tag = c.isTie ? '(টাই)' : (ended ? '(নির্বাচিত)' : '(এগিয়ে)');
                return '<div class="rs-winrow" data-cid="' + c.candidateId + '">' +
                    '<span class="rs-pos">' + bn(i + 1) + '</span>' +
                    '<div class="rs-row-info"><div class="rs-row-name">' + esc(c.name) + remarkHtml(c) + '</div>' +
                    '<div class="rs-row-sub">ব্যালট ' + bn(pad2(c.ballotNo)) + (c.designation ? ' | ' + esc(c.designation) : '') + '</div></div>' +
                    '<div class="rs-v"><b data-num="' + c.votes + '" data-cid="' + c.candidateId + '">' + fmt(c.votes) + '</b>' +
                    '<small>' + pct(c.percent) + ' ' + tag + '</small></div></div>';
            }).join('');
            body += othersHtml(p, rest, 1);
        }

        var foot = multi
            ? '<span>পদ প্রার্থী: ' + bn(p.candidates.length) + ' জন</span><span class="is-gold">' +
            (noVotes ? '' : (ended ? bn(p.seats) + ' জন কোটা পূর্ণ' : bn(p.seats) + 'টি আসনের গণনা চলছে')) + '</span>'
            : '<span>মোট বৈধ ভোট: ' + fmt(p.validVotes) + '</span><span>' +
            (noVotes ? '' : (ended ? 'ব্যালট সম্পন্ন' : 'গণনা চলছে')) + '</span>';

        return '<div class="rs-card-head"><div><div class="rs-post-no">পদবী ' + bn(pad2(idx + 1)) + '</div>' +
            '<h3>' + postTitle(p) + '</h3></div>' + badgeHtml(p, phase) + '</div>' +
            body + '<div class="rs-spacer"></div><div class="rs-card-foot">' + foot + '</div>';
    }

    function postSig(p, phase) {
        return phase + '|' + p.seats + '|' + p.validVotes + '|' + (state.expanded[p.postId] ? 1 : 0) + '|' +
            p.candidates.map(function (c) { return c.candidateId + ':' + c.votes; }).join(',');
    }

    // card er bhitorer number / bar / flash animate
    function animateCard(el, p, animate) {
        Array.prototype.forEach.call(el.querySelectorAll('img'), function (img) {
            img.addEventListener('error', function () { img.remove(); });
        });

        Array.prototype.forEach.call(el.querySelectorAll('[data-num]'), function (n) {
            var cid = n.getAttribute('data-cid');
            var to = +n.getAttribute('data-num');
            var from = animate ? (state.prevVotes[cid] || 0) : to;
            countUp(n, from, to, 950);

            if (animate && !state.first && to > from) {
                var host = n.closest('.rs-leader, .rs-winrow, .rs-row');
                if (host) {
                    host.classList.add('rs-flash');
                    if (host.classList.contains('rs-leader') || host.classList.contains('rs-winrow') || host.classList.contains('rs-row')) {
                        host.style.position = host.style.position || 'relative';
                        var f = document.createElement('span');
                        f.className = 'rs-float';
                        f.textContent = '+' + fmt(to - from);
                        host.appendChild(f);
                        setTimeout(function () { if (f.parentNode) f.parentNode.removeChild(f); }, 1700);
                    }
                    setTimeout(function () { host.classList.remove('rs-flash'); }, 1700);
                }
            }
        });

        Array.prototype.forEach.call(el.querySelectorAll('.rs-bar i'), function (b) {
            var cid = b.getAttribute('data-cid');
            var to = +b.getAttribute('data-pct');
            b.style.width = (animate ? (state.prevPct[cid] || 0) : to) + '%';
            void b.offsetWidth;
            requestAnimationFrame(function () { b.style.width = Math.min(100, to) + '%'; });
        });
    }

    function renderCards(d) {
        var seen = {};

        d.posts.forEach(function (p, idx) {
            if (hideTie(p, d)) return;     // tie pod alada section e dekhay
            seen[p.postId] = true;
            var el = state.cards[p.postId];
            var sig = postSig(p, d.phase);

            if (!el) {
                el = document.createElement('article');
                el.className = 'rs-card' + (p.seats > 1 ? ' is-multi' : '');
                el.style.setProperty('--i', idx);
                el.setAttribute('data-post', p.postId);
                state.cards[p.postId] = el;
                elGrid.appendChild(el);
            }

            if (el.getAttribute('data-sig') !== sig) {
                var onlyToggle = el.getAttribute('data-vsig') === voteSig(p) && el.getAttribute('data-phase') === String(d.phase);
                el.innerHTML = cardHtml(p, idx, d.phase);
                el.setAttribute('data-sig', sig);
                el.setAttribute('data-vsig', voteSig(p));
                el.setAttribute('data-phase', String(d.phase));
                animateCard(el, p, !onlyToggle);
            }

            // lead change
            var top = p.candidates[0];
            if (top && top.votes > 0 && !p.isTie) {
                var old = state.leaders[p.postId];
                if (old && old !== top.candidateId && !state.first && d.phase === PHASE.Running) {
                    toast(p.displayName + ' পদে নেতৃত্ব বদল: ' + top.name + ' এখন এগিয়ে');
                }
                state.leaders[p.postId] = top.candidateId;
            }
        });

        // je pod ar nai
        Object.keys(state.cards).forEach(function (k) {
            if (!seen[k]) {
                if (state.cards[k].parentNode) state.cards[k].parentNode.removeChild(state.cards[k]);
                delete state.cards[k];
            }
        });
    }

    function voteSig(p) {
        return p.candidates.map(function (c) { return c.candidateId + ':' + c.votes; }).join(',');
    }

    function rememberVotes(d) {
        d.posts.forEach(function (p) {
            p.candidates.forEach(function (c) {
                state.prevVotes[c.candidateId] = c.votes;
                state.prevPct[c.candidateId] = c.percent;
            });
        });
    }

    /* =========================================================
       Summary table
       ========================================================= */
    function buildSummaryShell() {
        elSummary.innerHTML =
            '<div class="rs-sum-head"><div>' +
            '<h3>' + IC.table + '<span id="sumTitle">ফলাফল সারাংশ ও প্রত্যয়ন তালিকা</span></h3>' +
            '<p id="sumSub"></p></div>' +
            '<span class="rs-sum-pill" id="sumPill"></span></div>' +
            '<div class="rs-table-scroll"><table class="rs-table"><thead><tr>' +
            '<th>পদের নাম</th><th>শীর্ষ প্রার্থী / বিজয়ী</th><th>ব্যালট</th><th>প্রাপ্ত ভোট</th>' +
            '<th>শতকরা হার</th><th>নিকটতম থেকে ব্যবধান</th><th>স্ট্যাটাস</th>' +
            '</tr></thead><tbody id="sumBody"></tbody></table></div>';
    }

    function statusChip(p, c, rank, phase) {
        var multi = p.seats > 1;
        if (!c || c.votes === 0) return '<span class="rs-st rs-st-wait">—</span>';
        if (c.isTie) return '<span class="rs-st rs-st-tie">টাই</span>';
        if (phase === PHASE.Ended) {
            return multi
                ? '<span class="rs-st rs-st-elect">নির্বাচিত (' + ord(rank) + ' স্থান)</span>'
                : '<span class="rs-st rs-st-win">' + IC.trophy + 'বিজয়ী</span>';
        }
        return '<span class="rs-st rs-st-lead">' + IC.up + 'এগিয়ে' + (multi ? ' (' + ord(rank) + ' স্থান)' : '') + '</span>';
    }

    function updateSummary(d) {
        var ended = d.phase === PHASE.Ended;
        $('sumTitle').textContent = 'ফলাফল সারাংশ ও প্রত্যয়ন তালিকা (পূর্ণাঙ্গ ' + bn(d.totalPosts) + 'টি পদ)';
        $('sumSub').textContent = ended
            ? 'নির্বাচন কমিশন সচিবালয় কর্তৃক নিরীক্ষিত ও আনুষ্ঠানিকভাবে প্রত্যয়নের জন্য প্রস্তুত'
            : 'ভোট গণনা চলমান — এটি সাময়িক ফলাফল, চূড়ান্ত নয়';
        var pill = $('sumPill');
        pill.className = 'rs-sum-pill' + (ended ? '' : ' is-live');
        pill.innerHTML = IC.check + (ended ? 'ডিজিটাল নিরীক্ষা সম্পন্ন' : 'লাইভ — সাময়িক ফলাফল');

        var rows = [], n = 0;
        d.posts.forEach(function (p, idx) {
            if (hideTie(p, d)) return;     // tie pod summary te nai
            var count = p.seats > 1 ? Math.min(p.seats, p.candidates.length) : 1;
            for (var r = 0; r < count; r++) {
                var c = p.candidates[r];
                var next = p.candidates[r + 1];
                var lead = c ? c.votes - (next ? next.votes : 0) : 0;
                var en = (p.displayNameEn || '').replace(/s$/i, '');
                var label;
                if (p.seats > 1) {
                    label = (r === 0 ? bn(idx + 1) + '. ' : '- ') + esc(p.displayName) +
                        ' (' + esc(en) + ' - ' + ord(r + 1) + ')';
                } else {
                    label = bn(idx + 1) + '. ' + postTitle(p);
                }
                rows.push(
                    '<tr style="--i:' + (n++) + '">' +
                    '<td>' + label + '</td>' +
                    '<td class="c-cand">' + (c ? esc(c.name) + remarkHtml(c) : '—') + '</td>' +
                    '<td>' + (c ? bn(pad2(c.ballotNo)) : '—') + '</td>' +
                    '<td class="c-num" data-sum-num="' + (c ? c.votes : 0) + '">' + (c ? fmt(c.votes) : '০') + '</td>' +
                    '<td>' + (c ? pct(c.percent) : '—') + '</td>' +
                    '<td class="c-lead">' + (c && c.votes > 0 ? '+' + fmt(lead) + ' ভোট' : '—') + '</td>' +
                    '<td>' + statusChip(p, c, r + 1, d.phase) + '</td>' +
                    '</tr>');
            }
        });
        $('sumBody').innerHTML = rows.join('');
    }

    /* =========================================================
       Render everything
       ========================================================= */
    var lastData = data;

    function render(d) {
        lastData = d;
        if (state.first) {
            buildStatsShell();
            buildSummaryShell();
        }

        updateHeader(d);
        updateStats(d);
        renderTies(d);
        renderCards(d);
        updateSummary(d);

        // new ballot chip
        if (!state.first && state.lastBallots != null && d.totalBallots > state.lastBallots) {
            var chip = $('newVotes');
            chip.textContent = '+' + fmt(d.totalBallots - state.lastBallots) + ' নতুন ব্যালট';
            chip.hidden = false;
            chip.classList.remove('show');
            void chip.offsetWidth;
            chip.classList.add('show');
            setTimeout(function () { chip.hidden = true; }, 4500);
        }

        if (!state.first && state.lastPhase === PHASE.Running && d.phase === PHASE.Ended) {
            toast('ভোটগ্রহণ শেষ হয়েছে — চূড়ান্ত ফলাফল প্রস্তুত');
        }

        rememberVotes(d);
        state.lastBallots = d.totalBallots;
        state.lastPhase = d.phase;
        state.version = d.version;

        if (state.first) {
            state.first = false;
            setTimeout(function () { page.classList.add('is-ready'); }, 1400);
        }
    }

    /* =========================================================
       Polling
       ========================================================= */
    function interval() {
        if (state.lastPhase === PHASE.Running) return POLL_RUNNING;
        if (state.lastPhase === PHASE.NotStarted) return POLL_WAITING;
        return 0;   // Ended: aar poll dorkar nai
    }

    function schedule() {
        clearTimeout(state.timer);
        var ms = interval();
        if (!ms || document.hidden) return;
        ms = ms * Math.min(1 + state.failures, 6);
        state.timer = setTimeout(function () { poll(false); }, ms);
    }

    function poll(manual) {
        if (state.busy) return;
        state.busy = true;

        var btn = $('btnRefresh');
        if (btn) btn.classList.add('is-busy');

        var url = cfg.dataUrl + (cfg.dataUrl.indexOf('?') < 0 ? '?' : '&') +
            'electionId=' + encodeURIComponent(cfg.electionId) + '&_=' + Date.now();

        fetch(url, {
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(function (r) {
                if (!r.ok) throw new Error('HTTP ' + r.status);
                return r.json();
            })
            .then(function (d) {
                if (!d || d.success === false || d.hasElection === false) throw new Error('bad data');
                state.failures = 0;

                if (d.version !== state.version) {
                    render(d);
                } else {
                    updateHeader(d);   // sudhu "সর্বশেষ আপডেট" somoy
                    state.lastPhase = d.phase;
                }
                if (manual) toast('ফলাফল হালনাগাদ হয়েছে');
            })
            .catch(function () {
                state.failures++;
                if (manual || state.failures === 3) toast('সংযোগ সমস্যা — আবার চেষ্টা করা হচ্ছে...', true);
            })
            .then(function () {
                state.busy = false;
                setTimeout(function () { if (btn) btn.classList.remove('is-busy'); }, 450);
                schedule();
            });
    }

    /* =========================================================
       Buttons
       ========================================================= */
    function ripple(e) {
        var b = e.currentTarget;
        var r = b.getBoundingClientRect();
        var s = Math.max(r.width, r.height);
        var el = document.createElement('span');
        el.className = 'rs-ripple';
        el.style.width = el.style.height = s + 'px';
        el.style.left = (e.clientX - r.left - s / 2) + 'px';
        el.style.top = (e.clientY - r.top - s / 2) + 'px';
        b.appendChild(el);
        setTimeout(function () { if (el.parentNode) el.parentNode.removeChild(el); }, 650);
    }

    Array.prototype.forEach.call(document.querySelectorAll('.rs-btn'), function (b) {
        b.addEventListener('click', ripple);
    });

    $('btnRefresh').addEventListener('click', function () { poll(true); });

    /* ---- print: alada page e, ekhane kokhono window.print() hoy na (freeze / khali page bondho) ---- */
    function openPrint() {
        var url = cfg.printUrl + (cfg.printUrl.indexOf('?') < 0 ? '?' : '&') +
            'electionId=' + encodeURIComponent(cfg.electionId);
        var w = window.open(url, '_blank');
        if (!w) toast('পপ-আপ ব্লক হয়েছে। এই সাইটের পপ-আপ অনুমতি দিন, অথবা "সামারি প্রিন্ট" বাটনে চাপুন।', true);
    }

    function csvDownload() {
        window.location.href = cfg.csvUrl + '?electionId=' + encodeURIComponent(cfg.electionId);
    }

    function postForm(url, data) {
        var tk = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
        var body = new URLSearchParams();
        body.append('__RequestVerificationToken', tk ? tk.value : '');
        Object.keys(data).forEach(function (k) { body.append(k, data[k]); });

        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: body.toString()
        }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        });
    }

    $('btnGazette').addEventListener('click', function () {
        var d = lastData;

        if (!window.Swal) {
            if (d.phase !== PHASE.Ended || d.issueCount > 0) { alert('এখনই অনুমোদন করা যাবে না।'); return; }
            if (d.gazetteApproved) { openPrint(); return; }
            if (!window.confirm('চূড়ান্ত ফলাফল অনুমোদন করবেন?')) return;
            doApprove();
            return;
        }

        if (d.phase !== PHASE.Ended) {
            Swal.fire({
                icon: 'info',
                title: 'এখনই অনুমোদন করা যাবে না',
                text: 'ভোটগ্রহণ শেষ হওয়ার পর চূড়ান্ত গেজেট অনুমোদন করা যাবে।',
                confirmButtonText: 'ঠিক আছে',
                confirmButtonColor: '#0c3d33',
                returnFocus: false
            });
            return;
        }

        if (d.issueCount > 0) {
            Swal.fire({
                icon: 'warning',
                title: 'টাই সমাধান বাকি',
                text: 'কিছু পদে সমান ভোট আছে। আগে টাই সমাধান করুন, তারপর গেজেট অনুমোদন করা যাবে।',
                showCancelButton: true,
                confirmButtonText: 'টাই সমাধান করুন',
                cancelButtonText: 'বন্ধ করুন',
                confirmButtonColor: '#0c3d33',
                returnFocus: false
            }).then(function (r) {
                if (r.isConfirmed) {
                    window.location.href = cfg.tieUrl + (cfg.tieUrl.indexOf('?') < 0 ? '?' : '&') +
                        'electionId=' + encodeURIComponent(cfg.electionId);
                }
            });
            return;
        }

        if (d.gazetteApproved) {
            openPrint();
            return;
        }

        Swal.fire({
            icon: 'question',
            title: 'চূড়ান্ত গেজেট অনুমোদন',
            text: 'চূড়ান্ত ফলাফল অনুমোদন করে প্রিন্ট পেজ খুলতে চান?',
            showCancelButton: true,
            confirmButtonText: 'হ্যাঁ, অনুমোদন করুন',
            cancelButtonText: 'বাতিল',
            confirmButtonColor: '#0c3d33',
            returnFocus: false
        }).then(function (r) {
            if (r.isConfirmed) doApprove();
        });
    });

    function doApprove() {
        postForm(cfg.approveUrl, { electionId: cfg.electionId })
            .then(function (res) {
                if (!res || !res.success) {
                    toast((res && res.message) || 'গেজেট অনুমোদন করা যায়নি।', true);
                    return;
                }
                lastData.gazetteApproved = true;
                updateHeader(lastData);
                toast((res.message || 'চূড়ান্ত গেজেট অনুমোদিত হয়েছে') + ' — এখন "সামারি প্রিন্ট" চাপুন');
                openPrint();
            })
            .catch(function () {
                toast('সংযোগ সমস্যা — আবার চেষ্টা করুন', true);
            });
    }

    // "আরও N জন দেখুন"
    elGrid.addEventListener('click', function (e) {
        var b = e.target.closest ? e.target.closest('[data-more]') : null;
        if (!b) return;
        var id = b.getAttribute('data-more');
        state.expanded[id] = !state.expanded[id];
        renderCards(lastData);
    });

    document.addEventListener('visibilitychange', function () {
        if (document.hidden) clearTimeout(state.timer);
        else if (interval()) poll(false);
    });

    window.addEventListener('pageshow', function (e) {
        if (!e.persisted) return;
        if (window.Swal) { try { Swal.close(); } catch (x) { } }
        document.body.classList.remove('swal2-shown', 'swal2-height-auto', 'swal2-no-backdrop');
        document.body.style.overflow = '';
        document.body.style.paddingRight = '';
        var sc = document.querySelector('.swal2-container');
        if (sc && sc.parentNode) sc.parentNode.removeChild(sc);
        if (lastData && lastData.phase === PHASE.Running) poll(false);
    });

    /* =========================================================
       Start
       ========================================================= */
    render(data);
    schedule();
})();
