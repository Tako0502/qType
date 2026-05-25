# @smoker_winston/qtype

Kazakh language SDK — spellcheck, autocorrect, definitions, and **morphology-aware** lookup powered by [qType](https://github.com/Tako0502/qType).

Works in Node 18+ and modern browsers. Zero runtime dependencies. ESM + CJS dual build with full TypeScript types.

## Install

```bash
npm install @smoker_winston/qtype
```

## Quick start

```ts
import { QType } from '@smoker_winston/qtype';

const qt = new QType({ baseUrl: 'http://localhost:5188' });

// Is it a valid Kazakh word?
await qt.isKazakhWord('сөздіктерден'); // → true (plural + ablative of сөздік)

// What's the canonical form?
await qt.lemmatize('кітабым'); // → 'кітап'   (1sg possessive with п→б alternation)

// Get corrections for a typo
const { suggestions } = await qt.suggest('сөздк', 5);
// suggestions[0] = { form: 'сөздік', lemma: 'сөздік', distance: 1, source: 'lemma' }

// Definition (from real Kazakh dictionaries)
const { entries, lemma, tag } = await qt.define('балалар');
// lemma: 'бала', tag: 'pl', entries: [ { sourceTitle, rawHtml }, ... ]
```

## What you get

| Method | Returns |
|---|---|
| `spellcheck(word)` | `{ valid, via: 'lemma'\|'wordform'\|'none', lemma, tag, ... }` |
| `suggest(word, k=5)` | `{ suggestions: [{ form, lemma, distance, source }, ...] }` |
| `define(word)` | `{ lemma, matchedVia, tag, entries: [{ rawHtml, sourceTitle, ... }] }` |
| `lemmatize(word)` | `string \| null` — convenience over `spellcheck` |
| `isKazakhWord(word)` | `boolean` |
| `random()` | `{ id, text }` |
| `stats()` | `{ entries, lemmas, wordforms, ... }` |
| `health()` | `boolean` |

## Configuration

```ts
new QType({
  baseUrl: 'https://api.qtype.kz',  // your hosted qType backend
  timeoutMs: 5000,                   // default 10s
  headers: { Authorization: 'Bearer …' },
  fetch: customFetch,                // override (testing, edge runtimes)
});
```

## Running a backend

The SDK calls a qType server. To run your own:

```bash
git clone https://github.com/Tako0502/qType
cd qType
# set MySQL password in src/QType.Importer/appsettings.json
mysql -uroot < db/001_schema.sql
dotnet run --project src/QType.Importer -- all      # load + lemmatize + morph (~1 minute)
dotnet run --project src/QType.Web -c Release       # serves http://localhost:5188
```

A public hosted endpoint is on the roadmap.

## TypeScript

Full types ship with the package — `QTypeOptions`, `SpellCheckResult`, `SuggestResult`, `DefineResult`, `Suggestion`, `EntryHit`, `Stats`, `RandomLemma`, `QTypeError`.

## Errors

Failed requests throw `QTypeError` with `status` and `body` when an HTTP error occurs, or a timeout/network description otherwise.

```ts
import { QTypeError } from '@smoker_winston/qtype';

try {
  await qt.define('бала');
} catch (e) {
  if (e instanceof QTypeError) console.error(e.status, e.body);
}
```

## License

MIT — see [LICENSE](../LICENSE) at repo root.
