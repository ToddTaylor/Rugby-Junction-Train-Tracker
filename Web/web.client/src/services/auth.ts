import { getCookie } from '../utils/cookies';
import { AuthSession } from '../types/Auth';

const COOKIE_NAME = 'rjtt_auth';
const SESSION_KEY = 'rjtt_auth_session';

export async function getAuthToken(): Promise<string | null> {
    // Try cookie first
    const cookieData = getCookie(COOKIE_NAME);
    if (cookieData) {
        try {
            const session = JSON.parse(cookieData) as AuthSession;
            return session.token;
        } catch { }
    }

    // Try sessionStorage
    const sessionData = sessionStorage.getItem(SESSION_KEY);
    if (sessionData) {
        try {
            const session = JSON.parse(sessionData) as AuthSession;
            return session.token;
        } catch { }
    }

    return null;
}
