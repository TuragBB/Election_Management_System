/* ================================================================
   Admin > সকল মনোনয়ন আবেদন
   - infinite scroll
   - search / sort / filter
   - Bengali token based search
   - safe request reset
   ================================================================ */

(function () {
    'use strict';

    var cfg = window.ADMIN_CFG;
    var PAGE_SIZE = 10;

    var state = {
        skip: 0,
        sort: 'date',
        dir: 'desc',
        loading: false,
        done: false,
        total: 0,
        req: 0
    };

    var body = document.getElementById('nomBody');
    var stateBox = document.getElementById('listState');
    var sentinel = document.getElementById('sentinel');
    var badge = document.getElementById('totalBadge');
    var chipRow = document.getElementById('chipRow');

    var fSearch = document.getElementById('fSearch');
    var fPost = document.getElementById('fPost');
    var fStatus = document.getElementById('fStatus');
    var fDate = document.getElementById('fDate');

    if (!body || !stateBox || !sentinel || !badge ||
        !fSearch || !fPost || !fStatus || !fDate) {
        console.error('Admin nominations: required HTML elements are missing.');
        return;
    }

    /* =========================================================
       Helpers
       ========================================================= */

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#39;'
            }[c];
        });
    }

    function bn(v) {
        return String(v == null ? '' : v).replace(/[0-9]/g, function (d) {
            return '০১২৩৪৫৬৭৮৯'.charAt(+d);
        });
    }

    function toQuery(o) {
        var parts = [];

        Object.keys(o).forEach(function (k) {
            if (o[k] !== '' && o[k] != null) {
                parts.push(
                    encodeURIComponent(k) + '=' +
                    encodeURIComponent(o[k])
                );
            }
        });

        return parts.join('&');
    }

    function filters() {
        return {
            q: fSearch.value.trim(),
            post: fPost.value,
            status: fStatus.value,
            date: fDate.value,
            sort: state.sort,
            dir: state.dir
        };
    }

    /* =========================================================
       Icons
       ========================================================= */

    var ICONS = {
        star: '<svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>',

        pen: '<svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z"/></svg>',

        coin: '<svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 1v22"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>',

        user: '<svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>',

        users: '<svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.9"/><path d="M16 3.1a4 4 0 0 1 0 7.8"/></svg>'
    };

    var POSTS = {
        'সভাপতি': { cls: 'pc-gold', icon: ICONS.star },
        'সহ-সভাপতি': { cls: 'pc-gold', icon: ICONS.star },
        'সাধারণ সম্পাদক': { cls: 'pc-slate', icon: ICONS.pen },
        'যুগ্ম সম্পাদক': { cls: 'pc-orange', icon: ICONS.pen },
        'কোষাধ্যক্ষ': { cls: 'pc-purple', icon: ICONS.coin },
        'সহকারী কোষাধ্যক্ষ': { cls: 'pc-teal', icon: ICONS.coin },
        'সম্মানিত সদস্য': { cls: 'pc-blue', icon: ICONS.users }
    };

    var STATUS_CLS = {
        Pending: 'st-pending',
        Approved: 'st-approved',
        Rejected: 'st-rejected'
    };

    var CLOCK =
        '<svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">' +
        '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>' +
        '</svg>';

    var EYE =
        '<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round">' +
        '<path d="M1 12s4-7 11-7 11 7 11 7-4 7-11 7S1 12 1 12z"/>' +
        '<circle cx="12" cy="12" r="3"/>' +
        '</svg>';

    var PRINT =
        '<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round">' +
        '<path d="M6 9V3h12v6"/>' +
        '<rect x="3" y="9" width="18" height="9" rx="2"/>' +
        '<path d="M6 14h12v7H6z"/>' +
        '</svg>';

    /* =========================================================
       Rendering
       ========================================================= */

    function rowHtml(x, n) {

        var pm = POSTS[x.post] || {
            cls: 'pc-blue',
            icon: ICONS.user
        };

        return '' +
            '<tr>' +

            '<td class="col-serial">' +
            bn(n < 10 ? '0' + n : n) +
            '</td>' +

            '<td class="col-date">' +
            '<div class="appno">' +
            esc(x.applicationNo) +
            '</div>' +

            '<div class="appdate">' +
            CLOCK +
            '<span>' + esc(x.dateText) + '</span>' +
            '</div>' +

            '</td>' +

            '<td class="col-cand">' +

            '<div class="cand-name">' +
            esc(x.name) +
            '</div>' +

            '<span class="chip-member">' +
            'সদস্য: ' + esc(x.memberNo) +
            '</span>' +

            '<div class="cand-org">' +
            esc(x.designation) +
            ' · আইডি: ' +
            esc(x.organizationId) +
            '</div>' +

            '</td>' +

            '<td class="col-contact">' +
            (x.mobile ? esc(x.mobile) : '—') +
            '</td>' +

            '<td class="col-post">' +
            '<span class="post-chip ' + pm.cls + '">' +
            pm.icon +
            '<span>' + esc(x.post) + '</span>' +
            '</span>' +
            '</td>' +

            '<td class="col-status">' +
            '<span class="status ' +
            (STATUS_CLS[x.status] || 'st-pending') +
            '">' +
            '<i></i>' +
            esc(x.statusText) +
            '</span>' +
            '</td>' +

            '<td class="col-action">' +

            '<button type="button" class="icon-btn" ' +
            'data-act="view" data-id="' +
            esc(x.id) +
            '" title="দেখুন" aria-label="দেখুন">' +
            EYE +
            '</button>' +

            '<button type="button" class="icon-btn" ' +
            'data-act="print" data-id="' +
            esc(x.id) +
            '" title="প্রিন্ট" aria-label="প্রিন্ট">' +
            PRINT +
            '</button>' +

            '</td>' +

            '</tr>';
    }

    function setState(mode, msg) {

        if (mode === 'loading') {

            stateBox.innerHTML =
                '<span class="spinner"></span> লোড হচ্ছে...';

        } else if (mode === 'end') {

            stateBox.innerHTML =
                state.total > 0
                    ? 'সব আবেদন দেখানো হয়েছে'
                    : '';

        } else if (mode === 'empty') {

            stateBox.innerHTML =
                '<div class="empty-box">' +
                'কোনো আবেদন পাওয়া যায়নি।' +
                '</div>';

        } else if (mode === 'error') {

            stateBox.innerHTML =
                '<div class="error-box">' +
                esc(msg || 'লোড করা যায়নি।') +
                ' <button type="button" class="btn-link" id="btnRetry">' +
                'আবার চেষ্টা করুন' +
                '</button>' +
                '</div>';

            var r = document.getElementById('btnRetry');

            if (r) {
                r.addEventListener('click', function () {
                    load(true);
                });
            }

        } else {

            stateBox.innerHTML = '';
        }
    }

    function renderChips() {

        /* IMPORTANT:
           Old project did not have chipRow.
           Never let missing chipRow kill the entire JS.
        */

        if (!chipRow) {
            return;
        }

        var chips = [];

        chips.push(
            '<span class="chip">নির্বাচনী বর্ষ: ২০২৭</span>'
        );

        chips.push(
            '<span class="chip">স্ট্যাটাস: ' +
            esc(
                fStatus.value
                    ? fStatus.options[fStatus.selectedIndex].text
                    : 'সকল'
            ) +
            '</span>'
        );

        chips.push(
            '<span class="chip">পদ: ' +
            esc(fPost.value ? fPost.value : 'সকল') +
            '</span>'
        );

        if (fDate.value) {
            chips.push(
                '<span class="chip">তারিখ: ' +
                esc(bn(fDate.value)) +
                '</span>'
            );
        }

        if (fSearch.value.trim()) {
            chips.push(
                '<span class="chip">সার্চ: ' +
                esc(fSearch.value.trim()) +
                '</span>'
            );
        }

        chipRow.innerHTML =
            '<span class="chip-label">সক্রিয় ফিল্টার:</span>' +
            chips.join('');
    }

    function updateSortHeaders() {

        var ths =
            document.querySelectorAll('th.sortable');

        Array.prototype.forEach.call(ths, function (th) {

            th.classList.remove(
                'sorted-asc',
                'sorted-desc'
            );

            if (
                th.getAttribute('data-sort') ===
                state.sort
            ) {
                th.classList.add(
                    state.dir === 'asc'
                        ? 'sorted-asc'
                        : 'sorted-desc'
                );
            }
        });
    }

    /* =========================================================
       Main loader
       ========================================================= */

    function load(reset) {

        if (reset) {

            state.skip = 0;
            state.done = false;
            state.loading = false;
            state.total = 0;

            /* Every new search/filter gets a new request id. */
            state.req++;

            body.innerHTML = '';

            renderChips();
            updateSortHeaders();
        }

        if (state.loading || state.done) {
            return;
        }

        state.loading = true;

        setState('loading');

        var myReq = state.req;

        var params = filters();

        params.skip = state.skip;
        params.take = PAGE_SIZE;

        /*
         * Cache-buster.
         * This prevents browser/proxy from returning an old GET response.
         */
        params._ = new Date().getTime();

        fetch(
            cfg.listUrl + '?' + toQuery(params),
            {
                method: 'GET',
                credentials: 'same-origin',
                cache: 'no-store',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Cache-Control': 'no-cache'
                }
            }
        )
            .then(function (res) {

                if (!res.ok) {
                    throw new Error(
                        'HTTP ' + res.status
                    );
                }

                return res.json();
            })

            .then(function (data) {

                /*
                 * If user typed another search while
                 * this request was running, ignore this
                 * old response.
                 */
                if (myReq !== state.req) {
                    return;
                }

                if (!data.success) {
                    throw new Error(
                        data.message || 'error'
                    );
                }

                var html = '';

                for (
                    var i = 0;
                    i < data.items.length;
                    i++
                ) {
                    html += rowHtml(
                        data.items[i],
                        state.skip + i + 1
                    );
                }

                body.insertAdjacentHTML(
                    'beforeend',
                    html
                );

                state.skip += data.items.length;
                state.total = data.total;

                state.loading = false;

                state.done =
                    data.items.length === 0 ||
                    state.skip >= data.total;

                badge.textContent =
                    'মোট আবেদন: ' +
                    bn(data.total) +
                    ' টি';

                if (state.total === 0) {

                    setState('empty');

                } else if (state.done) {

                    setState('end');

                } else {

                    setState('');
                }

                window.requestAnimationFrame(
                    checkFill
                );
            })

            .catch(function (err) {

                if (myReq !== state.req) {
                    return;
                }

                console.error(
                    'Nomination list error:',
                    err
                );

                state.loading = false;

                setState(
                    'error',
                    'তালিকা লোড করা যায়নি।'
                );
            });
    }

    /* =========================================================
       Infinite scroll
       ========================================================= */

    function checkFill() {

        if (
            state.done ||
            state.loading
        ) {
            return;
        }

        var top =
            sentinel.getBoundingClientRect().top;

        if (
            top <
            window.innerHeight + 200
        ) {
            load(false);
        }
    }

    if ('IntersectionObserver' in window) {

        new IntersectionObserver(
            function (entries) {

                if (
                    entries[0].isIntersecting
                ) {
                    load(false);
                }

            },
            {
                rootMargin: '250px'
            }
        ).observe(sentinel);

    } else {

        window.addEventListener(
            'scroll',
            checkFill
        );
    }

    /* =========================================================
       Search
       ========================================================= */

    var timer = null;

    fSearch.addEventListener(
        'input',
        function () {

            clearTimeout(timer);

            /*
             * IMPORTANT:
             * If user completely clears the search,
             * reload immediately.
             */
            if (
                fSearch.value.trim() === ''
            ) {
                load(true);
                return;
            }

            timer = setTimeout(
                function () {
                    load(true);
                },
                300
            );
        }
    );

    /* =========================================================
       Other filters
       ========================================================= */

    [
        fPost,
        fStatus,
        fDate
    ].forEach(function (el) {

        el.addEventListener(
            'change',
            function () {
                load(true);
            }
        );

    });

    /* =========================================================
       Reset
       ========================================================= */

    document
        .getElementById('btnReset')
        .addEventListener(
            'click',
            function () {

                fSearch.value = '';
                fPost.value = '';
                fStatus.value = '';
                fDate.value = '';

                state.sort = 'date';
                state.dir = 'desc';

                load(true);
            }
        );

    /* =========================================================
       Sorting
       ========================================================= */

    Array.prototype.forEach.call(
        document.querySelectorAll('th.sortable'),
        function (th) {

            th.addEventListener(
                'click',
                function () {

                    var key =
                        th.getAttribute(
                            'data-sort'
                        );

                    if (
                        state.sort === key
                    ) {

                        state.dir =
                            state.dir === 'asc'
                                ? 'desc'
                                : 'asc';

                    } else {

                        state.sort = key;

                        state.dir =
                            (
                                key === 'date' ||
                                key === 'serial'
                            )
                                ? 'desc'
                                : 'asc';
                    }

                    load(true);
                }
            );
        }
    );

    /* =========================================================
       Table actions
       ========================================================= */

    body.addEventListener(
        'click',
        function (e) {

            var btn =
                e.target.closest
                    ? e.target.closest('[data-act]')
                    : null;

            if (!btn) {
                return;
            }

            var id =
                btn.getAttribute('data-id');

            var action =
                btn.getAttribute('data-act');

            if (action === 'view') {

                window.location.href =
                    cfg.detailsUrl +
                    '/' +
                    encodeURIComponent(id);

            } else if (action === 'print') {

                window.open(
                    cfg.printUrl +
                    '/' +
                    encodeURIComponent(id),
                    'nomPrint',
                    'width=1100,height=850,scrollbars=yes,resizable=yes'
                );
            }
        }
    );

    /* =========================================================
       Print summary
       ========================================================= */

    document
        .getElementById('btnSummaryPrint')
        .addEventListener(
            'click',
            function () {

                window.open(
                    cfg.summaryUrl +
                    '?' +
                    toQuery(filters()),
                    'nomSummary',
                    'width=1200,height=850,scrollbars=yes,resizable=yes'
                );
            }
        );

    /* =========================================================
       Export
       ========================================================= */

    document
        .getElementById('btnExport')
        .addEventListener(
            'click',
            function () {

                window.location.href =
                    cfg.exportUrl +
                    '?' +
                    toQuery(filters());
            }
        );

    /* =========================================================
       Start
       ========================================================= */

    load(true);

})();