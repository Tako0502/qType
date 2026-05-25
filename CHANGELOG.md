# Changelog

All notable changes to qType. Follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/).

## [0.2.0] — 2026-05-25

### SDK
- Added `Suggestion.frequency` field (corpus-derived count from Wikipedia kk). Used by backend for tie-breaking at the same edit distance. Non-breaking additive change.
- Bumped to `@smoker_winston/qtype@0.2.0`.

### MCP server
- Initial publish as `@smoker_winston/qtype-mcp@0.2.0`. Versioned to match the SDK.

### Backend
- Stage 1 — Kazakh POS classification (`KazakhPos.Classify`) from suffix heuristics; 20,659 verbs / 98,180 nouns / 9,849 adjectives / 4,207 adverbs identified.
- Verb conjugation in `KazakhVerb.Conjugate`: past (with full person), present-future, future-intent, imperative, optative, participle, perfect, converbs, agent noun, gerund, negative forms. Routed via `KazakhInflector.Inflect(lemma, pos)`.
- Stage 5 — Leipzig Wikipedia kk corpus frequency loader (`FrequencyImporter`, 169k word forms with counts).
- Frequency-aware suggestion ranking in `DictionaryService.SuggestAsync`: orders by (distance, -frequency, length).
- 143 xUnit tests covering phonology, inflection, verbs, POS, lemmatizer (`tests/QType.Tests/`).
- GitHub Actions CI (`.github/workflows/ci.yml`) running .NET tests + SDK build + MCP build on every push/PR.
- Server-side allow-list HTML sanitizer (`HtmlSanitizer`) applied to `/api/define` output to prevent XSS via dictionary content.
- `IMemoryCache` layer in `DictionaryService` with 5-min TTL on spellcheck and define calls (~14× perf on repeat lookups).
- `CONTRIBUTING.md`, `CHANGELOG.md`, issue templates, PR template.

### Changed
- Plural suffix rule refined: stems ending in л/з/ж/m/н/ң now correctly take `-дар/-дер` instead of the previously-wrong `-лар/-лер` for л/з. (e.g. `ұл → ұлдар`, `сөз → сөздер`.)
- Future-intent and negative-verb markers (`-мақ/-бақ/-пақ`, `-ма/-ба/-па`) refined to use correct sandhi after sonorants (л, р, й, у, м, н, ң) → `-м-` initial.
- Vowel harmony rule: `у` and `и` are now treated as harmonically neutral; harmony is determined by the rightmost non-neutral vowel. Fixes verbs like `білу` which is front-harmonic (was incorrectly back).
- Past-tense 1pl person ending now harmonizes к/қ (e.g. `келдік` instead of `келдіқ`).
- HTML responses from `/api/define` now sanitized via allow-list (only `<p>`, `<i>`, `<em>`, `<b>`, `<strong>`, `<br>`, `<sub>`, `<sup>`, `<u>`, `<small>` survive, with no attributes).

### Internal
- POS classifier added as Stage 1 in pipeline (`dotnet run --project src/QType.Importer -- pos`).
- `qlemma.pos` populated; verb generation gated on it.

## [0.1.0] — 2026-05-25

### Added
- Three-tier MySQL schema (`db/001_schema.sql`): raw / parsed / derived.
- Stage 0 — raw load: 379,349 dictionary entries, 409,336 headwords, 181 sources from `sq_items.json` / `sq_source.json`. Resumable via `rawIndex`.
- Stage 2 — lemma extraction: 133,226 canonical lemmas, 266,937 lemma↔entry links, 142,349 phrases.
- Stage 4 — rule-based Kazakh morphology generator: vowel harmony, sandhi, consonant alternation (п↔б, к↔г, қ↔ғ). 2.2M inflected forms.
- REST API in `src/QType.Web`: `/api/spellcheck`, `/api/suggest`, `/api/define`, `/api/random`, `/api/stats`. Tailwind frontend at `/` with live inflection-aware lookup.
- TypeScript SDK published as `@smoker_winston/qtype` on npm. Zero runtime dependencies, dual ESM+CJS, full types.
- MCP server (`@smoker_winston/qtype-mcp`) exposing 6 tools to MCP-aware AI clients.
- MIT licensed.
