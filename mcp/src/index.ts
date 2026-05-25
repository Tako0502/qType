#!/usr/bin/env node
/**
 * qType MCP server — exposes Kazakh language tools to any MCP-aware AI client.
 *
 * Tools:
 *   qtype_spellcheck   — is X a valid Kazakh word?
 *   qtype_suggest      — top-k corrections for a typo
 *   qtype_define       — dictionary definitions (resolves inflected forms to lemma)
 *   qtype_lemmatize    — reduce inflected form to canonical lemma
 *   qtype_random_word  — random Kazakh lemma
 *   qtype_stats        — corpus statistics
 *
 * Transport: stdio. Configure backend via QTYPE_BASE_URL (default http://localhost:5188).
 */

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { QType, QTypeError } from "@smoker_winston/qtype";
import { z } from "zod";

const VERSION = "0.1.0";
const SERVER_NAME = "qtype-mcp-server";
const DEFAULT_BASE = "http://localhost:5188";
const CHARACTER_LIMIT = 20_000;

enum ResponseFormat {
  MARKDOWN = "markdown",
  JSON = "json",
}

const formatField = z.nativeEnum(ResponseFormat)
  .default(ResponseFormat.MARKDOWN)
  .describe("'markdown' (human-readable) or 'json' (structured machine-readable). Default: markdown.");

// ─────────────────────────────────────────────────────────────────────
//                            Setup
// ─────────────────────────────────────────────────────────────────────

const baseUrl = process.env.QTYPE_BASE_URL ?? DEFAULT_BASE;
const qt = new QType({ baseUrl });

const server = new McpServer({
  name: SERVER_NAME,
  version: VERSION,
});

function truncate(text: string): { text: string; truncated: boolean } {
  if (text.length <= CHARACTER_LIMIT) return { text, truncated: false };
  return {
    text:
      text.slice(0, CHARACTER_LIMIT) +
      `\n\n…[truncated — original was ${text.length} chars, limit ${CHARACTER_LIMIT}]`,
    truncated: true,
  };
}

type ToolResponse = {
  content: Array<{ type: "text"; text: string }>;
  structuredContent?: Record<string, unknown>;
};

function ok<T extends object>(text: string, data?: T): ToolResponse {
  return {
    content: [{ type: "text", text }],
    ...(data ? { structuredContent: { ...data } as Record<string, unknown> } : {}),
  };
}

function fail(message: string): ToolResponse {
  return { content: [{ type: "text", text: message }] };
}

function handleError(err: unknown, hint?: string): string {
  if (err instanceof QTypeError) {
    if (err.status === 0 || err.status === undefined) {
      return `Error: cannot reach qType backend at ${baseUrl}. Is the server running? Set QTYPE_BASE_URL env var to override.${hint ? "\n" + hint : ""}`;
    }
    return `Error: qType API returned ${err.status}: ${err.body ?? err.message}`;
  }
  return `Error: ${err instanceof Error ? err.message : String(err)}`;
}

