export interface AuthSession {
  email: string;
  token: string;
  expiresUtc: string; // ISO date
}

export interface AuthState {
  session: AuthSession | null;
  loading: boolean;
  error?: string;
  step: 'email' | 'code' | 'ready';
  emailInput: string;
  remember: boolean;
}
