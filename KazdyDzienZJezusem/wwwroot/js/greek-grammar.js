// Explain GNT/Ross morphology in Polish; do not guess unsupported codes.
(function (root) {
    'use strict';
    const cases = { N: 'mianownik', G: 'dopełniacz', D: 'celownik', A: 'biernik', V: 'wołacz' };
    const numbers = { S: 'liczba pojedyncza', P: 'liczba mnoga' };
    const genders = { M: 'rodzaj męski', F: 'rodzaj żeński', N: 'rodzaj nijaki' };
    const parts = { N: 'rzeczownik', A: 'przymiotnik', T: 'rodzajnik',
        P: 'zaimek', R: 'zaimek względny', D: 'zaimek wskazujący',
        F: 'zaimek zwrotny', I: 'zaimek pytajny' };
    const tenses = { P: 'czas teraźniejszy', I: 'imperfekt (czas przeszły niedokonany)',
        F: 'czas przyszły', A: 'aoryst', R: 'perfectum', L: 'plusquamperfectum', X: 'czas przyszły dokonany' };
    const voices = { A: 'strona czynna', M: 'strona medialna', P: 'strona bierna',
        D: 'strona medialna (deponens)', N: 'strona bierna (deponens)', E: 'strona medialna/bierna' };
    const moods = { I: 'tryb oznajmujący', S: 'tryb łączący', O: 'tryb życzący',
        M: 'tryb rozkazujący', N: 'bezokolicznik', P: 'imiesłów' };
    const unchanged = { PREP: 'przyimek (nieodmienny; znaczenie zależy od konstrukcji)',
        CONJ: 'spójnik (nieodmienny)', ADV: 'przysłówek', PRT: 'partykuła', INJ: 'wykrzyknik' };

    function single(code) {
        const key = (code || '').trim();
        if (!key) return 'Brak dostępnego kodu gramatycznego.';
        if (unchanged[key.toUpperCase()]) return key + ' — ' + unchanged[key.toUpperCase()] + '.';
        const nominal = /^([NATPRDFI])-([NGDAV])([SP])([MFN])$/.exec(key);
        if (nominal)
            return key + ' — ' + [parts[nominal[1]], cases[nominal[2]], numbers[nominal[3]], genders[nominal[4]]].join('; ') + '.';
        const personal = /^P-([123])([NGDAV])([SP])$/.exec(key);
        if (personal)
            return key + ' — zaimek osobowy; ' + personal[1] + '. osoba; ' + cases[personal[2]] + '; ' + numbers[personal[3]] + '.';
        const verb = /^V-([12]?)([PIFARLX])([AMPDNE])([ISOMNP])(?:-([123][SP]|[NGDAV][SP][MFN]))?$/.exec(key);
        if (verb) {
            const details = ['czasownik', tenses[verb[2]], voices[verb[3]], moods[verb[4]]];
            if (verb[1]) details.push(verb[1] + '. forma aorystu');
            const ending = verb[5] || '';
            if (/^[123][SP]$/.test(ending)) details.push(ending[0] + '. osoba', numbers[ending[1]]);
            else if (/^[NGDAV][SP][MFN]$/.test(ending))
                details.push(cases[ending[0]], numbers[ending[1]], genders[ending[2]]);
            return key + ' — ' + details.join('; ') + '.';
        }
        if (/^ADV-[CS]$/i.test(key))
            return key + ' — przysłówek; ' + (key.endsWith('C') ? 'stopień wyższy' : 'stopień najwyższy') + '.';
        return key + ' — kod nierozpoznany w objaśniaczu; nie należy wnioskować o odmianie z samego skrótu.';
    }
    function describe(code) {
        if (!code) return single('');
        return String(code).split(/\s+\/\s+/).map(single).join('\n');
    }
    const api = { describe };
    root.KdjGreekGrammar = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof globalThis !== 'undefined' ? globalThis : this);
