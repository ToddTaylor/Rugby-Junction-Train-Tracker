// Authentication API calls using real backend endpoints.

const API_BASE = import.meta.env.VITE_API_URL;
const API_KEY = import.meta.env.VITE_API_KEY;

export interface SendCodeRequest { email: string; }
export interface SendCodeResponse { success: boolean; errors?: string[]; }

export async function sendLoginCode(req: SendCodeRequest): Promise<SendCodeResponse> {
  try {
    const { fetchWithAuth } = await import('../utils/fetchWithAuth');
    const response = await fetchWithAuth(`${API_BASE}/api/v1/auth/send-code`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Api-Key': API_KEY },
      body: JSON.stringify(req)
    });
    const data = await response.json().catch(() => null);
    if (!response.ok) {
      const errors = data?.errors || ['Failed to send code'];
      return { success: false, errors };
    }
    return { success: !!data?.success };
  } catch (e: any) {
    return { success: false, errors: [e.message || 'Unknown error'] };
  }
}

export interface VerifyCodeRequest { email: string; code: string; remember: boolean; }
export interface VerifyCodeResponse { 
  success: boolean; 
  token?: string; 
  expiresUtc?: string; 
  roles?: string[]; 
  userId?: number; 
  errors?: string[]; 
}

export async function verifyLoginCode(req: VerifyCodeRequest): Promise<VerifyCodeResponse> {
  try {
    const { fetchWithAuth } = await import('../utils/fetchWithAuth');
    const response = await fetchWithAuth(`${API_BASE}/api/v1/auth/verify-code`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Api-Key': API_KEY },
      body: JSON.stringify(req)
    });
    const data = await response.json().catch(() => null);
    if (!response.ok) {
      const errors = data?.errors || ['Failed to verify code'];
      return { success: false, errors };
    }
    return {
      success: !!data?.success,
      token: data?.token,
      expiresUtc: data?.expiresUtc,
      roles: data?.roles,
      userId: data?.userId,
      errors: data?.errors
    };
  } catch (e: any) {
    return { success: false, errors: [e.message || 'Unknown error'] };
  }
}
