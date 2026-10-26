(() => {
    const toggle = document.getElementById('greek-polish-toggle');
    const status = document.getElementById('greek-polish-status');
    if (!toggle || !status) return;

    const storageKey = 'kdj:greek-polish';
    const greek = /(?=[\u0370-\u03ff\u1f00-\u1fff])\p{L}(?:(?=[\u0370-\u03ff\u1f00-\u1fff])\p{L}|\p{M})*/gu;
    let generation = 0;
    let controller;

    function restoreText() {
        document.querySelectorAll('[data-greek-original]').forEach(element => {
            element.replaceWith(document.createTextNode(element.dataset.greekOriginal));
        });
        document.getElementById('main-content')?.normalize();
    }

    function render(node, words) {
        const text = node.textContent;
        const matches = Array.from(text.matchAll(greek));
        if (words.length !== matches.length || words.some((word, index) =>
            word.greek !== matches[index][0] || typeof word.polish !== 'string' || !word.polish.trim())) {
            throw new Error('The translation could not be aligned with the Greek text.');
        }
        const fragment = document.createDocumentFragment();
        let offset = 0;
        for (let index = 0; index < matches.length; index++) {
            const match = matches[index];
            fragment.append(document.createTextNode(text.slice(offset, match.index)));
            const ruby = document.createElement('ruby');
            ruby.dataset.greekOriginal = match[0];
            ruby.lang = 'grc';
            ruby.className = 'greek-interlinear-word';
            ruby.append(document.createTextNode(match[0]));
            const translation = document.createElement('rt');
            translation.lang = 'pl';
            translation.textContent = words[index].polish;
            ruby.append(translation);
            fragment.append(ruby);
            offset = match.index + match[0].length;
        }
        fragment.append(document.createTextNode(text.slice(offset)));
        node.replaceWith(fragment);
    }

    async function translatePage() {
        const current = ++generation;
        controller?.abort();
        controller = new AbortController();
        restoreText();
        status.textContent = '';
        if (!toggle.checked) return;
        const main = document.getElementById('main-content');
        if (!main) return;

        const walker = document.createTreeWalker(main, NodeFilter.SHOW_TEXT, {
            acceptNode(node) {
                const parent = node.parentElement;
                if (!parent || parent.closest('script, style, textarea, input, select, code, pre, rt, [contenteditable="true"], [hidden]')
                    || !parent.getClientRects().length || !Array.from(node.textContent.matchAll(greek)).length) {
                    return NodeFilter.FILTER_REJECT;
                }
                return NodeFilter.FILTER_ACCEPT;
            }
        });
        const pending = [];
        while (walker.nextNode()) {
            const node = walker.currentNode;
            const context = node.parentElement.closest('.verse-list, .translation-verse-card, p, h1, h2, h3, h4, h5, h6, li')
                ?? node.parentElement;
            pending.push({ node, text: node.textContent, context: context.textContent });
        }
        // Split long literary paragraphs at word boundaries and retain nearby context.
        for (let index = 0; index < pending.length; index++) {
            const item = pending[index];
            if (item.text.length > 2000) {
                let boundary = item.text.lastIndexOf(' ', 2000);
                if (boundary < 1) boundary = 2000;
                const remainder = item.node.splitText(boundary);
                pending.splice(index + 1, 0, { node: remainder, text: remainder.textContent, context: item.context });
                item.text = item.node.textContent;
            }
            if (item.context.length > 12000) {
                const start = Math.max(0, item.context.indexOf(item.text) - 4000);
                item.context = item.context.slice(start, start + 12000);
            }
        }
        if (!pending.length) {
            status.textContent = 'No Greek text on this page.';
            return;
        }
        status.textContent = 'Translating Greek in context…';
        try {
            for (const item of pending) {
                if (current !== generation) return;
                if (!item.node.isConnected) continue;
                const response = await fetch('/api/greek-interlinear', {
                    method: 'POST', credentials: 'same-origin',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ text: item.text, context: item.context }),
                    signal: controller.signal
                });
                const result = await response.json();
                if (!response.ok) throw new Error(result.error ?? 'Translation is unavailable.');
                if (current !== generation) return;
                if (item.node.isConnected) render(item.node, result.words);
            }
            status.textContent = 'Polish forms follow the context. Automatic translations may contain errors.';
        } catch (error) {
            if (current !== generation || error.name === 'AbortError') return;
            status.textContent = `${error.message} Switch off and on to retry.`;
        }
    }

    try { toggle.checked = localStorage.getItem(storageKey) === 'true'; }
    catch { /* The switch also works without browser storage. */ }
    toggle.addEventListener('change', () => {
        try { localStorage.setItem(storageKey, String(toggle.checked)); }
        catch { /* Saving the preference is optional. */ }
        void translatePage();
    });
    document.addEventListener('up:fragment:inserted', () => void translatePage());
    document.addEventListener('change', event => {
        if (event.target.matches('[data-tag-translation-select]')) void translatePage();
    });
    window.addEventListener('pageshow', event => { if (event.persisted) void translatePage(); });
    void translatePage();
})();
