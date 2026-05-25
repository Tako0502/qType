// Moves the CJS build output (dist-cjs/index.js) to dist/index.cjs and cleans up.
import { mkdir, copyFile, rm, readdir } from 'node:fs/promises';
import { join } from 'node:path';

const cjsDir = new URL('../dist-cjs/', import.meta.url);
const dist = new URL('../dist/', import.meta.url);

try {
  await mkdir(dist, { recursive: true });
  const files = await readdir(cjsDir);
  for (const f of files) {
    if (f.endsWith('.js')) {
      const dest = join(dist.pathname, f.replace(/\.js$/, '.cjs'));
      await copyFile(join(cjsDir.pathname, f), dest);
    }
  }
  await rm(cjsDir, { recursive: true, force: true });
  console.log('CJS build placed into dist/*.cjs');
} catch (err) {
  console.error('rename-cjs failed:', err);
  process.exit(1);
}
