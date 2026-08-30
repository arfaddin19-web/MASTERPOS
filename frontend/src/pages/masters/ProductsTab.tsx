import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createCategory,
  createGroup,
  createProduct,
  createUnit,
  deleteProduct,
  getProductBom,
  listCategories,
  listGroups,
  listProducts,
  listUnits,
  listWarehouses,
  setProductActive,
  setProductBom,
  updateProduct,
  type ProductBomLineDto,
} from '../../api/masters';
import type { ProductDto, ProductType } from '../../api/types';
import { Switch, useBanner, Banner } from '../../components/Shared';
import { formatRs } from '../../lib/format';

const TYPES: ProductType[] = ['Inventory', 'Service', 'Recipe', 'Consumable'];
const KOT_STATIONS = ['None', 'Kitchen', 'Bar'] as const;

interface FormState {
  name: string;
  productType: ProductType;
  categoryId: string;
  groupId: string;
  unitId: string;
  defaultWarehouseId: string;
  barcode: string;
  purchasePrice: string;
  salePrice: string;
  isVatApplicable: boolean;
  reorderLevel: string;
  kotStation: (typeof KOT_STATIONS)[number];
  prepTimeMinutes: string;
  trackInPos: boolean;
  isActive: boolean;
}

function blankForm(): FormState {
  return {
    name: '',
    productType: 'Inventory',
    categoryId: '',
    groupId: '',
    unitId: '',
    defaultWarehouseId: '',
    barcode: '',
    purchasePrice: '0',
    salePrice: '0',
    isVatApplicable: true,
    reorderLevel: '0',
    kotStation: 'None',
    prepTimeMinutes: '',
    trackInPos: true,
    isActive: true,
  };
}

function toForm(p: ProductDto): FormState {
  return {
    name: p.name,
    productType: p.productType,
    categoryId: p.categoryId ?? '',
    groupId: p.groupId ?? '',
    unitId: p.unitId,
    defaultWarehouseId: p.defaultWarehouseId ?? '',
    barcode: p.barcode ?? '',
    purchasePrice: String(p.purchasePrice),
    salePrice: String(p.salePrice),
    isVatApplicable: p.isVatApplicable,
    reorderLevel: String(p.reorderLevel),
    kotStation: (p.kotStation ?? 'None') as (typeof KOT_STATIONS)[number],
    prepTimeMinutes: p.prepTimeMinutes != null ? String(p.prepTimeMinutes) : '',
    trackInPos: p.trackInPos,
    isActive: p.isActive,
  };
}

