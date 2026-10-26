(() => {
    const status = document.getElementById('greek-polish-status');
    const attribution = document.getElementById('greek-polish-source');
    const supplementAttribution = document.getElementById('greek-supplement-source');
    const options = {
        polish: { label: 'Polish', language: 'pl', alternatives: 'alternatives' },
        english: { label: 'English', language: 'en', alternatives: 'englishAlternatives' },
        grammar: { label: 'Grammar code', language: 'en', alternatives: 'grammarAlternatives' },
        pronunciation: { label: 'Pronunciation (Erasmian)', language: 'pl', alternatives: 'pronunciationAlternatives' }
    };
    for (const [name, option] of Object.entries(options)) {
        option.input = document.getElementById(`greek-${name}-toggle`);
        option.storageKey = `kdj:greek-${name}`;
    }
    if (!status || Object.values(options).some(option => !option.input)) return;

    const greek = /(?=[\u0370-\u03ff\u1f00-\u1fff])\p{L}(?:(?=[\u0370-\u03ff\u1f00-\u1fff])\p{L}|\p{M})*/gu;
    const cache = new Map();
    let generation = 0;
    let controller;

    function restoreText() {
        document.querySelectorAll('[data-greek-original]').forEach(element => {
            element.replaceWith(document.createTextNode(element.dataset.greekOriginal));
        });
        document.getElementById('main-content')?.normalize();
    }

    function render(node, words, enabled) {
        const text = node.textContent;
        const matches = Array.from(text.matchAll(greek));
        if (words.length !== matches.length || words.some((word, index) =>
            word.greek !== matches[index][0] || enabled.some(name => !Array.isArray(word[options[name].alternatives])))) {
            throw new Error('The dictionary data could not be aligned with the Greek text.');
        }
        const fragment = document.createDocumentFragment();
        let offset = 0;
        for (let index = 0; index < matches.length; index++) {
            const match = matches[index];
            const word = words[index];
            fragment.append(document.createTextNode(text.slice(offset, match.index)));
            const ruby = document.createElement('ruby');
            ruby.dataset.greekOriginal = match[0];
            ruby.dataset.greekMatch = word.match;
            ruby.lang = 'grc';
            ruby.className = 'greek-interlinear-word';
            const source = document.createElement('span');
            source.className = 'greek-source-word';
            source.textContent = match[0];
            ruby.append(source);
            const annotations = document.createElement('rt');
            const titles = [];
            // Top to bottom, followed by the Greek source below the annotations.
            for (const name of ['grammar', 'english', 'polish', 'pronunciation']) {
                if (!enabled.includes(name)) continue;
                const option = options[name];
                const alternatives = word[option.alternatives];
                const layer = document.createElement('span');
                layer.dataset.greekLayer = name;
                layer.lang = option.language;
                layer.textContent = word[name] ?? (alternatives.length ? alternatives.slice(0, 3).join(' / ') : '?');
                if (name === 'english' && word.englishIsDictionary) {
                    layer.dataset.greekDictionary = 'true';
                    if (layer.textContent.length > 72) layer.textContent = layer.textContent.slice(0, 69) + '…';
                    layer.textContent += ' †';
                    layer.title = `Dictionary meaning of ${word.englishLemmas.join(' / ')}; not an inflected translation.`;
                    titles.push(layer.title);
                }
                annotations.append(layer);
                titles.push(`${option.label}: ${alternatives.length ? alternatives.join(' / ') : 'missing from the static dictionary'}`);
            }
            ruby.title = titles.join('\n');
            ruby.append(annotations);
            fragment.append(ruby);
            offset = match.index + match[0].length;
        }
        fragment.append(document.createTextNode(text.slice(offset)));
        node.replaceWith(fragment);
    }

    function referenceFor(node) {
        const element = node.parentElement;
        const fragment = element.closest('[data-tag-fragment]')?.dataset.tagFragment;
        if (fragment && !fragment.includes('/')) return fragment;
        const link = element.closest('.translation-verse-card')?.querySelector('.translation-verse-heading a')
            ?? element.closest('.tag-passage')?.querySelector('h3 a');
        if (!link) return null;
        const url = new URL(link.href);
        const match = /^\/Biblia\/[^/]+\/([^/]+)\/(\d+)$/.exec(url.pathname);
        const verse = element.closest('.bible-verse')?.querySelector('.verse-number')?.textContent.trim() ?? url.hash.slice(1);
        return match && /^\d+$/.test(verse) ? `${decodeURIComponent(match[1])}${match[2]},${verse}` : null;
    }

    async function annotatePage() {
        const current = ++generation;
        controller?.abort();
        controller = new AbortController();
        restoreText();
        status.textContent = '';
        if (supplementAttribution) supplementAttribution.hidden = true;
        const enabled = Object.keys(options).filter(name => options[name].input.checked);
        if (attribution) attribution.hidden = enabled.length === 0;
        if (!enabled.length) return;
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
            pending.push({ node, text: node.textContent, context: context.textContent, reference: referenceFor(node) });
        }
        for (let index = 0; index < pending.length; index++) {
            const item = pending[index];
            if (item.text.length > 2000) {
                let boundary = item.text.lastIndexOf(' ', 2000);
                if (boundary < 1) boundary = 2000;
                const remainder = item.node.splitText(boundary);
                pending.splice(index + 1, 0, { node: remainder, text: remainder.textContent, context: item.context, reference: item.reference });
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
        status.textContent = 'Loading dictionary annotations…';
        try {
            let missing = 0;
            let ambiguous = 0;
            for (const item of pending) {
                if (current !== generation) return;
                if (!item.node.isConnected) continue;
                const key = JSON.stringify([item.text, item.reference]);
                let result = cache.get(key);
                if (!result) {
                    const response = await fetch('/api/greek-interlinear', {
                        method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ text: item.text, context: item.context, reference: item.reference }),
                        signal: controller.signal
                    });
                    result = await response.json();
                    if (!response.ok) throw new Error(result.error ?? 'Dictionary annotations are unavailable.');
                    if (current !== generation) return;
                    if (cache.size >= 250) cache.delete(cache.keys().next().value);
                    cache.set(key, result);
                }
                missing += result.words.filter(word => enabled.some(name => !word[options[name].alternatives]?.length)).length;
                ambiguous += result.words.filter(word => enabled.some(name => word[options[name].alternatives]?.length > 1)).length;
                if (supplementAttribution && result.words.some(word => word.englishSources?.length))
                    supplementAttribution.hidden = false;
                if (item.node.isConnected) render(item.node, result.words, enabled);
            }
            status.textContent = `Static dictionary. Missing forms: ${missing}; forms with alternatives: ${ambiguous}.`;
        } catch (error) {
            if (current !== generation || error.name === 'AbortError') return;
            status.textContent = `${error.message} Switch an annotation off and on to retry.`;
        }
    }

    for (const option of Object.values(options)) {
        try { option.input.checked = localStorage.getItem(option.storageKey) === 'true'; }
        catch { /* Reading preferences is optional. */ }
        option.input.addEventListener('change', () => {
            try { localStorage.setItem(option.storageKey, String(option.input.checked)); }
            catch { /* Each switch works when browser storage is unavailable. */ }
            void annotatePage();
        });
    }
    document.addEventListener('up:fragment:inserted', () => void annotatePage());
    document.addEventListener('change', event => {
        if (event.target.matches('[data-tag-translation-select]')) void annotatePage();
    });
    window.addEventListener('pageshow', event => { if (event.persisted) void annotatePage(); });
    void annotatePage();
})();
