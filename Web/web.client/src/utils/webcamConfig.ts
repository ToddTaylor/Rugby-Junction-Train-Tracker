export const MAX_WEBCAMS = 12;

const STORAGE_KEY = 'webcam-config';
const LEGACY_CACHE_KEYS = ['youtube-videos', 'youtube-videos-time'];
const VIDEO_ID_PATTERN = /^[A-Za-z0-9_-]{11}$/;

export interface WebCamEntry {
    /** Search text or a YouTube URL used to find the live stream. */
    source: string;
    /** YouTube video ID. When blank, the page searches for a live stream using the source. */
    videoId: string;
}

export const DEFAULT_SOURCES = [
    'The Press Box LIVE Railcam - Fond du Lac, WI',
    'Slinger Depot Museum Live Railcam',
    'Bulldog Builders LIVE Railcam - Waukesha, WI',
    'Burlington, WI Dougs Auto Service Live Railcam',
    'Hiawatha Bar & Grill LIVE Railcam - Sturtevant, WI',
    'Wauwatosa Historical Society Live Railcam',
    'Thirsty Bird Saloon LIVE Railcam - Oconomowoc, WI',
    'Julies Java House Live Railcam',
    'Wash Station LIVE Railcam - Wauwatosa (Butler), WI'
];

export const emptyEntry = (): WebCamEntry => ({ source: '', videoId: '' });

export function defaultEntries(): WebCamEntry[] {
    return DEFAULT_SOURCES.map(source => ({ source, videoId: '' }));
}

/** Returns exactly MAX_WEBCAMS rows, filling the remainder with empty rows. */
export function padEntries(entries: WebCamEntry[]): WebCamEntry[] {
    const rows = entries.slice(0, MAX_WEBCAMS).map(e => ({ ...e }));
    while (rows.length < MAX_WEBCAMS) rows.push(emptyEntry());
    return rows;
}

/** Trims values, drops empty rows, and caps the list at MAX_WEBCAMS. Returns null for malformed input. */
export function normalizeEntries(value: unknown): WebCamEntry[] | null {
    if (!Array.isArray(value)) return null;
    const entries: WebCamEntry[] = [];
    for (const item of value) {
        if (!item || typeof item !== 'object') return null;
        const { source, videoId } = item as Record<string, unknown>;
        const entry = {
            source: typeof source === 'string' ? source.trim() : '',
            videoId: typeof videoId === 'string' ? videoId.trim() : ''
        };
        if (entry.source || entry.videoId) entries.push(entry);
    }
    return entries.slice(0, MAX_WEBCAMS);
}

export function loadEntries(): WebCamEntry[] {
    try {
        LEGACY_CACHE_KEYS.forEach(key => localStorage.removeItem(key));
        const stored = localStorage.getItem(STORAGE_KEY);
        if (stored) return normalizeEntries(JSON.parse(stored)) ?? defaultEntries();
    } catch (e) {
        console.warn('Could not read the web cam configuration:', e);
    }
    return defaultEntries();
}

export function saveEntries(entries: WebCamEntry[]): WebCamEntry[] {
    const normalized = normalizeEntries(entries) ?? [];
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(normalized));
    } catch (e) {
        console.warn('Could not save the web cam configuration:', e);
    }
    return normalized;
}

function toUrl(input: string): URL | null {
    const withScheme = /^https?:\/\//i.test(input) ? input : `https://${input}`;
    try {
        return new URL(withScheme);
    } catch {
        return null;
    }
}

function isYouTubeHost(host: string) {
    return /(^|\.)youtube(-nocookie)?\.com$/i.test(host);
}

/** Extracts a video ID from a bare ID or any common YouTube video URL. */
export function parseVideoId(input: string): string | null {
    const value = input.trim();
    if (VIDEO_ID_PATTERN.test(value)) return value;
    if (!/youtu/i.test(value)) return null;

    const url = toUrl(value);
    if (!url) return null;

    const segments = url.pathname.split('/').filter(Boolean);
    let candidate: string | null | undefined = null;
    if (/^(www\.)?youtu\.be$/i.test(url.hostname)) {
        candidate = segments[0];
    } else if (isYouTubeHost(url.hostname)) {
        candidate = url.searchParams.get('v')
            ?? (['live', 'embed', 'shorts', 'v'].includes(segments[0]) ? segments[1] : null);
    }
    return candidate && VIDEO_ID_PATTERN.test(candidate) ? candidate : null;
}

export type ChannelRef = { handle: string } | { channelId: string };

/** Recognizes channel URLs such as youtube.com/@SteelHighway/live or youtube.com/channel/UC.... */
export function parseChannelRef(input: string): ChannelRef | null {
    const value = input.trim();
    if (!/youtube\.com/i.test(value)) return null;

    const url = toUrl(value);
    if (!url || !isYouTubeHost(url.hostname)) return null;

    const [first, second] = url.pathname.split('/').filter(Boolean);
    if (first?.startsWith('@') && first.length > 1) return { handle: decodeURIComponent(first) };
    if (first === 'channel' && second) return { channelId: second };
    return null;
}
