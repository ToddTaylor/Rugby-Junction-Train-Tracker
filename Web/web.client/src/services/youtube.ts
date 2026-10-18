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

interface SearchItem { id?: { videoId?: string } }
interface VideoItem { id: string; snippet?: { title?: string; liveBroadcastContent?: string } }

/**
 * Returns the first search result that is currently live. The search API's eventType=live filter
 * and its liveBroadcastContent field are stale for streams that have run for months, so this
 * searches without the filter and confirms live status with videos.list, which is current.
 */
async function searchLive(params: Record<string, string>, maxResults: number): Promise<FoundVideo | null> {
    const data = await callApi('search', { part: 'id', type: 'video', maxResults: String(maxResults), ...params });
    const ids = ((data.items ?? []) as SearchItem[])
        .map(item => item.id?.videoId)
        .filter((id): id is string => !!id);
    if (!ids.length) return null;

    const details = await callApi('videos', { part: 'snippet', id: ids.join(',') });
    const live = new Map(((details.items ?? []) as VideoItem[])
        .filter(video => video.snippet?.liveBroadcastContent === 'live')
        .map(video => [video.id, video.snippet?.title]));
    const videoId = ids.find(id => live.has(id));
    return videoId ? { videoId, title: live.get(videoId) } : null;
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
        return searchLive({ channelId: channelId! }, 50);
    }

    return searchLive({ q: source.trim() }, 5);
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
