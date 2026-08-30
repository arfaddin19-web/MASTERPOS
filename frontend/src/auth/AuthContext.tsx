import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { login as apiLogin } from '../api/auth';
import { setStoredToken } from '../api/client';
import type { LoginResponse, PermissionDto } from '../api/types';

const SESSION_KEY = 'masterpos.session';

type Session = Omit<LoginResponse, 'accessToken'>;

interface AuthContextValue {
  session: Session | null;
  isAuthenticated: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
  hasPermission: (module: string, action: keyof Omit<PermissionDto, 'module'>) => boolean;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function loadSession(): Session | null {
  const raw = localStorage.getItem(SESSION_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as Session;
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => loadSession());

  const login = useCallback(async (username: string, password: string) => {
    const response = await apiLogin(username, password);
    const { accessToken, ...rest } = response;
    setStoredToken(accessToken);
    localStorage.setItem(SESSION_KEY, JSON.stringify(rest));
    setSession(rest);
  }, []);

  const logout = useCallback(() => {
    setStoredToken(null);
    localStorage.removeItem(SESSION_KEY);
    setSession(null);
  }, []);

  const hasPermission = useCallback(
    (module: string, action: keyof Omit<PermissionDto, 'module'>) => {
      const perm = session?.permissions.find((p) => p.module === module);
      return perm ? perm[action] : false;
    },
    [session],
  );

  const value = useMemo<AuthContextValue>(
    () => ({ session, isAuthenticated: session !== null, login, logout, hasPermission }),
    [session, login, logout, hasPermission],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider');
  return ctx;
}
