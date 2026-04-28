import type { SortMeta } from 'primeng/api';

export function tableSortToApiParam(order: number): 'asc' | 'desc' {
  return order === -1 ? 'desc' : 'asc';
}

/** Событие p-table (onSort): одинарная сортировка или multiSort. */
export function parseOnSortEvent(event: unknown): SortMeta | null {
  if (!event || typeof event !== 'object') return null;
  const e = event as Record<string, unknown>;
  if (typeof e['field'] === 'string' && (e['order'] === 1 || e['order'] === -1)) {
    return { field: e['field'] as string, order: e['order'] as number };
  }
  const m =
    (e['multisortmeta'] as SortMeta[] | undefined)?.[0] ??
    (e['multiSortMeta'] as SortMeta[] | undefined)?.[0];
  if (m?.field && (m.order === 1 || m.order === -1)) return m;
  return null;
}

/**
 * Тот же field+order, что уже в состоянии — повтор от p-table после смены [value] (см. ngOnChanges → sortSingle → onSort).
 * Тогда не вызывать refetch, иначе бесконечные запросы.
 */
export function isRedundantTableSort(
  meta: SortMeta,
  currentField: string | null,
  currentOrder: 1 | -1
): boolean {
  return (
    currentField != null &&
    currentField === meta.field &&
    currentOrder === (meta.order as 1 | -1)
  );
}
