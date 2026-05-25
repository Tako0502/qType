# qType

Kazakh language platform — spellcheck, autocorrect, definitions, MCP server, i18n SDK.

The foundation is in place and **ready for manual testing**: the full 379k Sozdikqor dictionary corpus is loaded into MySQL, lemmas extracted, and a REST API exposes spellcheck / suggest / define / random / stats.

## Status

| Stage | Status |
|---|---|
| Schema (3 tiers, 13 tables) | ✅ |
| Stage 0 — raw load (sources + 379k entries + 409k headwords) | ✅ |
| Stage 2 — lemma extract (133k lemmas, 267k links, 142k phrases) | ✅ |
| **Stage 4 — morphology (rule-based, 2.2M inflected forms)** | ✅ |
| REST API (spellcheck / suggest / define / random / stats) | ✅ |
| Frontend demo (Tailwind, live spellcheck, inflection-aware) | ✅ |
| TypeScript SDK (`@smoker_winston/qtype` on npm) | ✅ |
| **MCP server** (`@smoker_winston/qtype-mcp` on npm) | ✅ |
| Stage 1 — classify (POS, proper-noun heuristic) | ☐ |
| Stage 3 — HTML parse (definitions / etymology / examples) | ☐ |
| Stage 5 — frequency (corpus) | ☐ |
| MCP server | ☐ |
| JS i18n SDK | ☐ |
| iOS keyboard / browser extension | ☐ |

## What you can test now

The API at `http://localhost:5188` does:

- **Exact-match spellcheck** against 133k canonical lemmas + 409k headword variants
- **Levenshtein suggestions** with prefix pre-filter (200–500 candidates, top-k by edit distance)
- **Definitions** — returns the original dictionary entries (rawHtml) from up to 181 sources
- **Random lemma** — handy for browsing

Known limitations (will be fixed by later stages):
- No frequency ranking — suggestions are pure edit-distance. Stage 5 adds corpus stats.
- Definition HTML is unparsed (raw from source dictionaries). Stage 3 will extract `qdefinition`, `qetymology`, `qexample` rows.
- Morphology is rule-based (covers ~80% of common inflections). No verb conjugation yet (needs POS classification from Stage 1).

Morphology now resolves these correctly:
- `балалар` → `бала` (plural) · `балаға` → `бала` (dative) · `кітабым` → `кітап` (possessive 1sg with stem alternation) · `сөздіктерден` → `сөздік` (plural+ablative).

## Quick start

```bash
git clone https://github.com/Tako0502/qType
cd qType

# 1. Drop your MySQL password into a *.Local.json override (gitignored)
cat > src/QType.Importer/appsettings.Local.json <<'EOF'
{ "Site": { "ConnectionString": "Server=127.0.0.1;Port=3306;Database=qtype_db;User Id=root;Password=YOUR_PASSWORD;AllowLoadLocalInfile=true;DefaultCommandTimeout=600;" } }
EOF
cat > src/QType.Web/appsettings.Local.json <<'EOF'
{ "Site": { "ConnectionString": "Server=127.0.0.1;Port=3306;Database=qtype_db;User Id=root;Password=YOUR_PASSWORD;" } }
EOF

# 2. DB schema + sources (sources via .NET importer to avoid double-encoding)
mysql -uroot -p --default-character-set=utf8mb4 < db/001_schema.sql
dotnet run --project src/QType.Importer -- sources

# 2. Load 379k dictionary entries (≈ 20 sec)
dotnet run --project src/QType.Importer -- entries

# 3. Extract lemmas (≈ 6 sec)
dotnet run --project src/QType.Importer -- lemmas

# 4. Generate inflected forms via rule-based Kazakh morphology (≈ 40 sec, ~2.2M rows)
dotnet run --project src/QType.Importer -- morph

# 5. Start the API + demo frontend
dotnet run --project src/QType.Web -c Release
# open http://localhost:5188
```

Defaults pull `sq_items.json` / `sq_source.json` from `~/Downloads/`. Change paths in `src/QType.Importer/appsettings.json`.

## Manual test calls

```bash
BASE="http://localhost:5188"

# stats — should show ~379k entries, ~133k lemmas
curl -s $BASE/api/stats | jq

# spellcheck a real Kazakh word
curl -s -G $BASE/api/spellcheck --data-urlencode 'word=сөздік' | jq
# → valid: true, lemmaId: 100253, 6 entries

# spellcheck a typo
curl -s -G $BASE/api/spellcheck --data-urlencode 'word=сөздк' | jq
# → valid: false

# suggest corrections for that typo
curl -s -G $BASE/api/suggest --data-urlencode 'word=сөздк' --data-urlencode 'k=5' | jq
# → top suggestion "сөздік" with distance 1

# get full definitions (original HTML from up to 25 source dictionaries)
curl -s -G $BASE/api/define --data-urlencode 'word=абад' | jq

# random lemma (for browsing)
curl -s $BASE/api/random | jq
```

## Project layout

```
db/
  001_schema.sql              # 3-tier schema, MySQL 8+, utf8mb4_0900_ai_ci
  002_seed_sources.sql        # 181 dictionary sources

docs/
  SCHEMA.md                   # table reference
  PIPELINE.md                 # 6-stage processing plan

src/
  QType.COMMON/               # QSingleton, TextNormalizer (Kazakh-aware)
  QType.MODEL/                # 13 POCOs, one per table ([Table("qx")] attrs)
  QType.DBHelper/             # connection, Dapper SimpleCRUD bootstrap, BulkInsert
  QType.Importer/             # console: sources | entries | lemmas | all (Stage 0+2)
  QType.Web/                  # minimal ASP.NET API (port 5188)
```

## Use it from JavaScript / TypeScript

```bash
npm install @smoker_winston/qtype
```

```ts
import { QType } from '@smoker_winston/qtype';

const qt = new QType({ baseUrl: 'http://localhost:5188' });

await qt.isKazakhWord('сөздіктерден');          // → true (pl + ablative)
await qt.lemmatize('кітабым');                   // → 'кітап'
const { suggestions } = await qt.suggest('сөздк', 5);
const def = await qt.define('балалар');
```

Full SDK docs: [sdk/README.md](sdk/README.md).

## Use it from any AI client (MCP)

qType ships an MCP server so Claude Desktop, Claude Code, Cursor, Zed, and other MCP clients can call its tools directly. Add this to your client's MCP config:

```json
{
  "mcpServers": {
    "qtype": {
      "command": "npx",
      "args": ["-y", "@smoker_winston/qtype-mcp"]
    }
  }
}
```

For Claude Code: `claude mcp add qtype -- npx -y @smoker_winston/qtype-mcp`.

The AI then has these tools available: `qtype_spellcheck`, `qtype_suggest`, `qtype_define`, `qtype_lemmatize`, `qtype_random_word`, `qtype_stats`. Full MCP setup docs: [mcp/README.md](mcp/README.md).

## Stack

.NET 10 · MySQL 8 · Dapper · Dapper.SimpleCRUD · MySqlConnector · ASP.NET Core Minimal API · TypeScript SDK

## Next step recommendations

The single highest-leverage move is **Stage 4 — morphology wiring** (apertium-kaz). Without it the autocorrect can't handle inflected forms, which is most of real-world Kazakh typing. Stage 3 (HTML parsing) is the second priority — it turns the raw entries into structured `qdefinition` / `qetymology` / `qexample` rows that the API can return in clean JSON instead of HTML blobs.

See `docs/PIPELINE.md` for the full plan.
