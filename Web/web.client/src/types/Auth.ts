export interface AuthSession {
  email: string;
  token: string;
  expiresUtc: string; // ISO date
  roles?: string[]; // User roles for authorization
  userId?: number; // User ID
}

export interface AuthState {
  session: AuthSession | null;
  loading: boolean;
  error?: string;
  step: 'email' | 'code' | 'ready';
  emailInput: string;
  remember: boolean;
}
