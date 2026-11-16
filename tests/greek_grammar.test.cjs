const assert = require('node:assert/strict');
const test = require('node:test');
const { describe } = require('../KazdyDzienZJezusem/wwwroot/js/greek-grammar.js');

test('Greek nominal morphology explains case, number and gender in Polish', () => {
    assert.match(describe('N-GSM'), /rzeczownik; dopełniacz; liczba pojedyncza; rodzaj męski/);
    assert.match(describe('N-DSF'), /rzeczownik; celownik; liczba pojedyncza; rodzaj żeński/);
    assert.match(describe('T-NPM'), /rodzajnik; mianownik; liczba mnoga; rodzaj męski/);
});

test('Verbs and indeclinables do not pretend to be Polish case forms', () => {
    assert.match(describe('V-FDI-3S'), /czas przyszły; strona medialna.*3\. osoba; liczba pojedyncza/);
    assert.match(describe('PREP'), /przyimek \(nieodmienny/);
    assert.match(describe('UNKNOWN'), /nierozpoznany/);
});
