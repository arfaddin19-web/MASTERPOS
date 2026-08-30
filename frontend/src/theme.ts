// Dark/light mode — a per-browser preference (not per-user account, no
// backend involved), so plain localStorage is the right store. Applied as
// a data-theme attribute on <html>, matching the CSS in styles/global.css's
// `:root[data-theme='light']` override block.
const THEME_KEY = 'masterpos.theme';
export type Theme = 'dark' | 'light';

export function getStoredTheme(): Theme {
  try {
    return localStorage.getItem(THEME_KEY) === 'light' ? 'light' : 'dark';
  } catch {
    return 'dark';
  }
}

export function applyTheme(theme: Theme) {
  if (theme === 'light') document.documentElement.setAttribute('data-theme', 'light');
  else document.documentElement.removeAttribute('data-theme');
  try {
    localStorage.setItem(THEME_KEY, theme);
  } catch {
    // Private browsing / storage disabled — the toggle still works for this
    // page load, it just won't be remembered next time.
  }
}
