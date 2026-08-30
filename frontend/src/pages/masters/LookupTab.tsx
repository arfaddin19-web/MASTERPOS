import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../../auth/AuthContext';
import { createCategory, createUnit, createWarehouse, listCategories, listUnits, listWarehouses } from '../../api/masters';
import { Banner, useBanner } from '../../components/Shared';

type Kind = 'Category' | 'Unit' | 'Warehouse';

/** Categories, Units, and Warehouses are all "a name (plus one optional
 * field) and nothing else" — one create-only management screen serves all
 * three rather than three near-identical files. Matches what the mockup's
 * own "+ quick-add" affordance implies: these are lightweight reference
 * lists, not full documents with an edit/delete lifecycle. */
export function LookupTab({ kind }: { kind: Kind }) {
  const queryClient = useQueryClient();
  const { session } = useAuth();
  const { banner, fail, succeed, clear } = useBanner();
  const [name, setName] = useState('');
  const [extra, setExtra] = useState(''); // short code (Unit) — unused for others
  const [isDefault, setIsDefault] = useState(false);

  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: listCategories, enabled: kind === 'Category' });
  const unitsQuery = useQuery({ queryKey: ['units'], queryFn: listUnits, enabled: kind === 'Unit' });
  const warehousesQuery = useQuery({ queryKey: ['warehouses'], queryFn: listWarehouses, enabled: kind === 'Warehouse' });

  const rows =
    kind === 'Category'
      ? (categoriesQuery.data ?? []).map((c) => ({ id: c.id, name: c.name, meta: '' }))
      : kind === 'Unit'
        ? (unitsQuery.data ?? []).map((u) => ({ id: u.id, name: u.name, meta: u.shortCode ?? '' }))
        : (warehousesQuery.data ?? []).map((w) => ({ id: w.id, name: w.name, meta: w.isDefault ? 'Default' : '' }));

  const createMutation = useMutation({
    mutationFn: async () => {
      if (!name.trim()) throw new Error('Name is required.');
      if (kind === 'Category') return createCategory(name.trim());
      if (kind === 'Unit') return createUnit(name.trim(), extra.trim() || null);
      if (!session?.defaultBranchId) throw new Error('No branch on this session.');
      return createWarehouse(name.trim(), session.defaultBranchId, isDefault);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [kind === 'Category' ? 'categories' : kind === 'Unit' ? 'units' : 'warehouses'] });
      succeed(`${name.trim()} added.`);
      setName('');
      setExtra('');
      setIsDefault(false);
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="two-col">
        <div className="list-card" style={{ maxHeight: 520 }}>
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>{kind === 'Unit' ? 'Short Code' : kind === 'Warehouse' ? '' : ''}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id}>
                  <td style={{ color: 'var(--text)' }}>{r.name}</td>
                  <td>{r.meta && <span className="badge badge-gold">{r.meta}</span>}</td>
                </tr>
              ))}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={2} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                    None yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="form-card">
          <div className="form-card-title">New {kind}</div>
          <div className="field">
            <label>Name</label>
            <input className="input" value={name} onChange={(e) => setName(e.target.value)} placeholder={kind === 'Warehouse' ? 'e.g. Main Store' : 'e.g. Beverages'} />
          </div>
          {kind === 'Unit' && (
            <div className="field">
              <label>Short Code</label>
              <input className="input" value={extra} onChange={(e) => setExtra(e.target.value)} placeholder="e.g. kg, pc" />
            </div>
          )}
          {kind === 'Warehouse' && (
            <div className="toggle-field">
              <span>Set as default warehouse</span>
              <button type="button" className={`switch${isDefault ? '' : ' off'}`} onClick={() => setIsDefault((v) => !v)} />
            </div>
          )}
          <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
            {createMutation.isPending ? <span className="spinner" /> : `Add ${kind}`}
          </button>
        </div>
      </div>
    </>
  );
}
