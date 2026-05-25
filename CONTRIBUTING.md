# Contributing to qType

Thanks for considering a contribution. qType is a small project with a focused goal: make Kazakh language processing genuinely useful for app developers, in production, today.

Below are the contribution paths ranked by impact + ease.

## What needs help most

### 1. Morphology rules (high impact, easy entry)
The Kazakh morphology engine in `src/QType.COMMON/Morphology/` covers ~70% of common inflection by rules alone. Concrete wins:
- **Irregular verb stems** — vowel-deletion verbs like `оқу` (stem `оқы`-), `ілу` (stem `іле`-), `тұру` (stem `тұра`-) generate slightly wrong forms today. A hand-curated `KazakhIrregularVerbs.cs` mapping ~200 lemmas to their real stems would fix this immediately. See the comment block at the top of `KazakhVerb.cs`.
- **Voice morphology** — passive (`-ыл/-іл`), causative (`-дыр/-дір`, `-т`), reflexive (`-ын/-ін`) are not generated. Adding them roughly doubles the wordform table.
- **Stem alternation lexicon** — for nouns where п↔б, к↔г, қ↔ғ alternation is lexically governed (not all stems alternate), a list of which lemmas actually alternate would let us emit only correct forms instead of both variants.

Test before submitting: `dotnet test tests/QType.Tests/`. Every new rule should land with at least one xUnit test case.

### 2. Dictionary sources
Add new sources to `qsource` and ingest them via an importer extension. Useful additions:
- **Free / public-domain dictionaries** — older Kazakh dictionaries past copyright; government-published glossaries; Wiktionary kk.
- **Domain-specific** — medical, legal, IT terminology.

If you have explicit permission from a rights-holder, attach the permission letter to the PR. Without permission, we cannot accept the source.

### 3. Test coverage
Tests live in `tests/QType.Tests/`. We use xUnit `[Theory]` + `[InlineData]` for table-driven tests. Add a case whenever you find a Kazakh word that the engine handles wrong.

### 4. Frontend / UX
The demo at `src/QType.Web/wwwroot/index.html` is English-only and missing keyboard navigation, history, copy-to-clipboard, and a Kazakh UI string. All easy improvements.

### 5. SDK / MCP integrations
The SDK (`sdk/`) and MCP server (`mcp/`) are zero-dependency TypeScript. Bug fixes and new convenience methods welcome. Don't add runtime dependencies without discussion — leanness is a feature.

## How to contribute

1. **Open an issue first** for anything beyond a one-line fix. Saves you from writing the wrong thing.
2. **Fork** → branch → PR.
3. **Tests must pass**: `dotnet test tests/QType.Tests/`. CI will block otherwise.
4. **Match the existing patterns**: no new abstractions unless justified, minimal changes, no unrelated refactors.
5. **Sign your commits**: `git commit -s` if your workflow uses DCO.

## What gets rejected

- Adding runtime dependencies to the SDK or MCP server without justification
- Disabling existing tests instead of fixing them
- Wholesale refactors that don't fix a stated bug
- Copyright-questionable dictionary content without permission letters
- Changes that break the 3-tier schema isolation (each tier should be re-runnable independently)

## Local development

```bash
git clone https://github.com/Tako0502/qType
cd qType

# 1. set MySQL password in src/QType.Importer/appsettings.Local.json
mysql -uroot -p < db/001_schema.sql
dotnet run --project src/QType.Importer -- all   # ~ 1 minute end-to-end

# 2. run the web API + open the demo
dotnet run --project src/QType.Web -c Release    # http://localhost:5188

# 3. test changes
dotnet test tests/QType.Tests/
```

## Code style

- C#: follow the existing conventions in `src/` (camelCase columns, lowercase table names, Dapper for data access, no FK constraints in DDL).
- TypeScript: ESM, strict mode, no `any`, prefer functions over classes when possible.
- Database: schema changes go in a new file `db/00N_*.sql`. Never edit existing migration files.

## Questions

Open a discussion at https://github.com/Tako0502/qType/discussions, or reach out at https://github.com/Tako0502.
