import { parseChannelRef, parseVideoId } from '../utils/webcamConfig';

const API_BASE = 'https://www.googleapis.com/youtube/v3';
const LOOKUP_CACHE_KEY = 'webcam-video-lookups';
const LOOKUP_CACHE_MS = 7 * 24 * 60 * 60 * 1000;

export interface FoundVideo {
    videoId: string;
    title?: string;
}

async function callApi(path: string, params: Record<string, string>) {
    const query = new URLSearchParams({ ...params, key: import.meta.env.VITE_YOUTUBE_API_KEY ?? '' });
    const res = await fetch(`${API_BASE}/${path}?${query}`);
    const data = await res.json();
    if (data.error) {
        throw new Error(data.error.message || `YouTube API request failed (${res.status}).`);
    }
    return data;
}

async function searchLive(params: Record<string, string>): Promise<FoundVideo | null> {
    const data = await callApi('search', { part: 'snippet', type: 'video', eventType: 'live', maxResults: '1', ...params });
    const item = data.items?.[0];
    return item?.id?.videoId ? { videoId: item.id.videoId, title: item.snippet?.title } : null;
}

/**
 * Finds the video for a web cam source: a video URL or ID is parsed directly, a channel URL
 * finds that channel's live stream, and anything else is searched for as a live stream.
 * Returns null when nothing is live and throws when the YouTube API reports an error.
 */
export async function findLiveVideo(source: string): Promise<FoundVideo | null> {
    const videoId = parseVideoId(source);
    if (videoId) return { videoId };

    const channel = parseChannelRef(source);
    if (channel) {
        let channelId = 'channelId' in channel ? channel.channelId : null;
        if (!channelId && 'handle' in channel) {
            const data = await callApi('channels', { part: 'id', forHandle: channel.handle });
            channelId = data.items?.[0]?.id ?? null;
            if (!channelId) return null;
        }
        return searchLive({ channelId: channelId! });
    }

    return searchLive({ q: source.trim() });
}

type LookupCache = Record<string, { videoId: string; time: number }>;

function readLookupCache(): LookupCache {
    try {
        return JSON.parse(localStorage.getItem(LOOKUP_CACHE_KEY) || '{}') as LookupCache;
    } catch {
        return {};
    }
}

/** Like findLiveVideo, but caches found IDs for a week and returns null instead of throwing. */
export async function resolveVideoId(source: string): Promise<string | null> {
    const key = source.trim();
    if (!key) return null;

    const cache = readLookupCache();
    const cached = cache[key];
    if (cached && Date.now() - cached.time < LOOKUP_CACHE_MS) return cached.videoId;

    try {
        const found = await findLiveVideo(key);
        if (!found) {
            console.warn(`No live stream found for "${key}".`);
            return null;
        }
        // Only successful lookups are cached so a stream that comes back online is found on the next visit.
        try {
            localStorage.setItem(LOOKUP_CACHE_KEY, JSON.stringify({ ...readLookupCache(), [key]: { videoId: found.videoId, time: Date.now() } }));
        } catch {
            // Storage can be unavailable in private windows; the lookup still succeeded.
        }
        return found.videoId;
    } catch (e) {
        console.warn(`YouTube lookup failed for "${key}":`, e);
        return null;
    }
}
