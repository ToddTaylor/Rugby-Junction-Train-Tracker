import { AuthSession } from '../types/Auth';

const STORAGE_KEY = 'rjtt_auth_session';

export async function getAuthToken(): Promise<string | null> {
    const sessionData = localStorage.getItem(STORAGE_KEY);
    if (sessionData) {
        try {
            const session = JSON.parse(sessionData) as AuthSession;
            return session.token;
        } catch { }
    }

    return null;
}
