# @smoker_winston/qtype-mcp

MCP (Model Context Protocol) server for **qType** — gives any MCP-aware AI client (Claude Desktop, Claude Code, Cursor, Zed, …) inflection-aware Kazakh spellcheck, autocorrect, lemmatization, and dictionary lookup over 379k dictionary entries and 2.2M generated word forms.

## What an AI gets

| Tool | Purpose |
|---|---|
| `qtype_spellcheck` | Is X a valid Kazakh word? Resolves inflected forms to their lemma + tag. |
| `qtype_suggest` | Top-K spelling corrections, deduped by lemma. |
| `qtype_define` | Definitions across 181 source dictionaries; auto-resolves inflected forms. |
| `qtype_lemmatize` | Reduce any form to its canonical base. |
| `qtype_random_word` | Random Kazakh lemma — handy for prompts. |
| `qtype_stats` | Corpus counts. |

The AI can ask any of these in natural language, e.g. _"Is `сөздіктерден` a Kazakh word?"_ → tool routes through the morphology layer and returns "yes, plural+ablative of сөздік".

## Requirements

A running [qType backend](https://github.com/Tako0502/qType) at `http://localhost:5188` (default) or anywhere reachable. Set `QTYPE_BASE_URL` env var to override.

## Install

You don't need to install it directly — point your MCP client at it via `npx`. The package will be fetched automatically on first launch.

### Claude Desktop

Edit `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows):

```json
{
  "mcpServers": {
    "qtype": {
      "command": "npx",
      "args": ["-y", "@smoker_winston/qtype-mcp"],
      "env": {
        "QTYPE_BASE_URL": "http://localhost:5188"
      }
    }
  }
}
```

Restart Claude Desktop. The 6 tools appear in the tool picker.

### Claude Code

```bash
claude mcp add qtype -- npx -y @smoker_winston/qtype-mcp
```

Or edit `~/.claude.json` directly:

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

### Cursor

`Settings → MCP → Add new MCP Server`:

```json
{
  "qtype": {
    "command": "npx",
    "args": ["-y", "@smoker_winston/qtype-mcp"]
  }
}
```

### Zed

`~/.config/zed/settings.json`:

```json
{
  "context_servers": {
    "qtype": {
      "command": {
        "path": "npx",
        "args": ["-y", "@smoker_winston/qtype-mcp"]
      }
    }
  }
}
```

## Try it

In any MCP-connected chat:

> _"Is `балаларымыздың` a valid Kazakh word?"_

The AI calls `qtype_spellcheck`, gets back `{ valid: true, lemma: "бала", tag: "pl+poss1pl+gen" }`, and answers in plain language.

> _"What does `сөздіктерден` mean?"_

The AI calls `qtype_define`, gets the canonical lemma `сөздік` plus definitions from the synonyms dictionary, terminology dictionary, and literary-language dictionary, and summarizes.

## Environment variables

| Var | Default | Purpose |
|---|---|---|
| `QTYPE_BASE_URL` | `http://localhost:5188` | URL of the qType REST API. |

## Local development

```bash
git clone https://github.com/Tako0502/qType
cd qType/mcp
npm install
npm run build
npm run inspect       # opens MCP Inspector UI for hands-on testing
```

## Errors

If the backend is unreachable, every tool returns a clear actionable error pointing at `QTYPE_BASE_URL`. To start the backend:

```bash
cd qType
dotnet run --project src/QType.Web -c Release
```

See the [main README](https://github.com/Tako0502/qType) for full backend setup.

## License

MIT — see [LICENSE](./LICENSE).
