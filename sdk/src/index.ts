/**
 * @smoker_winston/qtype — Kazakh language SDK
 *
 * Wraps the qType REST API (spellcheck, suggest, define, lemmatize) so you can
 * add Kazakh-aware lookup to any JS/TS project with one import.
 *
 * @example
 * ```ts
 * import { QType } from '@smoker_winston/qtype';
 *
 * const qt = new QType({ baseUrl: 'http://localhost:5188' });
 *
 * const r = await qt.spellcheck('сөздіктерден');
 * // → { valid: true, via: 'wordform', lemma: 'сөздік', tag: 'pl+abl', ... }
 *
 * const s = await qt.suggest('сөздк', 5);
 * // → suggestions: [{ form: 'сөздік', lemma: 'сөздік', distance: 1, ... }, ...]
 *
 * const def = await qt.define('кітабым');
 * // → { lemma: 'кітап', tag: 'poss1sg+alt', entries: [...] }
 * ```
 */

export interface QTypeOptions {
  /** Base URL of the qType server. Default: `http://localhost:5188`. */
  baseUrl?: string;
  /** Override the global `fetch`. Useful for testing or non-browser/Node runtimes. */
  fetch?: typeof fetch;
  /** Request timeout in ms. Default: `10000`. */
  timeoutMs?: number;
  /** Extra headers sent on every request. */
  headers?: Record<string, string>;
}

export interface SpellCheckResult {
  word: string;
  normalized: string;
  valid: boolean;
  /** How the match was found. */
  via: 'lemma' | 'wordform' | 'none';
  lemmaId: number;
  /** Canonical lemma the word resolves to (empty when `via === 'none'`). */
  lemma: string;
  /** The exact form that was matched (normalized input). */
  matchedForm: string;
  /** Morphological tag (e.g. `pl`, `dat`, `pl+abl`, `poss1sg+alt`) when matched via `wordform`. */
  tag: string;
  /** How many raw headword rows match the normalized input. */
  headwordHits: number;
  isPhrase: boolean;
}

export interface Suggestion {
  /** Concrete form that scored well against the user's input. */
  form: string;
  /** Canonical lemma this form belongs to. */
  lemma: string;
  lemmaId: number;
  /** Levenshtein edit distance from the user's input. */
  distance: number;
  /** Whether the candidate was discovered as a lemma directly, or via an inflected form. */
  source: 'lemma' | 'form';
}

export interface SuggestResult {
  word: string;
  normalized: string;
  suggestions: Suggestion[];
}

export interface EntryHit {
  entryId: number;
  sourceId: number;
  sourceTitle: string;
  /** Raw dictionary HTML — render with caution if the source is untrusted. */
  rawHtml: string;
  entryDate: string;
}

export interface DefineResult {
  word: string;
  normalized: string;
  lemmaId: number | null;
  lemma: string;
  isPhrase: boolean;
  isDialectal: boolean;
  matchedVia: 'lemma' | 'wordform' | 'none';
  tag: string;
  entries: EntryHit[];
}

export interface Stats {
  entries: number;
  headwords: number;
  lemmas: number;
  phrases: number;
  sources: number;
  wordforms: number;
}

export interface RandomLemma {
  id: number;
  text: string;
}

export class QTypeError extends Error {
  constructor(
    message: string,
    public readonly status?: number,
    public readonly body?: string,
  ) {
    super(message);
    this.name = 'QTypeError';
  }
}

const DEFAULT_BASE = 'http://localhost:5188';
const DEFAULT_TIMEOUT = 10_000;

export class QType {
  private readonly baseUrl: string;
  private readonly fetchImpl: typeof fetch;
  private readonly timeoutMs: number;
  private readonly headers: Record<string, string>;

  constructor(options: QTypeOptions = {}) {
    this.baseUrl = (options.baseUrl ?? DEFAULT_BASE).replace(/\/+$/, '');
    this.fetchImpl = options.fetch ?? globalThis.fetch;
    this.timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT;
    this.headers = options.headers ?? {};
    if (!this.fetchImpl) {
      throw new Error('No fetch implementation available. Pass options.fetch on Node < 18.');
    }
  }

  /** Liveness check. Returns `true` if the server is up. */
  async health(): Promise<boolean> {
    try {
      const r = await this.request<{ status: string }>('/health');
      return r.status === 'ok';
    } catch {
      return false;
    }
  }

  /** Counts: entries, headwords, lemmas, phrases, sources, wordforms. */
  stats(): Promise<Stats> {
    return this.request<Stats>('/api/stats');
  }

  /**
   * Is `word` a valid Kazakh word?
   * Resolves via the canonical lemma table OR the inflected-form table.
   */
  spellcheck(word: string): Promise<SpellCheckResult> {
    return this.request<SpellCheckResult>('/api/spellcheck', { word });
  }

  /**
   * Top-k corrections for a (possibly mis-typed) word.
   * Dedup'd by lemma — won't return 5 inflected forms of the same word.
   */
  suggest(word: string, k = 5): Promise<SuggestResult> {
    return this.request<SuggestResult>('/api/suggest', { word, k: String(k) });
  }

  /**
   * Look up dictionary entries for a word. If `word` is an inflected form,
   * resolves to the parent lemma and returns its entries.
   */
  define(word: string): Promise<DefineResult> {
    return this.request<DefineResult>('/api/define', { word });
  }

  /**
   * Convenience: returns the canonical lemma of `word`, or `null` if unknown.
   * (`'балалар'` → `'бала'`, `'кітабым'` → `'кітап'`.)
   */
  async lemmatize(word: string): Promise<string | null> {
    const r = await this.spellcheck(word);
    return r.valid ? r.lemma || r.normalized : null;
  }

  /**
   * Boolean wrapper around `spellcheck`. Convenient as a predicate.
   */
  async isKazakhWord(word: string): Promise<boolean> {
    const r = await this.spellcheck(word);
    return r.valid;
  }

  /** Random lemma — handy for testing or generating prompts. */
  random(): Promise<RandomLemma> {
    return this.request<RandomLemma>('/api/random');
  }

  // ──────────────────────────────────────────────────────────────

  private async request<T>(path: string, params?: Record<string, string>): Promise<T> {
    const url = new URL(this.baseUrl + path);
    if (params) {
      for (const [k, v] of Object.entries(params)) {
        if (v !== undefined && v !== null) url.searchParams.set(k, v);
      }
    }

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);
    try {
      const res = await this.fetchImpl(url.toString(), {
        signal: controller.signal,
        headers: { Accept: 'application/json', ...this.headers },
      });
      const text = await res.text();
      if (!res.ok) {
        throw new QTypeError(`qType API ${res.status}`, res.status, text);
      }
      return text ? (JSON.parse(text) as T) : ({} as T);
    } catch (err: unknown) {
      if (err instanceof QTypeError) throw err;
      if ((err as { name?: string })?.name === 'AbortError') {
        throw new QTypeError(`qType request timeout after ${this.timeoutMs}ms`);
      }
      throw new QTypeError(
        `qType request failed: ${(err as Error).message ?? String(err)}`,
      );
    } finally {
      clearTimeout(timer);
    }
  }
}

export default QType;
