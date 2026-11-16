(() => {
    const status = document.getElementById('greek-polish-status');
    const attribution = document.getElementById('greek-polish-source');
    const supplementAttribution = document.getElementById('greek-supplement-source');
    const perseusAttribution = document.getElementById('greek-perseus-source');
    const options = {
        polish: { label: 'Polish (kontekst)', language: 'pl', alternatives: 'alternatives' },
        morphology: { label: 'Polski (Strong + gramatyka)', language: 'pl', alternatives: 'morphologyAlternatives' },
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
    const wordDetails = new WeakMap();
    const grammarDescription = code => window.KdjGreekGrammar?.describe(code) ?? (code || 'Brak kodu');
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
            for (const name of ['grammar', 'english', 'polish', 'morphology', 'pronunciation']) {
                if (!enabled.includes(name)) continue;
                const option = options[name];
                const alternatives = word[option.alternatives];
                const layer = document.createElement('span');
                layer.dataset.greekLayer = name;
                layer.lang = option.language;
                layer.textContent = (name === 'morphology' ? word.morphology?.polish : word[name])
                    ?? (alternatives.length ? alternatives.slice(0, 3).join(' / ') : '?');
                if (name === 'grammar') layer.title = grammarDescription(word.grammar || alternatives.join(' / '));
                if (name === 'morphology') {
                    layer.dataset.greekMorphologyStatus = word.morphology?.status ?? 'unavailable';
                    layer.title = 'Strong: ' + (word.morphology?.strong || 'brak') + '\n'
                        + grammarDescription(word.morphology?.grammar) + '\n'
                        + 'Glosa z korpusu: to nie jest automatycznie sprawdzona polska deklinacja.';
                }
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
            const code = word.morphology?.grammar ?? word.grammar ?? word.grammarAlternatives?.join(' / ');
            titles.push('Gramatyka: ' + grammarDescription(code));
            titles.push('Kliknij słowo, aby zobaczyć Strong, odmianę i warianty tłumaczenia.');
            ruby.title = titles.join('\n');
            ruby.tabIndex = 0;
            ruby.setAttribute('role', 'button');
            ruby.setAttribute('aria-label', 'Szczegóły greckiego słowa ' + match[0]);
            wordDetails.set(ruby, word);
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
        if (perseusAttribution) perseusAttribution.hidden = true;
        const enabled = Object.keys(options).filter(name => options[name].input.checked);
        const useMorphology = enabled.includes('morphology');
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
                const key = JSON.stringify([item.text, item.reference, useMorphology]);
                let result = cache.get(key);
                if (!result) {
                    const response = await fetch('/api/greek-interlinear', {
                        method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ text: item.text, context: item.context, reference: item.reference, morphology: useMorphology }),
                        signal: controller.signal
                    });
                    result = await response.json();
                    if (!response.ok) throw new Error(result.error ?? 'Dictionary annotations are unavailable.');
                    if (current !== generation) return;
                    if (cache.size >= 250) cache.delete(cache.keys().next().value);
                    cache.set(key, result);
                }
                if (useMorphology) {
                    if (!Array.isArray(result.morphology) || result.morphology.length !== result.words.length)
                        throw new Error('The Strong annotations could not be aligned with this passage.');
                    result.words.forEach((word, i) => {
                        word.morphology = result.morphology[i];
                        word.morphologyAlternatives = word.morphology?.alternatives ?? [];
                    });
                }
                missing += result.words.filter(word => enabled.some(name => !word[options[name].alternatives]?.length)).length;
                ambiguous += result.words.filter(word => enabled.some(name => word[options[name].alternatives]?.length > 1)).length;
                if (supplementAttribution && result.words.some(word => word.englishSources?.length))
                    supplementAttribution.hidden = false;
                if (perseusAttribution && result.words.some(word => word.englishSources?.includes('Perseus / Logeion')))
                    perseusAttribution.hidden = false;
                if (item.node.isConnected) render(item.node, result.words, enabled);
            }
            status.textContent = `Static dictionary. Missing forms: ${missing}; forms with alternatives: ${ambiguous}.`;
            if (useMorphology) status.textContent += ' Strong/gramatyka: glosy poświadczone, bez automatycznej odmiany po polsku.';
        } catch (error) {
            if (current !== generation || error.name === 'AbortError') return;
            status.textContent = `${error.message} Switch an annotation off and on to retry.`;
        }
    }


    const helpContent = {
        polish: ['Polski — kontekst', 'Zachowany dotychczasowy tryb. Pokazuje polskie odpowiedniki dopasowane do konkretnego wersetu z korpusu Rapacza (Oblubienica/BibleHub). Mogą być idiomatyczne: np. ἐν może wystąpić jako „na”, „z” albo „w”. To nie jest rekonstrukcja odmiany ze Stronga.'],
        morphology: ['Polski — Strong + gramatyka', 'Dla tekstu TR identyfikujemy słowo z konkretnego wersetu w danych GNT, łącząc numer Stronga z pełnym kodem gramatycznym (np. G4536 + N-DSF). Szukamy polskich glos poświadczonych dla dokładnie tej pary. Pierwsza z nich jest wyświetlana, a pozostałe po kliknięciu. Nie korzystamy ze znaczeń samych lematów jako odmienionych słów. NIE jest to generator polskiej fleksji: grecki celownik może oznaczać np. polski celownik lub narzędnik. Glosy kontekstowe nadal mogą nie odpowiadać wymaganemu polskiemu przypadkowi. Gdy brakuje wiarygodnego dopasowania, pokazujemy „?”. W tekstach poza TR nie przypisujemy numeru Stronga na podstawie samego podobieństwa słowa.'],
        english: ['English', 'Angielskie odpowiedniki z korpusu; znak † oznacza tylko definicję słownikową, nie tłumaczenie odmienionej formy.'],
        grammar: ['Kod gramatyczny', 'Najedź kursorem lub ustaw fokus na kodzie, aby zobaczyć jego rozwinięcie po polsku. N-GSM: rzeczownik, dopełniacz, liczba pojedyncza, rodzaj męski. N-DSF: rzeczownik, celownik, liczba pojedyncza, rodzaj żeński. Kod greckiego przypadku nie wyznacza automatycznie polskiego odpowiednika.'],
        pronunciation: ['Wymowa', 'Przybliżona wymowa greki według erazmiańskiej praktyki szkolnej, zapisana polskimi znakami. Nie oddaje akcentu ani długości samogłosek.']
    };

    function openHelp(name) {
        const item = helpContent[name];
        if (!item) return;
        document.getElementById('greek-help-title').textContent = item[0];
        document.getElementById('greek-help-body').textContent = item[1];
        window.bootstrap.Modal.getOrCreateInstance(document.getElementById('greek-help-modal')).show();
    }

    function openWord(word) {
        const title = document.getElementById('greek-word-title');
        const body = document.getElementById('greek-word-body');
        if (!title || !body) return;
        title.textContent = 'Słowo: ' + word.greek;
        body.replaceChildren();
        function field(label, value) {
            if (value === null || value === undefined || value === '') return;
            const paragraph = document.createElement('p');
            paragraph.className = 'mb-2';
            const heading = document.createElement('strong');
            heading.textContent = label + ': ';
            paragraph.append(heading, document.createTextNode(value));
            body.append(paragraph);
        }
        const morphology = word.morphology;
        field('Strong (TR)', morphology?.strong || 'brak wiarygodnego dopasowania do wersetu TR');
        field('Forma słownikowa (TR)', morphology?.lemma);
        field('Kod i gramatyka', grammarDescription(morphology?.grammar ?? word.grammar ?? word.grammarAlternatives?.join(' / ')));
        field('Polska glosa wg Stronga i kodu', morphology?.polish || 'brak poświadczonej glosy dla tej pary');
        field('Inne poświadczone glosy z tym samym kodem', morphology?.alternatives?.filter(x => x !== morphology.polish).join(' / ') || 'brak');
        field('Polski odpowiednik kontekstowy', word.polish || word.alternatives?.join(' / ') || 'brak');
        field('Angielski odpowiednik', word.english || word.englishAlternatives?.join(' / '));
        field('Definicja źródłowa GNT (angielska)', morphology?.dictionaryDefinition);
        field('Wymowa', word.pronunciation || word.pronunciationAlternatives?.join(' / '));
        const note = document.createElement('p');
        note.className = 'small text-secondary mt-3 mb-0';
        note.textContent = 'Strong identyfikuje hasło, a kod opisuje gramatykę grecką. Wskazana polska glosa pochodzi z korpusu i nie jest gwarantowaną polską odmianą ani analizą składni całego zdania.';
        body.append(note);
        window.bootstrap.Modal.getOrCreateInstance(document.getElementById('greek-word-modal')).show();
    }

    document.addEventListener('click', event => {
        const help = event.target.closest('[data-greek-help]');
        if (help) { openHelp(help.dataset.greekHelp); return; }
        const ruby = event.target.closest('ruby.greek-interlinear-word');
        if (ruby && wordDetails.has(ruby)) {
            event.preventDefault();
            openWord(wordDetails.get(ruby));
        }
    });
    document.addEventListener('keydown', event => {
        if (event.key !== 'Enter' && event.key !== ' ') return;
        const ruby = event.target.closest?.('ruby.greek-interlinear-word');
        if (!ruby || !wordDetails.has(ruby)) return;
        event.preventDefault();
        openWord(wordDetails.get(ruby));
    });

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