export function ProductsTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();

  const [search, setSearch] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(blankForm());
  const [bomLines, setBomLines] = useState<(ProductBomLineDto & { key: string })[]>([]);

  const productsQuery = useQuery({
    queryKey: ['masters-products', search, typeFilter],
    queryFn: () => listProducts({ search: search || undefined, productType: typeFilter || undefined }),
  });
  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: listCategories });
  const groupsQuery = useQuery({ queryKey: ['groups'], queryFn: listGroups });
  const unitsQuery = useQuery({ queryKey: ['units'], queryFn: listUnits });
  const warehousesQuery = useQuery({ queryKey: ['warehouses'], queryFn: listWarehouses });

  const ingredientCandidates = useMemo(
    () => (productsQuery.data ?? []).filter((p) => p.productType === 'Inventory' && p.isActive),
    [productsQuery.data],
  );

  const selected = (productsQuery.data ?? []).find((p) => p.id === selectedId) ?? null;

  useEffect(() => {
    if (selected) {
      setForm(toForm(selected));
      if (selected.productType === 'Recipe') {
        getProductBom(selected.id)
          .then((lines) => setBomLines(lines.map((l) => ({ ...l, key: l.componentProductId }))))
          .catch(() => setBomLines([]));
      } else {
        setBomLines([]);
      }
    }
  }, [selectedId]); // eslint-disable-line react-hooks/exhaustive-deps

  function startNew() {
    setSelectedId(null);
    setForm(blankForm());
    setBomLines([]);
    clear();
  }

  function invalidateAll() {
    queryClient.invalidateQueries({ queryKey: ['masters-products'] });
    queryClient.invalidateQueries({ queryKey: ['products'] });
  }

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!form.name.trim()) throw new Error('Product name is required.');
      if (!form.unitId) throw new Error('Select a unit.');
      const payload = {
        name: form.name.trim(),
        productType: form.productType,
        categoryId: form.categoryId || null,
        groupId: form.groupId || null,
        unitId: form.unitId,
        defaultWarehouseId: form.defaultWarehouseId || null,
        barcode: form.barcode.trim() || null,
        purchasePrice: Number(form.purchasePrice) || 0,
        salePrice: Number(form.salePrice) || 0,
        isVatApplicable: form.isVatApplicable,
        reorderLevel: Number(form.reorderLevel) || 0,
        kotStation: form.kotStation === 'None' ? null : (form.kotStation as 'Kitchen' | 'Bar'),
        prepTimeMinutes: form.prepTimeMinutes ? Number(form.prepTimeMinutes) : null,
        trackInPos: form.trackInPos,
        isActive: form.isActive,
      };
      const saved = selectedId ? await updateProduct(selectedId, payload) : await createProduct(payload);
      // The product itself is now safely created/updated, whatever happens
      // next with its BOM — reflect that in the UI immediately so a BOM
      // failure below can never orphan it (created server-side but never
      // selected, leaving a retry to silently create a duplicate).
      invalidateAll();
      setSelectedId(saved.id);
      if (saved.productType === 'Recipe') {
        const lines = bomLines.filter((l) => l.componentProductId).map((l) => ({ componentProductId: l.componentProductId, quantity: l.quantity }));
        if (lines.length === 0) {
          throw new Error(`${saved.name} saved — now add at least one ingredient below and save again to complete the recipe.`);
        }
        await setProductBom(saved.id, lines);
      }
      return saved;
    },
    onSuccess: (saved) => {
      succeed(selectedId ? `${saved.name} updated.` : `${saved.name} created.`);
    },
    onError: fail,
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteProduct(selectedId!),
    onSuccess: () => {
      invalidateAll();
      succeed('Product deleted.');
      startNew();
    },
    onError: fail,
  });

  const toggleActiveMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setProductActive(id, isActive),
    onSuccess: (updated) => {
      invalidateAll();
      if (updated.id === selectedId) setForm((f) => ({ ...f, isActive: updated.isActive }));
    },
    onError: fail,
  });

  async function quickAddCategory() {
    const name = window.prompt('New category name');
    if (!name?.trim()) return;
    try {
      const created = await createCategory(name.trim());
      await queryClient.invalidateQueries({ queryKey: ['categories'] });
      setForm((f) => ({ ...f, categoryId: created.id }));
    } catch (err) {
      fail(err);
    }
  }
  async function quickAddGroup() {
    const name = window.prompt('New group name');
    if (!name?.trim()) return;
    try {
      const created = await createGroup(name.trim());
      await queryClient.invalidateQueries({ queryKey: ['groups'] });
      setForm((f) => ({ ...f, groupId: created.id }));
    } catch (err) {
      fail(err);
    }
  }
  async function quickAddUnit() {
    const name = window.prompt('New unit name (e.g. Plate, Kg)');
    if (!name?.trim()) return;
    try {
      const created = await createUnit(name.trim());
      await queryClient.invalidateQueries({ queryKey: ['units'] });
      setForm((f) => ({ ...f, unitId: created.id }));
    } catch (err) {
      fail(err);
    }
  }

  function addBomRow() {
    setBomLines((rows) => [...rows, { componentProductId: '', componentProductName: '', unitName: '', quantity: 0, key: `new-${rows.length}-${Date.now()}` }]);
  }
  function updateBomRow(key: string, patch: Partial<ProductBomLineDto>) {
    setBomLines((rows) =>
      rows.map((r) => {
        if (r.key !== key) return r;
        const next = { ...r, ...patch };
        if (patch.componentProductId) {
          const prod = ingredientCandidates.find((p) => p.id === patch.componentProductId);
          if (prod) next.unitName = prod.unitName;
        }
        return next;
      }),
    );
  }
  function removeBomRow(key: string) {
    setBomLines((rows) => rows.filter((r) => r.key !== key));
  }

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="toolbar">
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
          <div className="search-pill" style={{ width: 220 }}>
            <input placeholder="Search products…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
          <select className="input" style={{ width: 160 }} value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)}>
            <option value="">All Types</option>
            {TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
          <div className="chip">{(productsQuery.data ?? []).length} Products</div>
        </div>
        <button className="btn btn-primary" onClick={startNew}>
          + New Product
        </button>
      </div>

      <div className="split">
        <div className="list-card">
          {productsQuery.isLoading ? (
            <div className="empty-state">
              <span className="spinner" />
            </div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th style={{ width: 26 }}></th>
                  <th>Product</th>
                  <th>Type</th>
                  <th>Unit</th>
                  <th style={{ textAlign: 'right' }}>Price</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {(productsQuery.data ?? []).map((p) => (
                  <tr key={p.id} className={`row-clickable${p.id === selectedId ? ' row-selected' : ''}`} onClick={() => setSelectedId(p.id)}>
                    <td onClick={(e) => e.stopPropagation()}>
                      <Switch on={p.isActive} onToggle={() => toggleActiveMutation.mutate({ id: p.id, isActive: !p.isActive })} title="Active" />
                    </td>
                    <td>
                      <div className="pname">{p.name}</div>
                      <div className="psku">{p.barcode ?? p.categoryName ?? p.productType}</div>
                    </td>
                    <td>
                      <span className={`type-chip ${p.productType.toLowerCase()}`}>{p.productType}</span>
                    </td>
                    <td>{p.unitName}</td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {formatRs(p.salePrice)}
                    </td>
                    <td>
                      <span className={`badge ${p.isActive ? 'badge-success' : 'badge-neutral'}`}>{p.isActive ? 'Active' : 'Inactive'}</span>
                    </td>
                  </tr>
                ))}
                {(productsQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={6} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                      No products match.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          )}
        </div>

        <div className="form-card">
          <div className="form-head">
            <div className="form-card-title">{selectedId ? 'Edit Product' : 'New Product'}</div>
            {selectedId && (
              <button className="close-x" onClick={startNew} title="Close">
                ✕
              </button>
            )}
          </div>

          <div className="field">
            <label>Product Name</label>
            <input className="input" value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} />
          </div>

          <div className="field">
            <label>Type</label>
            <div className="type-seg">
              {TYPES.map((t) => (
                <button key={t} className={`type-seg-btn${form.productType === t ? ' on' : ''}`} onClick={() => setForm((f) => ({ ...f, productType: t }))}>
                  {t}
                </button>
              ))}
            </div>
          </div>

          <div className="frow">
            <div className="field">
              <label>Category</label>
              <div className="field-row">
                <select className="input" value={form.categoryId} onChange={(e) => setForm((f) => ({ ...f, categoryId: e.target.value }))}>
                  <option value="">—</option>
                  {(categoriesQuery.data ?? []).map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>
                <button className="quick-add" onClick={quickAddCategory} title="Quick add category">
                  +
                </button>
              </div>
            </div>
            <div className="field">
              <label>Group</label>
              <div className="field-row">
                <select className="input" value={form.groupId} onChange={(e) => setForm((f) => ({ ...f, groupId: e.target.value }))}>
                  <option value="">—</option>
                  {(groupsQuery.data ?? []).map((g) => (
                    <option key={g.id} value={g.id}>
                      {g.name}
                    </option>
                  ))}
                </select>
                <button className="quick-add" onClick={quickAddGroup} title="Quick add group">
                  +
                </button>
              </div>
            </div>
          </div>

          <div className="frow">
            <div className="field">
              <label>Unit</label>
              <div className="field-row">
                <select className="input" value={form.unitId} onChange={(e) => setForm((f) => ({ ...f, unitId: e.target.value }))}>
                  <option value="">Select…</option>
                  {(unitsQuery.data ?? []).map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name}
                    </option>
                  ))}
                </select>
                <button className="quick-add" onClick={quickAddUnit} title="Quick add unit">
                  +
                </button>
              </div>
            </div>
            <div className="field">
              <label>KOT Station</label>
              <select className="input" value={form.kotStation} onChange={(e) => setForm((f) => ({ ...f, kotStation: e.target.value as (typeof KOT_STATIONS)[number] }))}>
                {KOT_STATIONS.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="frow">
            <div className="field">
              <label>Purchase Price</label>
              <input className="input" type="number" step="0.01" value={form.purchasePrice} onChange={(e) => setForm((f) => ({ ...f, purchasePrice: e.target.value }))} />
            </div>
            <div className="field">
              <label>Sale Price</label>
              <input className="input" type="number" step="0.01" value={form.salePrice} onChange={(e) => setForm((f) => ({ ...f, salePrice: e.target.value }))} />
            </div>
          </div>

          <div className="frow">
            <div className="field">
              <label>VAT Applicable</label>
              <div className="toggle-field">
                <span>{form.isVatApplicable ? 'Charged' : 'Exempt'}</span>
                <Switch on={form.isVatApplicable} onToggle={() => setForm((f) => ({ ...f, isVatApplicable: !f.isVatApplicable }))} />
              </div>
            </div>
            <div className="field">
              <label>Reorder Level</label>
              {form.productType === 'Inventory' || form.productType === 'Consumable' ? (
                <input className="input" type="number" step="0.001" value={form.reorderLevel} onChange={(e) => setForm((f) => ({ ...f, reorderLevel: e.target.value }))} />
              ) : (
                <div className="disabled-field">Not applicable</div>
              )}
            </div>
          </div>

          <div className="field">
            <label>Default Warehouse</label>
            <select className="input" value={form.defaultWarehouseId} onChange={(e) => setForm((f) => ({ ...f, defaultWarehouseId: e.target.value }))}>
              <option value="">—</option>
              {(warehousesQuery.data ?? []).map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </select>
          </div>

          {form.productType === 'Recipe' && (
            <div className="bom-card">
              <div className="bom-head">
                <div className="bom-title">Recipe · Bill of Materials</div>
              </div>
              <div className="bom-row" style={{ color: 'var(--text-faint)', fontSize: 10, textTransform: 'uppercase', letterSpacing: '.3px' }}>
                <div>Ingredient</div>
                <div>Qty</div>
                <div>Unit</div>
                <div></div>
              </div>
              {bomLines.map((line) => (
                <div className="bom-row" key={line.key}>
                  <select className="input" value={line.componentProductId} onChange={(e) => updateBomRow(line.key, { componentProductId: e.target.value })}>
                    <option value="">Select ingredient…</option>
                    {ingredientCandidates.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                  <input
                    className="input"
                    type="number"
                    step="0.001"
                    value={line.quantity}
                    onChange={(e) => updateBomRow(line.key, { quantity: Number(e.target.value) })}
                  />
                  <input className="input" value={line.unitName} readOnly />
                  <button className="bom-remove" onClick={() => removeBomRow(line.key)}>
                    ✕
                  </button>
                </div>
              ))}
              <button className="bom-add" onClick={addBomRow}>
                + Add Ingredient
              </button>
            </div>
          )}

          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ fontSize: 12.5, color: 'var(--text-dim)' }}>Enable in POS</div>
            <Switch on={form.trackInPos} onToggle={() => setForm((f) => ({ ...f, trackInPos: !f.trackInPos }))} />
          </div>

          <div className="form-foot">
            <button className="btn btn-primary" style={{ flex: 1, justifyContent: 'center' }} disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
              {saveMutation.isPending ? <span className="spinner" /> : selectedId ? 'Save Changes' : 'Create Product'}
            </button>
            {selectedId && (
              <button
                className="btn btn-danger"
                onClick={() => {
                  if (window.confirm(`Delete "${form.name}"? This only works if it has no transaction history.`)) deleteMutation.mutate();
                }}
              >
                Delete
              </button>
            )}
          </div>
        </div>
      </div>
    </>
  );
}
