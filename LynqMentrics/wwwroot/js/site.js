// LynqMentrics client-side behavior: GDPR/CCPA cookie consent management.
(function () {
    "use strict";

    const CONSENT_COOKIE = "lynq_consent";
    const CONSENT_VERSION = "1.0";

    function getConsentCookie() {
        const match = document.cookie.match(new RegExp("(?:^|; )" + CONSENT_COOKIE + "=([^;]*)"));
        return match ? decodeURIComponent(match[1]) : null;
    }

    function setConsentCookie(analyticsAllowed) {
        const value = JSON.stringify({
            necessary: true,
            analytics: analyticsAllowed,
            version: CONSENT_VERSION
        });
        // SameSite=Lax keeps the cookie available on normal navigation; 365-day
        // expiry matches the stated retention in the Cookie Policy.
        document.cookie = CONSENT_COOKIE + "=" + encodeURIComponent(value) +
            ";max-age=" + (60 * 60 * 24 * 365) + ";path=/;SameSite=Lax";
    }

    function hideBanner() {
        const banner = document.getElementById("cookie-consent-banner");
        if (banner) {
            banner.remove();
        }
    }

    async function recordConsent(consentType, granted) {
        try {
            await fetch("/api/privacy/consent", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ consentType, granted, consentVersion: CONSENT_VERSION })
            });
        } catch (_) {
            // The audit trail write is best-effort; the consent cookie is the
            // primary mechanism and remains valid even if the API call fails.
        }
    }

    async function handleDecision(analyticsAllowed) {
        setConsentCookie(analyticsAllowed);
        hideBanner();
        await recordConsent(analyticsAllowed ? "analytics" : "necessary", analyticsAllowed);
        // Reload so the server renders consent-gated resources (e.g. Google Fonts)
        // according to the new decision.
        window.location.reload();
    }

    function wireBanner() {
        const banner = document.getElementById("cookie-consent-banner");
        if (!banner) {
            return;
        }

        const accept = banner.querySelector('[data-consent="accept"]');
        const decline = banner.querySelector('[data-consent="decline"]');

        if (accept) {
            accept.addEventListener("click", () => handleDecision(true));
        }
        if (decline) {
            decline.addEventListener("click", () => handleDecision(false));
        }
    }

    function wireCookieSettings() {
        const link = document.getElementById("cookie-settings-link");
        if (!link) {
            return;
        }

        link.addEventListener("click", () => {
            // Show the banner again so the visitor can change their decision.
            if (!document.getElementById("cookie-consent-banner")) {
                const banner = document.createElement("div");
                banner.id = "cookie-consent-banner";
                banner.setAttribute("role", "dialog");
                banner.setAttribute("aria-label", "Cookie consent");
                banner.className = "fixed inset-x-0 bottom-0 z-50 border-t border-slate-700 bg-slate-900/95 p-5 shadow-2xl backdrop-blur";
                banner.innerHTML =
                    '<div class="mx-auto flex max-w-6xl flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">' +
                    '<div class="max-w-3xl"><p class="text-sm text-slate-200">' +
                    "You can change your cookie preferences at any time." +
                    ' <a href="/Privacy/Policy" class="text-brandCyan underline">Privacy Policy</a> · ' +
                    ' <a href="/Privacy/Cookies" class="text-brandCyan underline">Cookie Policy</a></p></div>' +
                    '<div class="flex flex-shrink-0 flex-wrap gap-3">' +
                    '<button type="button" data-consent="accept" class="inline-flex items-center rounded-lg bg-brandCyan px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-cyan-300">Accept all</button>' +
                    '<button type="button" data-consent="decline" class="inline-flex items-center rounded-lg border border-slate-600 px-4 py-2 text-sm font-semibold text-slate-100 hover:border-brandCyan hover:text-white">Essential only</button>' +
                    "</div></div>";
                document.body.appendChild(banner);
                wireBanner();
            }
        });
    }

    document.addEventListener("DOMContentLoaded", () => {
        wireBanner();
        wireCookieSettings();
    });
})();
