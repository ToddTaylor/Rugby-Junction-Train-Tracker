import { useCallback, useState } from 'react';
import { AuthState, AuthSession } from '../types/Auth';
import { sendLoginCode, verifyLoginCode } from '../api/auth';
import { getCookie, setCookie, eraseCookie } from '../utils/cookies';

const COOKIE_NAME = 'rjtt_auth';
const SESSION_KEY = 'rjtt_auth_session';

function parseSession(raw: string | null): AuthSession | null {
  if (!raw) return null;
  try {
    const obj = JSON.parse(raw);
    if (!obj.token || !obj.expiresUtc) return null;
    if (new Date(obj.expiresUtc).getTime() < Date.now()) return null; // expired
    return obj as AuthSession;
  } catch { return null; }
}

export function useAuth() {
  // Disable auth if running on localhost (any port)
  const isLocalhost = typeof window !== 'undefined' && window.location.hostname === 'localhost';
  const [state, setState] = useState<AuthState>(() => {
    if (isLocalhost) {
      // Fake session for localhost with Admin role
      return {
        session: { 
          email: 'dev@localhost', 
          token: 'dev-token', 
          expiresUtc: new Date(Date.now() + 86400000).toISOString(),
          roles: ['Admin'],
          userId: 1
        },
        loading: false,
        error: undefined,
        step: 'ready',
        emailInput: '',
        remember: false,
      };
    }
    const cookieSession = parseSession(getCookie(COOKIE_NAME));
    const ephemeralSession = parseSession(sessionStorage.getItem(SESSION_KEY));
    const active = cookieSession || ephemeralSession;
    return {
      session: active,
      loading: false,
      error: undefined,
      step: active ? 'ready' : 'email',
      emailInput: '',
      remember: false,
    };
  });

  const requestCode = useCallback(async () => {
    if (isLocalhost) return;
    if (!state.emailInput) return;
    setState(s => ({ ...s, loading: true, error: undefined }));
    const resp = await sendLoginCode({ email: state.emailInput });
    setState(s => ({
      ...s,
      loading: false,
      error: resp.success ? undefined : (resp.errors?.join(', ') || 'Failed to send code'),
      step: resp.success ? 'code' : 'email'
    }));
  }, [state.emailInput, isLocalhost]);

  const verifyCode = useCallback(async (code: string) => {
    if (isLocalhost) return;
    if (!state.emailInput || !code) return;
    setState(s => ({ ...s, loading: true, error: undefined }));
    const resp = await verifyLoginCode({ email: state.emailInput, code, remember: state.remember });
    if (!resp.success || !resp.token || !resp.expiresUtc) {
      setState(s => ({ ...s, loading: false, error: resp.errors?.join(', ') || 'Invalid code' }));
      return;
    }
    const session: AuthSession = { email: state.emailInput, token: resp.token, expiresUtc: resp.expiresUtc };
    if (state.remember) {
      setCookie(COOKIE_NAME, JSON.stringify(session), 365); // 1 year
      sessionStorage.removeItem(SESSION_KEY);
    } else {
      sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
    }
    setState(s => ({ ...s, session, loading: false, step: 'ready' }));
  }, [state.emailInput, state.remember, isLocalhost]);

  const logout = useCallback(() => {
    if (isLocalhost) {
      setState({ session: null, loading: false, error: undefined, step: 'ready', emailInput: '', remember: false });
      return;
    }
    eraseCookie(COOKIE_NAME);
    sessionStorage.removeItem(SESSION_KEY);
    setState({ session: null, loading: false, error: undefined, step: 'email', emailInput: '', remember: false });
  }, [isLocalhost]);

  return {
    ...state,
    setEmailInput: (email: string) => setState(s => ({ ...s, emailInput: email })),
    setRemember: (val: boolean) => setState(s => ({ ...s, remember: val })),
    requestCode,
    verifyCode,
    logout,
  };
}
