# qType — Lemmatization Pipeline

Each stage is a separate, idempotent job. Re-running a stage is safe (resumes via `rawIndex` / lemma `text` uniqueness).

## Stage 0 — Raw load (built, this repo)

**Input:** `~/Downloads/sq_source.json`, `~/Downloads/sq_items.json`
**Output:** `qsource`, `qentry`, `qheadword`

```bash
# one-time DB setup
mysql -uroot < db/001_schema.sql
mysql -uroot < db/002_seed_sources.sql

# run the importer
dotnet run --project src/QType.Importer -- all
# or stage-by-stage
dotnet run --project src/QType.Importer -- sources
dotnet run --project src/QType.Importer -- entries
```

Config in `src/QType.Importer/appsettings.json`. Paths support `~/`. To test against a slice set `Import:MaxItems` to e.g. `1000`.

Resume behavior: `entries` reads `MAX(rawIndex)` and skips items already imported, so killing and restarting is safe.

## Stage 1 — Classify (todo)

Tag each `qheadword`:
- single-word vs phrase (already done in Stage 0 via `isPhrase`)
- pure Cyrillic vs mixed scripts
- proper noun heuristic (initial-capital + appears in only proper-noun-source IDs)

Write back to `qheadword.qStatus` as a bitfield, or add columns.

## Stage 2 — Lemma extract (todo)

Goal: populate `qlemma` from `qheadword`, fill `qlemmaentry`.

Approach:
1. Group `qheadword` rows by `textNormalized`.
2. For each unique normalized form that is **not** a phrase, insert a `qlemma`.
3. Link every contributing entry via `qlemmaentry`.
4. Phrases go to `qphrase` instead (later stage, since phrases need a head-lemma resolved first).

Expected output: roughly 150–200k single-word lemmas (after dropping phrases + dialect-only).

## Stage 3 — HTML parse (todo)

Goal: extract structured payloads from `qentry.rawHtml`.

Tools: HtmlAgilityPack + per-source extractors (different dictionaries use different markup conventions).

For each entry:
- `<i>…</i>` after etymology parens → `qdefinition.text`
- `(ар. … )` / `(пар. … )` / `(рус. … )` → `qetymology`
- `<p>` with `<b>headword</b>` inside → `qexample.sentence` + citation in parens at end
- Bolded sub-entries like `<b>Бақи жай</b> – о дүние` → `qphrase`

Build extractors source-by-source. Tier 1 raw HTML never changes, so Stage 3 can be re-run any time you improve a parser.

## Stage 4 — Morphology (todo)

Generate inflected forms for every lemma.

Recommended: **apertium-kaz** as a separate Python/Bash service.

```bash
# one-time setup on macOS
brew install lttoolbox apertium hfst
git clone https://github.com/apertium/apertium-kaz && cd apertium-kaz && ./autogen.sh && make
```

Workflow:
1. `.NET` side dumps lemmas to a temp file (one per line)
2. Pipes through `apertium -d apertium-kaz kaz-morph` (or `hfst-lookup`)
3. Reads back analyses, generates form set
4. Bulk inserts into `qwordform`

Backfill: any `qheadword.textNormalized` that doesn't match any generated form is either a loanword, proper noun, or analyzer gap — flag for review and insert as a fallback form attached to the same lemma.

## Stage 5 — Frequency (todo)

Sources to consider:
- [Leipzig Kazakh corpus](https://wortschatz.uni-leipzig.de/en/download/Kazakh) — pre-tokenized, easy
- Wikipedia kk dump
- Your own crawl of `tengrinews.kz`, `egemen.kz`, etc.

Tokenize, lowercase, count, upsert into `qfrequency`.

## Stage 6 — Quality & enrichment (ongoing)

- LLM-assisted definition cleanup for top-N frequency lemmas, queued in a moderator UI
- User-reported corrections → `qmisspelling` with `origin='learned'`
- POS classifier trained on the Stage 3 parsed data

## Pipeline state

Every stage writes a row into `qpipelinerun` at start and updates it on finish (with row counts, notes, errors). Use it to answer "when was X last run, and on how many rows?"
