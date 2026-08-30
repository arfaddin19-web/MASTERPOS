import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { completeSetup } from '../api/setup';
import { apiErrorMessage } from '../api/client';
import type { SetupCompanyRequest } from '../api/types';
import { ArrowRightIcon } from '../components/icons';

type BusinessType = 'Cafe' | 'Trading';

const initialForm: SetupCompanyRequest = {
  companyName: '',
  businessType: 'Cafe',
  taxRegistrationType: 'Vat',
  vatRegistrationNumber: '',
  vatRatePercent: 13,
  payrollEnabled: true,
  branchName: '',
  city: '',
  address: '',
  phone: '',
  adminFullName: '',
  adminUsername: '',
  adminPassword: '',
  adminEmail: '',
};

export function SetupPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState<1 | 2>(1);
  const [form, setForm] = useState<SetupCompanyRequest>(initialForm);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  function set<K extends keyof SetupCompanyRequest>(key: K, value: SetupCompanyRequest[K]) {
    setForm((f) => ({ ...f, [key]: value }));
  }

  async function handleFinish() {
    setError(null);
    setLoading(true);
    try {
      await completeSetup(form);
      navigate('/login', { replace: true });
    } catch (err) {
      setError(apiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="setup-wrap">
      <div className="brand">
        <div className="brand-mark">M</div>
        <div className="brand-word">MasterPOS</div>
      </div>
      <div className="steps">
        <div className="step-dot" />
        <div className={`step-dot${step === 1 ? ' off' : ''}`} />
      </div>

      {step === 1 ? (
        <>
          <div className="heading">Let's set up your workspace</div>
          <div className="subhead">
            These two choices decide which modules and fields appear across the app — you can
            change them anytime in Settings → Business Profile.
          </div>

          <div className="section">
            <div className="section-label">What kind of business are you running?</div>
            <div className="type-grid">
              {(['Cafe', 'Trading'] as BusinessType[]).map((type) => (
                <button
                  key={type}
                  type="button"
                  className={`type-card${form.businessType === type ? ' on' : ''}`}
                  onClick={() => set('businessType', type)}
                >
                  <div className={`radio${form.businessType === type ? ' on' : ''}`}>
                    {form.businessType === type && (
                      <svg width="11" height="11" viewBox="0 0 20 20" fill="none">
                        <path d="M4 10.5 8 14.5 16 5.5" stroke="var(--ink)" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    )}
                  </div>
                  <div className="type-title">{type === 'Cafe' ? 'Cafe / Restaurant' : 'Trading / Retail'}</div>
                  <div className="type-desc">
                    {type === 'Cafe'
                      ? 'Table service, kitchen & bar routing, menu-style billing for dine-in, takeaway and delivery.'
                      : 'Barcode billing, stock-heavy inventory, supplier & customer ledgers for wholesale or retail trade.'}
                  </div>
                </button>
              ))}
            </div>
          </div>

          <div className="section">
            <div className="section-label">Include Payroll &amp; Workforce?</div>
            <div className="payroll-row">
              <div>
                <div className="payroll-title">Payroll &amp; Workforce module</div>
                <div className="payroll-desc">Employee attendance, leave, overtime, advances and payslip generation.</div>
              </div>
              <div className="yn">
                <button type="button" className={`yn-opt${form.payrollEnabled ? ' on' : ''}`} onClick={() => set('payrollEnabled', true)}>
                  Yes
                </button>
                <button type="button" className={`yn-opt${!form.payrollEnabled ? ' on' : ''}`} onClick={() => set('payrollEnabled', false)}>
                  No
                </button>
              </div>
            </div>
          </div>

          <div className="actions">
            <div className="foot-note">Step 1 of 2 — Business Type &amp; Modules</div>
            <button type="button" className="btn btn-primary" onClick={() => setStep(2)}>
              Continue <ArrowRightIcon />
            </button>
          </div>
        </>
      ) : (
        <>
          <div className="heading">Tell us about your company</div>
          <div className="subhead">This creates your company, primary branch, and the first Admin login.</div>

          <div className="section">
            <div className="section-label">Company &amp; Tax</div>
            <div className="grid2">
              <div className="field">
                <label>Company Name</label>
                <input className="input" value={form.companyName} onChange={(e) => set('companyName', e.target.value)} />
              </div>
              <div className="field">
                <label>Tax Registration</label>
                <select className="input" value={form.taxRegistrationType} onChange={(e) => set('taxRegistrationType', e.target.value as 'Vat' | 'Pan')}>
                  <option value="Vat">VAT</option>
                  <option value="Pan">PAN</option>
                </select>
              </div>
              <div className="field">
                <label>VAT/PAN Registration Number</label>
                <input className="input" value={form.vatRegistrationNumber ?? ''} onChange={(e) => set('vatRegistrationNumber', e.target.value)} />
              </div>
              <div className="field">
                <label>VAT Rate %</label>
                <input
                  type="number"
                  className="input"
                  value={form.vatRatePercent}
                  onChange={(e) => set('vatRatePercent', Number(e.target.value))}
                />
              </div>
            </div>
          </div>

          <div className="section">
            <div className="section-label">Primary Branch</div>
            <div className="grid2">
              <div className="field">
                <label>Branch Name</label>
                <input className="input" value={form.branchName} onChange={(e) => set('branchName', e.target.value)} />
              </div>
              <div className="field">
                <label>City</label>
                <input className="input" value={form.city ?? ''} onChange={(e) => set('city', e.target.value)} />
              </div>
              <div className="field">
                <label>Address</label>
                <input className="input" value={form.address ?? ''} onChange={(e) => set('address', e.target.value)} />
              </div>
              <div className="field">
                <label>Phone</label>
                <input className="input" value={form.phone ?? ''} onChange={(e) => set('phone', e.target.value)} />
              </div>
            </div>
          </div>

          <div className="section">
            <div className="section-label">Admin Login</div>
            <div className="grid2">
              <div className="field">
                <label>Full Name</label>
                <input className="input" value={form.adminFullName} onChange={(e) => set('adminFullName', e.target.value)} />
              </div>
              <div className="field">
                <label>Email</label>
                <input className="input" value={form.adminEmail ?? ''} onChange={(e) => set('adminEmail', e.target.value)} />
              </div>
              <div className="field">
                <label>Username</label>
                <input className="input" value={form.adminUsername} onChange={(e) => set('adminUsername', e.target.value)} />
              </div>
              <div className="field">
                <label>Password</label>
                <input
                  type="password"
                  className="input"
                  value={form.adminPassword}
                  onChange={(e) => set('adminPassword', e.target.value)}
                />
              </div>
            </div>
          </div>

          {error && <div className="error-text" style={{ width: '100%', maxWidth: 920 }}>{error}</div>}

          <div className="actions">
            <button type="button" className="btn btn-ghost" onClick={() => setStep(1)}>
              Back
            </button>
            <div className="foot-note">Step 2 of 2 — Company &amp; Admin</div>
            <button type="button" className="btn btn-primary" onClick={handleFinish} disabled={loading}>
              {loading ? <span className="spinner" /> : <>Finish Setup <ArrowRightIcon /></>}
            </button>
          </div>
        </>
      )}
    </div>
  );
}
