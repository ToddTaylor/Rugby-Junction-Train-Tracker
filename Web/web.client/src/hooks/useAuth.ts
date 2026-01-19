import { useCallback, useState } from 'react';
import { AuthState, AuthSession } from '../types/Auth';
import { sendLoginCode, verifyLoginCode } from '../api/auth';

const STORAGE_KEY = 'rjtt_auth_session';

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
  const [state, setState] = useState<AuthState>(() => {
    const storedSession = parseSession(localStorage.getItem(STORAGE_KEY));
    return {
      session: storedSession,
      loading: false,
      error: undefined,
      step: storedSession ? 'ready' : 'email',
      emailInput: '',
      remember: false,
    };
  });

  const requestCode = useCallback(async () => {
    if (!state.emailInput) return;
    setState(s => ({ ...s, loading: true, error: undefined }));
    const resp = await sendLoginCode({ email: state.emailInput });
    setState(s => ({
      ...s,
      loading: false,
      error: resp.success ? undefined : (resp.errors?.join(', ') || 'Failed to send code'),
      step: resp.success ? 'code' : 'email'
    }));
  }, [state.emailInput]);

  const verifyCode = useCallback(async (code: string) => {
    if (!state.emailInput || !code) return;
    setState(s => ({ ...s, loading: true, error: undefined }));
    const resp = await verifyLoginCode({ email: state.emailInput, code, remember: state.remember });
    if (!resp.success || !resp.token || !resp.expiresUtc) {
      setState(s => ({ ...s, loading: false, error: resp.errors?.join(', ') || 'Invalid code' }));
      return;
    }
    const session: AuthSession = { 
      email: state.emailInput, 
      token: resp.token, 
      expiresUtc: resp.expiresUtc,
      roles: resp.roles,
      userId: resp.userId
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    setState(s => ({ ...s, session, loading: false, step: 'ready' }));
  }, [state.emailInput, state.remember]);

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY);
    setState({ session: null, loading: false, error: undefined, step: 'email', emailInput: '', remember: false });
  }, []);

  return {
    ...state,
    setEmailInput: (email: string) => setState(s => ({ ...s, emailInput: email })),
    setRemember: (val: boolean) => setState(s => ({ ...s, remember: val })),
    requestCode,
    verifyCode,
    logout,
  };
}