// Strip HTML to a readable plain-text approximation for markdown output.
function htmlToReadable(html: string): string {
  return html
    .replace(/<\/?p[^>]*>/gi, "\n\n")
    .replace(/<\/?(i|em)[^>]*>/gi, "_")
    .replace(/<\/?(b|strong)[^>]*>/gi, "**")
    .replace(/<br\s*\/?>/gi, "\n")
    .replace(/<[^>]+>/g, "")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

// ─────────────────────────────────────────────────────────────────────
//                            Tools
// ─────────────────────────────────────────────────────────────────────

// ── 1. spellcheck ─────────────────────────────────────────────────────
const SpellcheckInput = z.object({
  word: z.string().min(1).max(128).describe("A Kazakh word to validate. Inflected forms are resolved against the morphology table."),
  response_format: formatField,
}).strict();

server.registerTool(
  "qtype_spellcheck",
  {
    title: "Validate a Kazakh word",
    description: `Check whether a word is a valid Kazakh word.

The check is morphology-aware: inflected forms like "балалар" (children), "кітабым" (my book), "сөздіктерден" (from the dictionaries) all return valid=true and resolve to their canonical lemma. Returns the canonical lemma, morphological tag (e.g. "pl", "dat", "poss1sg+alt", "pl+abl"), and how the match was found ("lemma" for direct lookup, "wordform" via generated inflected forms, "none" if not in the dictionary).

Use when:
  - User asks "is X a Kazakh word?"
  - You need to validate Kazakh text before processing
  - You want to find the dictionary form of an inflected word

Args:
  - word (string): the word to check (1-128 chars)
  - response_format ('markdown' | 'json'): default 'markdown'

Returns (json schema):
  {
    "word": string,           // original input
    "normalized": string,     // lowercased, whitespace-collapsed
    "valid": boolean,
    "via": "lemma" | "wordform" | "none",
    "lemma": string,          // canonical form (empty when via='none')
    "lemmaId": number,
    "tag": string,            // e.g. "pl", "dat", "poss1sg+alt"
    "headwordHits": number,
    "isPhrase": boolean
  }`,
    inputSchema: SpellcheckInput.shape,
    annotations: {
      readOnlyHint: true,
      destructiveHint: false,
      idempotentHint: true,
      openWorldHint: true,
    },
  },
  async (input) => {
    try {
      const params = SpellcheckInput.parse(input);
      const r = await qt.spellcheck(params.word);

      if (params.response_format === ResponseFormat.JSON) {
        return ok(JSON.stringify(r, null, 2), r);
      }
      const lines: string[] = [];
      if (r.valid) {
        if (r.via === "lemma") {
          lines.push(`✓ **${r.normalized}** is a valid Kazakh lemma`);
          if (r.headwordHits > 0) lines.push(`Found in ${r.headwordHits} headword entr${r.headwordHits === 1 ? "y" : "ies"}.`);
        } else {
          lines.push(`✓ **${r.normalized}** is a valid Kazakh inflected form`);
          lines.push(`Canonical lemma: **${r.lemma}** (id ${r.lemmaId})`);
          if (r.tag) lines.push(`Morphological tag: \`${r.tag}\``);
        }
      } else {
        lines.push(`✗ **${r.normalized}** is not in the dictionary`);
        lines.push(`Use qtype_suggest to get correction candidates.`);
      }
      return ok(lines.join("\n"), r);
    } catch (err) {
      return fail(handleError(err));
    }
  },
);

// ── 2. suggest ────────────────────────────────────────────────────────
const SuggestInput = z.object({
  word: z.string().min(1).max(128).describe("The misspelled or partial word."),
  k: z.number().int().min(1).max(25).default(5).describe("How many suggestions to return (1-25)."),
  response_format: formatField,
}).strict();

server.registerTool(
  "qtype_suggest",
  {
    title: "Suggest corrections for a Kazakh word",
    description: `Get top-K spelling corrections for a (possibly misspelled) Kazakh word.

The candidate pool includes both canonical lemmas AND generated inflected forms; results are deduplicated by lemma so you won't get five inflected forms of the same word. Each candidate has a Levenshtein \`distance\` (lower = closer to the input) and a \`source\` flag indicating whether it was found as a base lemma or via inflection.

Use when:
  - The user typed an apparent typo (e.g. "сөздк") and you want corrections
  - You want autocomplete-style suggestions for a partial prefix
  - You need to find the closest Kazakh word to a foreign or misspelled input

Args:
  - word (string): the word to find corrections for (1-128 chars)
  - k (number): how many results, 1-25 (default 5)
  - response_format ('markdown' | 'json'): default 'markdown'

Returns (json schema):
  {
    "word": string,
    "normalized": string,
    "suggestions": [
      {
        "form": string,       // the form that matched (may be inflected)
        "lemma": string,      // canonical lemma the form belongs to
        "lemmaId": number,
        "distance": number,   // Levenshtein distance to input
        "source": "lemma" | "form"
      }
    ]
  }`,
    inputSchema: SuggestInput.shape,
    annotations: {
      readOnlyHint: true,
      destructiveHint: false,
      idempotentHint: true,
      openWorldHint: true,
    },
  },
  async (input) => {
    try {
      const params = SuggestInput.parse(input);
      const r = await qt.suggest(params.word, params.k);

      if (params.response_format === ResponseFormat.JSON) {
        return ok(JSON.stringify(r, null, 2), r);
      }
      if (r.suggestions.length === 0) {
        return ok(`No suggestions found for "${r.normalized}".`, r);
      }
      const lines = [`# Suggestions for "${r.normalized}"`, ""];
      for (const s of r.suggestions) {
        const tail = s.source === "form" && s.form !== s.lemma ? ` (form of **${s.lemma}**)` : "";
        lines.push(`- **${s.form}** · distance ${s.distance}${tail}`);
      }
      return ok(lines.join("\n"), r);
    } catch (err) {
      return fail(handleError(err));
    }
  },
);

// ── 3. define ─────────────────────────────────────────────────────────
const DefineInput = z.object({
  word: z.string().min(1).max(128).describe("The Kazakh word to define. Can be a lemma or any inflected form."),
  max_entries: z.number().int().min(1).max(25).default(10).describe("How many source-dictionary entries to return (1-25). Default 10."),
  response_format: formatField,
}).strict();

server.registerTool(
  "qtype_define",
  {
    title: "Get dictionary definitions for a Kazakh word",
    description: `Look up dictionary definitions for a Kazakh word across 181 source dictionaries. Inflected forms are automatically resolved to their canonical lemma before lookup.

Returns the canonical lemma, the morphological tag of the input (if it was an inflected form), and up to N source-dictionary entries. Each entry has a source title, the dictionary year, and the raw definition body. In markdown mode the HTML is converted to readable text; in json mode it's kept as raw HTML for downstream processing.

Use when:
  - User asks "what does X mean in Kazakh?"
  - You need etymology, usage examples, or dialectal info
  - You want to validate a translation by checking the source meaning

Args:
  - word (string): the word to define (1-128 chars)
  - max_entries (number): how many entries to return, 1-25 (default 10)
  - response_format ('markdown' | 'json'): default 'markdown'

Returns (json schema):
  {
    "word": string,
    "normalized": string,
    "lemmaId": number | null,
    "lemma": string,
    "isPhrase": boolean,
    "isDialectal": boolean,
    "matchedVia": "lemma" | "wordform" | "none",
    "tag": string,            // morphological tag if matched via inflection
    "entries": [
      {
        "entryId": number,
        "sourceId": number,
        "sourceTitle": string,
        "rawHtml": string,    // original dictionary HTML
        "entryDate": string   // YYYY-MM-DD
      }
    ]
  }`,
    inputSchema: DefineInput.shape,
    annotations: {
      readOnlyHint: true,
      destructiveHint: false,
      idempotentHint: true,
      openWorldHint: true,
    },
  },
  async (input) => {
    try {
      const params = DefineInput.parse(input);
      const r = await qt.define(params.word);
      const limitedEntries = r.entries.slice(0, params.max_entries);

      const out = { ...r, entries: limitedEntries };
      if (params.response_format === ResponseFormat.JSON) {
        const json = JSON.stringify(out, null, 2);
        const tr = truncate(json);
        return ok(tr.text, out);
      }

      if (r.matchedVia === "none" || limitedEntries.length === 0) {
        return ok(`No definitions found for "${r.normalized}". Try qtype_suggest to find similar words.`, out);
      }

      const lines: string[] = [];
      lines.push(`# ${r.lemma}`);
      if (r.matchedVia === "wordform") {
        lines.push(`_You queried "${r.normalized}" which is an inflected form of **${r.lemma}**${r.tag ? ` (tag \`${r.tag}\`)` : ""}._`);
      }
      if (r.isPhrase) lines.push(`_(phrase / idiom)_`);
      if (r.isDialectal) lines.push(`_(dialectal)_`);
      lines.push(`\nFound in ${limitedEntries.length}${r.entries.length > limitedEntries.length ? ` of ${r.entries.length}` : ""} source dictionar${limitedEntries.length === 1 ? "y" : "ies"}.\n`);

      for (const [i, e] of limitedEntries.entries()) {
        lines.push(`## ${i + 1}. ${e.sourceTitle || "Unknown source"}`);
        if (e.entryDate) lines.push(`_Entry date: ${e.entryDate}_`);
        lines.push("");
        lines.push(htmlToReadable(e.rawHtml));
        lines.push("");
      }
      const tr = truncate(lines.join("\n"));
      return ok(tr.text, out);
    } catch (err) {
      return fail(handleError(err));
    }
  },
);

// ── 4. lemmatize ──────────────────────────────────────────────────────
const LemmatizeInput = z.object({
  word: z.string().min(1).max(128).describe("Inflected (or already-base) Kazakh form."),
}).strict();

server.registerTool(
  "qtype_lemmatize",
  {
    title: "Reduce a Kazakh word to its canonical lemma",
    description: `Return the canonical (dictionary base) form of a Kazakh word. If the input is already a lemma, returns itself. If it's an inflected form (e.g. "балаларымыз" = "our children"), returns the base ("бала"). Returns null if the word is unknown.

This is a thin convenience wrapper around qtype_spellcheck — use this when you just want the lemma string and don't care about morphological tags.

Args:
  - word (string): the form to lemmatize

Returns (json schema):
  {
    "word": string,           // original input
    "lemma": string | null    // canonical form, or null if unknown
  }`,
    inputSchema: LemmatizeInput.shape,
    annotations: {
      readOnlyHint: true,
      destructiveHint: false,
      idempotentHint: true,
      openWorldHint: true,
    },
  },
  async (input) => {
    try {
      const { word } = LemmatizeInput.parse(input);
      const lemma = await qt.lemmatize(word);
      const out = { word, lemma };
      return ok(lemma == null ? `"${word}" is not a known Kazakh word.` : `${word} → ${lemma}`, out);
    } catch (err) {
      return fail(handleError(err));
    }
  },
);

// ── 5. random_word ────────────────────────────────────────────────────
const RandomInput = z.object({}).strict();

server.registerTool(
  "qtype_random_word",
  {
    title: "Get a random Kazakh lemma",
    description: `Return a random Kazakh lemma from the platform's 133k canonical lemmas. Useful for prompts, word-of-the-day features, exploration, or sampling the corpus.

Args: (none)

Returns:
  {
    "id": number,
    "text": string
  }`,
    inputSchema: RandomInput.shape,
    annotations: {
      readOnlyHint: true,
      destructiveHint: false,
      idempotentHint: false,
      openWorldHint: true,
    },
  },
  async () => {
    try {
      const r = await qt.random();
      return ok(`Random lemma: **${r.text}** (id ${r.id})`, r);
    } catch (err) {
      return fail(handleError(err));
    }
  },
);

// ── 6. stats ──────────────────────────────────────────────────────────
const StatsInput = z.object({}).strict();

server.registerTool(
  "qtype_stats",
  {
    title: "qType corpus statistics",
    description: `Return counts of entries, headwords, lemmas, phrases, sources, and inflected wordforms in the qType corpus.

Args: (none)

Returns:
  {
    "entries": number,        // raw dictionary entries
    "headwords": number,
    "lemmas": number,         // canonical base forms
    "phrases": number,
    "sources": number,        // contributing dictionaries
    "wordforms": number       // generated inflected forms
  }`,
    inputSchema: StatsInput.shape,
    annotations: {
      readOnlyHint: true,
      destructiveHint: false,
      idempotentHint: true,
      openWorldHint: true,
    },
  },
  async () => {
    try {
      const r = (await qt.stats()) as unknown as Record<string, number>;
      const lines = [
        `# qType corpus stats`,
        `- Source dictionaries: **${r.sources.toLocaleString()}**`,
        `- Raw entries:         **${r.entries.toLocaleString()}**`,
        `- Headwords:           **${r.headwords.toLocaleString()}**`,
        `- Canonical lemmas:    **${r.lemmas.toLocaleString()}**`,
        `- Phrases / idioms:    **${r.phrases.toLocaleString()}**`,
        `- Inflected wordforms: **${r.wordforms.toLocaleString()}**`,
      ];
      return ok(lines.join("\n"), r);
    } catch (err) {
      return fail(handleError(err));
    }
  },
);

// ─────────────────────────────────────────────────────────────────────
//                            Entry point
// ─────────────────────────────────────────────────────────────────────

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  // Use stderr — stdout is the MCP protocol channel
  console.error(`${SERVER_NAME} v${VERSION} listening on stdio (backend: ${baseUrl})`);
}

main().catch((err) => {
  console.error("Fatal:", err);
  process.exit(1);
});
