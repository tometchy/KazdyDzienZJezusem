// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    const initialized = new WeakSet();

    function initializeTagReaders() {
        document.querySelectorAll('[data-tag-reader]').forEach(reader => {
            const select = reader.querySelector('[data-tag-translation-select]');
            if (!select) return;
            const storageKey = `kdj:tag-translation:${reader.dataset.tagSlug}`;
            const panels = reader.querySelectorAll('[data-tag-translation]');

            function showTranslation(code) {
                const available = Array.from(select.options).some(option => option.value === code);
                select.value = available ? code : reader.dataset.defaultTranslation;
                panels.forEach(panel => { panel.hidden = panel.dataset.tagTranslation !== select.value; });
            }

            let savedTranslation;
            try { savedTranslation = localStorage.getItem(storageKey); }
            catch { /* Reading remains available when browser storage is blocked. */ }
            showTranslation(savedTranslation);
            reader.querySelector('[data-tag-translation-picker]').hidden = false;

            if (initialized.has(reader)) return;
            initialized.add(reader);
            select.addEventListener('change', () => {
                showTranslation(select.value);
                try { localStorage.setItem(storageKey, select.value); }
                catch { /* Switching translations does not require browser storage. */ }
            });
        });
    }

    document.addEventListener('DOMContentLoaded', initializeTagReaders);
    document.addEventListener('up:fragment:inserted', initializeTagReaders);
    window.addEventListener('pageshow', event => { if (event.persisted) initializeTagReaders(); });
})();

(() => {
    let csrfToken;
    let generation = 0;
    let inputId = 0;

    function renderTags(fragment, tags) {
        document.querySelectorAll('[data-tag-fragment]').forEach(element => {
            if (element.dataset.tagFragment !== fragment) return;
            const host = element.querySelector('.verse-link-controls') ?? element;
            host.querySelector('.verse-tags')?.remove();
            if (!tags.length) return;
            const list = document.createElement('ul');
            list.className = 'verse-tags';
            list.setAttribute('aria-label', `Tags for ${fragment}`);
            tags.forEach(tag => {
                const item = document.createElement('li');
                const link = document.createElement('a');
                link.className = 'verse-tag';
                link.href = `/Tag/${encodeURIComponent(tag.slug)}`;
                link.setAttribute('up-follow', '');
                link.textContent = tag.name;
                item.append(link);
                list.append(item);
            });
            const editor = host.querySelector('.tag-editor');
            if (editor) host.insertBefore(list, editor);
            else host.prepend(list);
        });
    }

    function addEditor(element) {
        if (element.querySelector('.tag-editor')) return;
        const fragment = element.dataset.tagFragment;
        const details = document.createElement('details');
        details.className = 'tag-editor';
        details.lang = 'en';
        const summary = document.createElement('summary');
        summary.textContent = 'Add tag';
        summary.setAttribute('aria-label', `Add tag to ${fragment}`);
        const form = document.createElement('form');
        const label = document.createElement('label');
        const input = document.createElement('input');
        input.id = `tag-name-${++inputId}`;
        input.name = 'name';
        input.required = true;
        input.maxLength = 80;
        input.className = 'form-control form-control-sm';
        input.setAttribute('list', 'tag-suggestions');
        input.placeholder = 'Choose an existing tag or enter a new one';
        label.htmlFor = input.id;
        label.className = 'visually-hidden';
        label.textContent = `Tag for ${fragment}`;
        const button = document.createElement('button');
        button.type = 'submit';
        button.className = 'btn btn-sm btn-outline-primary';
        button.textContent = 'Save';
        const status = document.createElement('p');
        status.setAttribute('role', 'status');
        form.append(label, input, button, status);
        form.addEventListener('submit', async event => {
            event.preventDefault();
            if (button.disabled) return;
            button.disabled = true;
            status.textContent = 'Saving…';
            try {
                const response = await fetch('/api/tags/fragment', {
                    method: 'POST', credentials: 'same-origin', cache: 'no-store',
                    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
                    body: JSON.stringify({ name: input.value, fragment })
                });
                const result = await response.json().catch(() => ({}));
                if (!response.ok) {
                    status.textContent = result.error ?? (response.status === 429
                        ? 'Too many requests. Please try again in a minute.'
                        : 'The request failed. Reload the page and try again.');
                    return;
                }
                renderTags(fragment, result.tags);
                const suggestions = document.getElementById('tag-suggestions');
                result.tags.forEach(tag => {
                    if (suggestions && !Array.from(suggestions.options).some(option => option.value === tag.name)) {
                        const option = document.createElement('option');
                        option.value = tag.name;
                        suggestions.append(option);
                    }
                });
                input.value = '';
                status.textContent = result.warning ?? 'Saved.';
            } catch {
                status.textContent = 'Connection lost. The change may have been saved; retrying is safe.';
            } finally { button.disabled = false; }
        });
        details.append(summary, form);
        (element.querySelector('.verse-link-controls') ?? element).append(details);
    }

    async function initialize() {
        const current = ++generation;
        try {
            const response = await fetch('/api/editor/session', { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok) return;
            const session = await response.json();
            if (current !== generation) return;
            csrfToken = session.csrfToken;
            if (!session.loggedIn) {
                document.querySelectorAll('.tag-editor, #tag-suggestions').forEach(element => element.remove());
                return;
            }
            if (!document.querySelector('[data-tag-fragment]')) return;
            const catalogResponse = await fetch('/api/tags', { cache: 'no-store' });
            if (!catalogResponse.ok) return;
            const catalog = await catalogResponse.json();
            if (current !== generation) return;
            document.getElementById('tag-suggestions')?.remove();
            const suggestions = document.createElement('datalist');
            suggestions.id = 'tag-suggestions';
            catalog.forEach(tag => {
                const option = document.createElement('option');
                option.value = tag.name;
                suggestions.append(option);
            });
            document.body.append(suggestions);
            document.querySelectorAll('[data-tag-fragment]').forEach(addEditor);
        } catch { /* Public reading remains available when the session endpoint is offline. */ }
    }
    document.addEventListener('DOMContentLoaded', initialize);
    document.addEventListener('up:fragment:inserted', initialize);
    window.addEventListener('pageshow', event => { if (event.persisted) void initialize(); });
})();

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

    function syncTranslationVerseLinks() {
        document.querySelectorAll('[data-preserve-verse-fragment="true"]').forEach(link => {
            link.hash = window.location.hash;
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
        const translationLink = clickedElement?.closest('[data-preserve-verse-fragment="true"]');
        if (translationLink instanceof HTMLAnchorElement) {
            translationLink.hash = window.location.hash;
        }

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
        syncTranslationVerseLinks();
    });

    document.addEventListener("up:location:changed", () => {
        normalizeLegacyVerseHash();
        syncCurrentVerseLink();
        syncTranslationVerseLinks();
    });

    document.addEventListener("up:fragment:inserted", () => {
        normalizeLegacyVerseHash();
        syncCurrentVerseLink();
        syncTranslationVerseLinks();
    });

    normalizeLegacyVerseHash();
    syncCurrentVerseLink();
    syncTranslationVerseLinks();
})();
