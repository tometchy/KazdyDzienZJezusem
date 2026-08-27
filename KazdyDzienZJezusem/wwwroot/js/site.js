// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    const feedbackTimers = new WeakMap();

    function normalizeLegacyVerseHash() {
        const legacyHash = /^#w(\d+)$/.exec(window.location.hash);
        if (!legacyHash) {
            return;
        }

        const verse = document.getElementById(legacyHash[1]);
        if (!verse) {
            return;
        }

        const normalizedHash = `#${legacyHash[1]}`;
        window.history.replaceState(
            window.history.state,
            "",
            `${window.location.pathname}${window.location.search}${normalizedHash}`);
        verse.scrollIntoView();
    }

    function syncCurrentVerseLink() {
        document.querySelectorAll("[data-verse-link]").forEach(link => {
            const isCurrent = link.hash === window.location.hash;
            if (isCurrent) {
                link.setAttribute("aria-current", "location");
            } else {
                link.removeAttribute("aria-current");
            }
            link.closest(".bible-verse")?.classList.toggle("is-targeted", isCurrent);
        });
    }

    async function copyText(text) {
        if (navigator.clipboard && window.isSecureContext) {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch {
                // Fall through to the compatibility path below.
            }
        }

        const textArea = document.createElement("textarea");
        textArea.value = text;
        textArea.readOnly = true;
        textArea.style.position = "fixed";
        textArea.style.inset = "0 auto auto -9999px";
        document.body.appendChild(textArea);
        textArea.select();
        textArea.setSelectionRange(0, textArea.value.length);

        let copied = false;
        try {
            copied = document.execCommand("copy");
        } catch {
            copied = false;
        }

        textArea.remove();
        return copied;
    }

    function showCopyFeedback(link, copied) {
        const status = link.closest(".verse-list")?.querySelector("[data-verse-link-status]");
        const verseReference = link.dataset.verseReference;
        const stateClass = copied ? "is-copied" : "is-copy-error";

        link.classList.remove("is-copied", "is-copy-error");
        link.classList.add(stateClass);
        if (status) {
            status.textContent = copied
                ? `Skopiowano adres wersetu ${verseReference}.`
                : `Nie udało się skopiować adresu wersetu ${verseReference}. Adres jest widoczny w pasku przeglądarki.`;
        }

        const previousTimer = feedbackTimers.get(link);
        if (previousTimer) {
            window.clearTimeout(previousTimer);
        }

        const timer = window.setTimeout(() => {
            link.classList.remove(stateClass);
            feedbackTimers.delete(link);
        }, 2400);
        feedbackTimers.set(link, timer);
    }

    document.addEventListener("click", event => {
        const clickedElement = event.target instanceof Element ? event.target : null;
        const link = clickedElement?.closest("[data-verse-link]");
        if (!(link instanceof HTMLAnchorElement)) {
            return;
        }

        void copyText(link.href).then(copied => {
            showCopyFeedback(link, copied);
            link.focus({ preventScroll: true });
        });
    }, { capture: true });

    window.addEventListener("hashchange", () => {
        normalizeLegacyVerseHash();
        syncCurrentVerseLink();
    });

    document.addEventListener("up:location:changed", () => {
        normalizeLegacyVerseHash();
        syncCurrentVerseLink();
    });

    document.addEventListener("up:fragment:inserted", () => {
        normalizeLegacyVerseHash();
        syncCurrentVerseLink();
    });

    normalizeLegacyVerseHash();
    syncCurrentVerseLink();
})();
