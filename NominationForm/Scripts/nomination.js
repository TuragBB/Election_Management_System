/* ================================================================
   Nomination form interactions
   - NikoshBAN UI is loaded by nomination.css
   - SweetAlert2 handles validation/submit confirmation
   - Native date input + premium trigger button
   ================================================================ */

(function () {
    "use strict";

    // Custom rule for [MustBeTrue].
    if ($.validator) {
        $.validator.addMethod("mustbetrue", function (value, element) {
            return !!element.checked;
        });

        if ($.validator.unobtrusive && $.validator.unobtrusive.adapters) {
            $.validator.unobtrusive.adapters.addBool("mustbetrue");
        }
    }

    function getFirstErrorElement($form) {
        var $input = $form.find(".input-validation-error").first();
        if ($input.length) return $input;

        var $message = $form.find(".field-validation-error").filter(function () {
            return $.trim($(this).text()).length > 0;
        }).first();

        if ($message.length) {
            var $near = $message.closest(".field, .question, .person-card").find("input, textarea, select").first();
            return $near.length ? $near : $message;
        }

        return $();
    }

    function showValidationPopup($form, title) {
        var messages = [];

        $form.find(".field-validation-error").each(function () {
            var text = $.trim($(this).text());
            if (text && messages.indexOf(text) === -1) {
                messages.push(text);
            }
        });

        $form.find(".validation-summary-errors li").each(function () {
            var text = $.trim($(this).text());
            if (text && messages.indexOf(text) === -1) {
                messages.push(text);
            }
        });

        var html = messages.length
            ? "<div style='text-align:left;line-height:1.75'>" +
            messages.slice(0, 8).map(function (m) {
                return "<div>• " + $("<div>").text(m).html() + "</div>";
            }).join("") +
            (messages.length > 8 ? "<div>• আরও কিছু তথ্য সংশোধন করতে হবে।</div>" : "") +
            "</div>"
            : "লাল চিহ্নিত ঘরগুলো ঠিক করে আবার চেষ্টা করুন।";

        Swal.fire({
            title: title || "ফর্মে ভুল আছে",
            html: html,
            icon: "error",
            confirmButtonText: "ঠিক আছে",
            confirmButtonColor: "#0c5b4e",
            width: 470
        }).then(function () {
            var $first = getFirstErrorElement($form);
            if ($first.length) {
                $("html, body").animate({
                    scrollTop: Math.max(0, $first.offset().top - 120)
                }, 350);
                $first.trigger("focus");
            }
        });
    }

    function openNativeDatePicker(target) {
        var input = typeof target === "string" ? document.getElementById(target.replace(/^#/, "")) : target;
        if (!input || input.disabled || input.readOnly) return;

        // Chrome/Edge: showPicker() must be called directly from the user's click.
        try {
            if (typeof input.showPicker === "function") {
                input.focus({ preventScroll: true });
                input.showPicker();
                return;
            }
        } catch (e) {
            // Fall through to the native click/focus fallback.
        }

        try {
            input.focus({ preventScroll: true });
            input.click();
        } catch (e2) {
            input.focus();
        }
    }

    $(function () {
        var $form = $("#nominationForm");
        if (!$form.length) return;

        /* ---------- Selected post preview ---------- */
        $("input[name='DesiredPost']").on("change", function () {
            $("#SelectedPostText").val($(this).val());
        });

        var selectedPost = $("input[name='DesiredPost']:checked").val();
        if (selectedPost) {
            $("#SelectedPostText").val(selectedPost);
        }

        /* ---------- Premium date buttons ---------- */
        $(document).on("click", ".date-picker-trigger", function (e) {
            e.preventDefault();
            e.stopPropagation();
            openNativeDatePicker($(this).attr("data-target"));
        });

        // Clicking the calendar area/field also opens the native picker.
        $(document).on("click", ".date-field", function (e) {
            if ($(e.target).closest(".date-picker-trigger").length) return;
            if ($(e.target).is("input")) return;
            openNativeDatePicker($(this).find(".premium-date").attr("id"));
        });

        /* ---------- Trim user text before validation ---------- */
        $form.find("input[type='text'], input[type='email'], textarea").on("blur", function () {
            $(this).val($.trim($(this).val()));
        });

        /* ---------- Server-side validation returned with the form ---------- */
        var hasServerErrors =
            $form.find(".input-validation-error").length > 0 ||
            $form.find(".field-validation-error").filter(function () {
                return $.trim($(this).text()).length > 0;
            }).length > 0 ||
            $form.find(".validation-summary-errors li").length > 0;

        if (hasServerErrors) {
            setTimeout(function () {
                showValidationPopup($form, "তথ্যগুলো ঠিক করুন");
            }, 180);
        }

        /* ---------- Submit validation + confirmation ---------- */
        var submitting = false;

        $form.on("submit", function (e) {
            if (submitting) return;

            e.preventDefault();

            // Trim all text values once more before running validation.
            $form.find("input[type='text'], input[type='email'], textarea").each(function () {
                $(this).val($.trim($(this).val()));
            });

            if (!$form.valid()) {
                showValidationPopup($form, "ফর্মে ভুল আছে");
                return;
            }

            Swal.fire({
                title: "আবেদন জমা দেবেন?",
                text: "আপনার দেওয়া তথ্যগুলো যাচাই করে নিশ্চিত করুন।",
                icon: "question",
                showCancelButton: true,
                confirmButtonText: "হ্যাঁ, জমা দিন",
                cancelButtonText: "ফিরে গিয়ে যাচাই করি",
                confirmButtonColor: "#0c5b4e",
                cancelButtonColor: "#71807c",
                reverseButtons: true,
                focusCancel: true
            }).then(function (result) {
                if (!result.isConfirmed) return;

                submitting = true;
                $("#submitNomination").prop("disabled", true).text("জমা হচ্ছে...");

                // Native submit bypasses this handler but keeps the MVC antiforgery token.
                $form[0].submit();
            });
        });
    });
}());
