-- qType — Kazakh language platform
-- Schema v1: three tiers (raw / parsed / derived)
-- MySQL 8+, utf8mb4_0900_ai_ci, InnoDB
-- Run order: 001_schema.sql → 002_seed_sources.sql → importer (Stage 0) → later stages

CREATE DATABASE IF NOT EXISTS qtype_db CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE qtype_db;

-- =============================================================
-- Tier 1 — Raw (immutable, mirrors sq_items.json / sq_source.json)
-- =============================================================

DROP TABLE IF EXISTS qheadword;
DROP TABLE IF EXISTS qentry;
DROP TABLE IF EXISTS qsource;

CREATE TABLE qsource (
  id INT UNSIGNED NOT NULL,
  title VARCHAR(512) NOT NULL DEFAULT '',
  addTime INT NOT NULL DEFAULT 0,
  qStatus TINYINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY idx_qsource_status (qStatus)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Dictionary sources from sq_source.json';

CREATE TABLE qentry (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  rawIndex INT UNSIGNED NOT NULL,
  rawHeadword VARCHAR(512) NOT NULL,
  rawHtml LONGTEXT NOT NULL,
  sourceId INT UNSIGNED NOT NULL DEFAULT 0,
  entryDate DATETIME NULL,
  addTime INT NOT NULL DEFAULT 0,
  qStatus TINYINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  UNIQUE KEY idx_qentry_rawindex (rawIndex),
  KEY idx_qentry_source (sourceId),
  KEY idx_qentry_headword_prefix (rawHeadword(64))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='One row per JSON item in sq_items.json (immutable)';

CREATE TABLE qheadword (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  entryId INT UNSIGNED NOT NULL,
  text VARCHAR(256) NOT NULL,
  textNormalized VARCHAR(256) NOT NULL,
  variantIndex TINYINT UNSIGNED NOT NULL DEFAULT 0,
  charCount SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  isPhrase TINYINT UNSIGNED NOT NULL DEFAULT 0,
  isDialectal TINYINT UNSIGNED NOT NULL DEFAULT 0,
  addTime INT NOT NULL DEFAULT 0,
  qStatus TINYINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY idx_qheadword_text (textNormalized),
  KEY idx_qheadword_entry (entryId),
  KEY idx_qheadword_phrase_status (isPhrase, qStatus),
  FULLTEXT KEY ft_qheadword_text (text)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Single-form headwords split from variant lists (e.g. "ЖАЙ, ЖӘЙ" → 2 rows)';

-- =============================================================
-- Tier 2 — Parsed (lemma graph, populated by Stages 2–3)
-- =============================================================

DROP TABLE IF EXISTS qphrase;
DROP TABLE IF EXISTS qexample;
DROP TABLE IF EXISTS qetymology;
DROP TABLE IF EXISTS qdefinition;
DROP TABLE IF EXISTS qlemmaentry;
DROP TABLE IF EXISTS qlemma;

CREATE TABLE qlemma (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  text VARCHAR(128) NOT NULL,
  pos VARCHAR(16) NOT NULL DEFAULT '',
  isPhrase TINYINT UNSIGNED NOT NULL DEFAULT 0,
  isDialectal TINYINT UNSIGNED NOT NULL DEFAULT 0,
  isLoanword TINYINT UNSIGNED NOT NULL DEFAULT 0,
  latinTranslit VARCHAR(256) NOT NULL DEFAULT '',
  notes VARCHAR(255) NOT NULL DEFAULT '',
  addTime INT NOT NULL DEFAULT 0,
  updateTime INT NOT NULL DEFAULT 0,
  qStatus TINYINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  UNIQUE KEY idx_qlemma_text (text),
  KEY idx_qlemma_pos (pos),
  KEY idx_qlemma_phrase (isPhrase, qStatus)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Canonical base forms (one per word, regardless of how many dictionaries describe it)';

CREATE TABLE qlemmaentry (
  lemmaId INT UNSIGNED NOT NULL,
  entryId INT UNSIGNED NOT NULL,
  weight FLOAT NOT NULL DEFAULT 1.0,
  addTime INT NOT NULL DEFAULT 0,
  PRIMARY KEY (lemmaId, entryId),
  KEY idx_qlemmaentry_entry (entryId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Many-to-many: a lemma is described by N raw dictionary entries';

CREATE TABLE qdefinition (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  lemmaId INT UNSIGNED NOT NULL,
  lang CHAR(2) NOT NULL DEFAULT 'kk',
  senseNumber TINYINT UNSIGNED NOT NULL DEFAULT 1,
  text TEXT NOT NULL,
  sourceId INT UNSIGNED NOT NULL DEFAULT 0,
  entryId INT UNSIGNED NOT NULL DEFAULT 0,
  addTime INT NOT NULL DEFAULT 0,
  qStatus TINYINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY idx_qdefinition_lemma (lemmaId, lang, senseNumber),
  KEY idx_qdefinition_source (sourceId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Parsed definitions (multiple senses per lemma, multiple languages)';

CREATE TABLE qetymology (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  lemmaId INT UNSIGNED NOT NULL,
  originLang VARCHAR(8) NOT NULL DEFAULT '',
  originForm VARCHAR(128) NOT NULL DEFAULT '',
  transliteration VARCHAR(128) NOT NULL DEFAULT '',
  entryId INT UNSIGNED NOT NULL DEFAULT 0,
  addTime INT NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY idx_qetymology_lemma (lemmaId),
  KEY idx_qetymology_origin (originLang)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Etymology parsed from "(ар. ...)" / "(пар. ...)" patterns';

CREATE TABLE qexample (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  lemmaId INT UNSIGNED NOT NULL,
  sentence TEXT NOT NULL,
  citation VARCHAR(512) NOT NULL DEFAULT '',
  sourceId INT UNSIGNED NOT NULL DEFAULT 0,
  entryId INT UNSIGNED NOT NULL DEFAULT 0,
  addTime INT NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY idx_qexample_lemma (lemmaId),
  KEY idx_qexample_source (sourceId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Usage examples with citations';

CREATE TABLE qphrase (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  headLemmaId INT UNSIGNED NOT NULL,
  text VARCHAR(256) NOT NULL,
  definition TEXT NOT NULL,
  sourceId INT UNSIGNED NOT NULL DEFAULT 0,
  entryId INT UNSIGNED NOT NULL DEFAULT 0,
  addTime INT NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY idx_qphrase_head (headLemmaId),
  KEY idx_qphrase_text (text(64))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Idioms and multi-word expressions, anchored to a head lemma';

-- =============================================================
-- Tier 3 — Derived (morphology + corpus stats, populated by Stages 4–5)
-- =============================================================

DROP TABLE IF EXISTS qmisspelling;
DROP TABLE IF EXISTS qfrequency;
DROP TABLE IF EXISTS qwordform;

CREATE TABLE qwordform (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  lemmaId INT UNSIGNED NOT NULL,
  form VARCHAR(128) NOT NULL,
  features JSON NULL,
  origin VARCHAR(16) NOT NULL DEFAULT 'apertium',
  addTime INT NOT NULL DEFAULT 0,
  qStatus TINYINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY idx_qwordform_form (form),
  KEY idx_qwordform_lemma (lemmaId),
  UNIQUE KEY idx_qwordform_unique (lemmaId, form)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Inflected forms generated by morphological analyzer (apertium-kaz)';

CREATE TABLE qfrequency (
  form VARCHAR(128) NOT NULL,
  count INT UNSIGNED NOT NULL DEFAULT 0,
  source VARCHAR(32) NOT NULL DEFAULT 'web',
  updateTime INT NOT NULL DEFAULT 0,
  PRIMARY KEY (form, source),
  KEY idx_qfrequency_count (count)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Corpus-derived word frequency (drives autocorrect ranking)';

CREATE TABLE qmisspelling (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  typed VARCHAR(128) NOT NULL,
  lemmaId INT UNSIGNED NOT NULL,
  weight FLOAT NOT NULL DEFAULT 1.0,
  origin VARCHAR(16) NOT NULL DEFAULT 'manual',
  addTime INT NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY idx_qmisspelling_typed (typed),
  KEY idx_qmisspelling_lemma (lemmaId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Known typos → lemma mapping (manual entries + learned from user corrections)';

-- =============================================================
-- Pipeline state (so stages can resume / report progress)
-- =============================================================

DROP TABLE IF EXISTS qpipelinerun;
CREATE TABLE qpipelinerun (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  stage VARCHAR(32) NOT NULL,
  startedAt INT NOT NULL,
  finishedAt INT NOT NULL DEFAULT 0,
  rowsProcessed INT UNSIGNED NOT NULL DEFAULT 0,
  errorCount INT UNSIGNED NOT NULL DEFAULT 0,
  notes TEXT NOT NULL,
  qStatus TINYINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY idx_qpipelinerun_stage (stage, startedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Audit trail for each pipeline stage run';
