import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listParties } from '../../api/masters';
import { createPartyPayment, listPartyPayments } from '../../api/accounting';
import { Banner, useBanner } from '../../components/Shared';
import { formatDate, formatRs, todayIso } from '../../lib/format';

const MODES = ['Cash', 'Card', 'ESewa', 'Khalti', 'BankTransfer'];

export function PaymentEntryTab() {
  const queryClient = useQueryClient();
  const { banner, fail, succeed, clear } = useBanner();
  const [partyId, setPartyId] = useState('');
  const [direction, setDirection] = useState<'Paid' | 'Received'>('Paid');
  const [amount, setAmount] = useState('');
  const [paymentMode, setPaymentMode] = useState('Cash');
  const [paymentDate, setPaymentDate] = useState(todayIso());
  const [narration, setNarration] = useState('');

  const partiesQuery = useQuery({ queryKey: ['parties-all'], queryFn: () => listParties({ activeOnly: true }) });
  const paymentsQuery = useQuery({ queryKey: ['party-payments'], queryFn: () => listPartyPayments() });

  const createMutation = useMutation({
    mutationFn: () => {
      if (!partyId) throw new Error('Select a party.');
      const amt = Number(amount);
      if (!amt || amt <= 0) throw new Error('Enter a positive amount.');
      return createPartyPayment({ partyId, direction, amount: amt, paymentMode, paymentDate, narration: narration.trim() || null });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['party-payments'] });
      succeed('Payment recorded.');
      setAmount('');
      setNarration('');
    },
    onError: fail,
  });

  return (
    <>
      <Banner banner={banner} onClear={clear} />
      <div className="two-col">
        <div className="card">
          <div className="card-head">
            <div className="card-title">Payment Entries</div>
            <span className="chip">{(paymentsQuery.data ?? []).length} recorded</span>
          </div>
          <div className="scroll-x">
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Party</th>
                  <th>Direction</th>
                  <th>Mode</th>
                  <th style={{ textAlign: 'right' }}>Amount</th>
                </tr>
              </thead>
              <tbody>
                {(paymentsQuery.data ?? []).map((p) => (
                  <tr key={p.id}>
                    <td>{formatDate(p.paymentDate)}</td>
                    <td style={{ color: 'var(--text)' }}>{p.partyName}</td>
                    <td>
                      <span className={`badge ${p.direction === 'Received' ? 'badge-success' : 'badge-gold'}`}>{p.direction}</span>
                    </td>
                    <td>{p.paymentMode}</td>
                    <td style={{ textAlign: 'right' }} className="tabular">
                      {formatRs(p.amount)}
                    </td>
                  </tr>
                ))}
                {(paymentsQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={5} className="muted" style={{ textAlign: 'center', padding: 30 }}>
                      No payments recorded yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="form-card">
          <div className="form-card-title">New Payment Entry</div>
          <div className="field">
            <label>Party</label>
            <select className="input" value={partyId} onChange={(e) => setPartyId(e.target.value)}>
              <option value="">Select…</option>
              {(partiesQuery.data ?? []).map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Direction</label>
            <div className="type-seg">
              <button className={`type-seg-btn${direction === 'Paid' ? ' on' : ''}`} onClick={() => setDirection('Paid')}>
                Paid Out
              </button>
              <button className={`type-seg-btn${direction === 'Received' ? ' on' : ''}`} onClick={() => setDirection('Received')}>
                Received In
              </button>
            </div>
          </div>
          <div className="frow">
            <div className="field">
              <label>Amount</label>
              <input className="input" type="number" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
            </div>
            <div className="field">
              <label>Payment Mode</label>
              <select className="input" value={paymentMode} onChange={(e) => setPaymentMode(e.target.value)}>
                {MODES.map((m) => (
                  <option key={m} value={m}>
                    {m}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="field">
            <label>Date</label>
            <input className="input" type="date" value={paymentDate} onChange={(e) => setPaymentDate(e.target.value)} />
          </div>
          <div className="field">
            <label>Narration</label>
            <input className="input" value={narration} onChange={(e) => setNarration(e.target.value)} />
          </div>
          <button className="btn btn-primary btn-block" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
            {createMutation.isPending ? <span className="spinner" /> : 'Record Payment'}
          </button>
        </div>
      </div>
    </>
  );
}
