import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createRole, deleteRole, listRoles, updateRole } from '../../api/auth-admin';
import { PERMISSION_MODULES, type PermissionDto, type RoleDto } from '../../api/types';
import { Banner, useBanner } from '../../components/Shared';

const ACTIONS: (keyof Omit<PermissionDto, 'module'>)[] = ['canView', 'canCreate', 'canEdit', 'canDelete', 'canApprove'];
const ACTION_LABELS: Record<string, string> = { canView: 'View', canCreate: 'Create', canEdit: 'Edit', canDelete: 'Delete', canApprove: 'Approve' };

function blankPermissions(): PermissionDto[] {
  return PERMISSION_MODULES.map((m) => ({ module: m, canView: false, canCreate: false, canEdit: false, canDelete: false, canApprove: false }));
}
function toPermissions(role: RoleDto): PermissionDto[] {
  return PERMISSION_MODULES.map((m) => role.permissions.find((p) => p.module === m) ?? { module: m, canView: false, canCreate: false, canEdit: false, canDelete: false, canApprove: false });
}

export function RolesTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [permissions, setPermissions] = useState<PermissionDto[]>(blankPermissions());

  const rolesQuery = useQuery({ queryKey: ['roles'], queryFn: listRoles });
  const selected = (rolesQuery.data ?? []).find((r) => r.id === selectedId) ?? null;

  // Land on the first role (normally the seeded system Admin) instead of an
  // empty "New Role" form on first paint — there's always at least one role
  // once Setup has run. Only fires once, on initial load: a later null (the
  // user clicking "+ New Role") must stay null, not get overridden back.
  const autoSelected = useRef(false);
  useEffect(() => {
    if (!autoSelected.current && rolesQuery.data && rolesQuery.data.length > 0) {
      autoSelected.current = true;
      setSelectedId(rolesQuery.data[0].id);
    }
  }, [rolesQuery.data]);

  useEffect(() => {
    if (selected) {
      setName(selected.name);
      setPermissions(toPermissions(selected));
    }
  }, [selectedId]); // eslint-disable-line react-hooks/exhaustive-deps

  function startNew() {
    setSelectedId(null);
    setName('');
    setPermissions(blankPermissions());
    clear();
  }

  function toggle(module: string, action: keyof Omit<PermissionDto, 'module'>) {
    setPermissions((rows) => rows.map((r) => (r.module === module ? { ...r, [action]: !r[action] } : r)));
  }

  const saveMutation = useMutation({
    mutationFn: () => {
      if (!name.trim()) throw new Error('Role name is required.');
      return selectedId ? updateRole(selectedId, { name: name.trim(), permissions }) : createRole({ name: name.trim(), permissions });
    },
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ['roles'] });
      setSelectedId(saved.id);
      succeed(selectedId ? `${saved.name} updated.` : `${saved.name} created.`);
    },
    onError: fail,
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteRole(selectedId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['roles'] });
      succeed('Role deleted.');
      startNew();
    },
    onError: fail,
  });

  const readOnly = selected?.isSystemRole ?? false;

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="toolbar">
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {(rolesQuery.data ?? []).map((r) => (
            <button
              key={r.id}
              className={`chip${r.id === selectedId ? ' badge-gold' : ''}`}
              style={{ cursor: 'pointer', border: r.id === selectedId ? '1px solid var(--gold-dim)' : undefined }}
              onClick={() => setSelectedId(r.id)}
            >
              {r.name}
              {r.isSystemRole && ' · System'}
            </button>
          ))}
        </div>
        <button className="btn btn-primary" onClick={startNew}>
          + New Role
        </button>
      </div>

      <div className="card">
        <div className="card-head">
          <div style={{ flex: 1, maxWidth: 280 }}>
            <input className="input" placeholder="Role name" value={name} onChange={(e) => setName(e.target.value)} disabled={readOnly} />
          </div>
          <div style={{ display: 'flex', gap: 10 }}>
            <button className="btn btn-primary" disabled={readOnly || saveMutation.isPending} onClick={() => saveMutation.mutate()}>
              {saveMutation.isPending ? <span className="spinner" /> : selectedId ? 'Save Changes' : 'Create Role'}
            </button>
            {selectedId && !readOnly && (
              <button
                className="btn btn-danger"
                onClick={() => {
                  if (window.confirm(`Delete role "${name}"?`)) deleteMutation.mutate();
                }}
              >
                Delete
              </button>
            )}
          </div>
        </div>
        {readOnly && <div className="page-sub" style={{ marginBottom: 12 }}>The system Admin role always has full access and can't be edited or deleted.</div>}
        <div className="scroll-x">
          <table>
            <thead>
              <tr>
                <th>Module</th>
                {ACTIONS.map((a) => (
                  <th key={a} className="rolecol">
                    {ACTION_LABELS[a]}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {permissions.map((row) => (
                <tr key={row.module}>
                  <td style={{ color: 'var(--text)' }}>{row.module}</td>
                  {ACTIONS.map((a) => (
                    <td key={a} className="rolecol">
                      <button
                        className={row[a] ? 'perm-tick' : 'perm-x'}
                        disabled={readOnly}
                        onClick={() => toggle(row.module, a)}
                        title={`${row.module} · ${ACTION_LABELS[a]}`}
                      >
                        {row[a] && (
                          <svg width="12" height="12" viewBox="0 0 20 20" fill="none">
                            <path d="M4 10.5 8 14.5 16 5.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                          </svg>
                        )}
                      </button>
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}
