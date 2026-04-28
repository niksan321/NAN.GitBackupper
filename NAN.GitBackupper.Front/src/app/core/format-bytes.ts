export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb >= 10 ? kb.toFixed(0) : kb.toFixed(1)} KB`;
  const mb = kb / 1024;
  if (mb < 1024) return `${mb >= 10 ? mb.toFixed(0) : mb.toFixed(1)} MB`;
  const gb = mb / 1024;
  return `${gb >= 10 ? gb.toFixed(0) : gb.toFixed(1)} GB`;
}
