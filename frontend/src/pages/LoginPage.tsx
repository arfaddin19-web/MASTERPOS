import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { apiErrorMessage } from '../api/client';

export function LoginPage() {
  const { login, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  if (isAuthenticated) return <Navigate to="/dashboard" replace />;

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await login(username, password);
      navigate('/dashboard', { replace: true });
    } catch (err) {
      setError(apiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login-wrap">
      <div className="login-panel-left">
        <div className="brand" style={{ position: 'relative' }}>
          <div className="brand-mark">M</div>
          <div>
            <div className="brand-word">MasterPOS</div>
            <div className="brand-sub">Enterprise Suite</div>
          </div>
        </div>
        <div>
          <div className="login-headline">
            Command your business —<br />
            sales, stock &amp; <em>payroll</em>,<br />
            in one ledger.
          </div>
          <div className="login-lede">
            Point of Sale, ERP and Workforce management built for retailers who run tight
            operations across every branch, every shift, every rupee.
          </div>
        </div>
        <div className="login-stats">
          <div>
            <div className="login-stat-num">1</div>
            <div className="login-stat-label">Local Install</div>
          </div>
          <div>
            <div className="login-stat-num">10</div>
            <div className="login-stat-label">Modules Live</div>
          </div>
          <div>
            <div className="login-stat-num">100%</div>
            <div className="login-stat-label">Your Data, Your Server</div>
          </div>
        </div>
      </div>

      <div className="login-panel-right">
        <form className="login-form-card" onSubmit={handleSubmit}>
          <div className="form-eyebrow">Welcome back</div>
          <div className="form-title">Sign in to your workspace</div>
          <div className="form-sub">Enter your credentials to continue.</div>

          <div className="field">
            <label htmlFor="username">Username</label>
            <input
              id="username"
              className="input"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
              autoFocus
              required
            />
          </div>
          <div className="field">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              className="input"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
            />
          </div>

          {error && <div className="error-text">{error}</div>}

          <button type="submit" className="btn btn-primary btn-block" disabled={loading} style={{ marginTop: 8 }}>
            {loading ? <span className="spinner" /> : 'Sign In'}
          </button>

          <div className="foot-note">Protected by role-based access &amp; audit trail</div>
        </form>
      </div>
    </div>
  );
}
