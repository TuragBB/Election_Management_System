/* ================================================================
   Admin > আবেদনপত্র বিস্তারিত
   Approve / Reject with SweetAlert2 confirmation
   ================================================================ */
(function () {
    'use strict';

    var wrap = document.getElementById('detailWrap');
    if (!wrap) return;

    var updateUrl = wrap.getAttribute('data-update-url');
    var id = wrap.getAttribute('data-id');
    var badge = document.getElementById('statusBadge');
    var badgeText = document.getElementById('statusBadgeText');
    var buttons = document.querySelectorAll('[data-decision]');
    var printButton = document.getElementById('btnDetailPrint');
    var printUrl = wrap.getAttribute('data-print-url');
    var CLASSES = ['st-pending', 'st-approved', 'st-rejected'];

    if (printButton && printUrl) {
        printButton.addEventListener('click', function () {
            window.open(
                printUrl + '/' + encodeURIComponent(id),
                'nomPrint',
                'width=1100,height=850,scrollbars=yes,resizable=yes'
            );
        });
    }

    function token() {
        var el = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    // The back control intentionally uses the real Nominations URL instead of
    // history.back(). This forces a fresh list request, so a status changed here
    // is immediately reflected in the admin list.

    function applyStatus(status, cssClass, detailText) {
        CLASSES.forEach(function (c) { badge.classList.remove(c); });
        badge.classList.add(cssClass);
        badge.setAttribute('data-status', status);
        badgeText.textContent = detailText;

        Array.prototype.forEach.call(buttons, function (b) {
            b.disabled = (b.getAttribute('data-decision') === status);
        });
    }

    // disable the button that matches the current status on page load
    applyStatus(badge.getAttribute('data-status'),
        CLASSES.filter(function (c) { return badge.classList.contains(c); })[0] || 'st-pending',
        badgeText.textContent);

    function decide(decision) {
        var approve = decision === 'Approved';

        Swal.fire({
            title: 'আপনি কি নিশ্চিত?',
            text: approve
                ? 'আপনি কি এই প্রার্থীর আবেদনটি অনুমোদন করতে চান?'
                : 'আপনি কি এই প্রার্থীর আবেদনটি বাতিল করতে চান?',
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'হ্যাঁ',
            cancelButtonText: 'না',
            reverseButtons: true,
            focusCancel: true,
            confirmButtonColor: approve ? '#0c4a3f' : '#c62828',
            cancelButtonColor: '#6b7a77',
            showLoaderOnConfirm: true,
            allowOutsideClick: function () { return !Swal.isLoading(); },
            preConfirm: function () {
                var body = 'id=' + encodeURIComponent(id) +
                    '&status=' + encodeURIComponent(decision) +
                    '&__RequestVerificationToken=' + encodeURIComponent(token());

                return fetch(updateUrl, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: body
                })
                    .then(function (res) { return res.json(); })
                    .then(function (data) {
                        if (!data.success) throw new Error(data.message || 'স্ট্যাটাস আপডেট করা যায়নি।');
                        return data;
                    })
                    .catch(function (err) {
                        Swal.showValidationMessage(err && err.message ? err.message : 'স্ট্যাটাস আপডেট করা যায়নি।');
                    });
            }
        }).then(function (result) {
            if (result.isConfirmed && result.value) {
                var d = result.value;
                applyStatus(d.status, d.cssClass, d.detailText);

                Swal.fire({
                    icon: 'success',
                    title: approve ? 'আবেদনটি অনুমোদন করা হয়েছে' : 'আবেদনটি বাতিল করা হয়েছে',
                    timer: 1800,
                    showConfirmButton: false
                });
            }
        });
    }

    Array.prototype.forEach.call(buttons, function (b) {
        b.addEventListener('click', function () {
            if (!b.disabled) decide(b.getAttribute('data-decision'));
        });
    });
})();
