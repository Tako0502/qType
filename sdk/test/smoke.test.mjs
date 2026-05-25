// Smoke test — requires a running qType server at $QTYPE_BASE (default http://localhost:5188).
import test from 'node:test';
import assert from 'node:assert/strict';
import { QType } from '../dist/index.js';

const baseUrl = process.env.QTYPE_BASE ?? 'http://localhost:5188';
const qt = new QType({ baseUrl });

test('health returns true when server is up', async () => {
  const ok = await qt.health();
  assert.equal(ok, true);
});

test('stats returns sensible counts', async () => {
  const s = await qt.stats();
  assert.ok(s.lemmas > 100_000, `expected >100k lemmas, got ${s.lemmas}`);
  assert.ok(s.wordforms > 1_000_000, `expected >1M wordforms, got ${s.wordforms}`);
});

test('spellcheck resolves direct lemma', async () => {
  const r = await qt.spellcheck('сөздік');
  assert.equal(r.valid, true);
  assert.equal(r.via, 'lemma');
  assert.equal(r.lemma, 'сөздік');
});

test('spellcheck resolves inflected form to parent lemma', async () => {
  const r = await qt.spellcheck('кітабым');
  assert.equal(r.valid, true);
  assert.equal(r.via, 'wordform');
  assert.equal(r.lemma, 'кітап');
  assert.match(r.tag, /poss1sg/);
});

test('spellcheck resolves plural+ablative (3 layers of morphology)', async () => {
  const r = await qt.spellcheck('сөздіктерден');
  assert.equal(r.valid, true);
  assert.equal(r.via, 'wordform');
  assert.equal(r.lemma, 'сөздік');
  assert.equal(r.tag, 'pl+abl');
});

test('spellcheck rejects nonsense', async () => {
  const r = await qt.spellcheck('zzzqqq');
  assert.equal(r.valid, false);
});

test('suggest returns the correct lemma as a top result for a typo', async () => {
  const r = await qt.suggest('сөздк', 5);
  assert.ok(r.suggestions.length > 0);
  const top = r.suggestions[0];
  assert.equal(top.lemma, 'сөздік');
  assert.ok(top.distance <= 2);
});

test('define resolves inflected form to the parent lemma\'s entries', async () => {
  const r = await qt.define('балалар');
  assert.equal(r.matchedVia, 'wordform');
  assert.equal(r.lemma, 'бала');
  assert.ok(r.entries.length > 0);
});

test('lemmatize convenience returns the base lemma', async () => {
  assert.equal(await qt.lemmatize('балалар'), 'бала');
  assert.equal(await qt.lemmatize('кітабым'), 'кітап');
  assert.equal(await qt.lemmatize('zzzqqq'), null);
});

test('isKazakhWord predicate', async () => {
  assert.equal(await qt.isKazakhWord('сөздік'), true);
  assert.equal(await qt.isKazakhWord('сөздіктерден'), true);
  assert.equal(await qt.isKazakhWord('xyzqq'), false);
});

test('random returns a lemma object', async () => {
  const r = await qt.random();
  assert.ok(typeof r.id === 'number');
  assert.ok(typeof r.text === 'string' && r.text.length > 0);
});
