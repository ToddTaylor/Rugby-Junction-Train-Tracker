import React, { useEffect, useRef, useState } from "react";
import { SquarePen } from "lucide-react";
import HamburgerMenu from "../components/HamburgerMenu";
import WebCamEditor from "../components/WebCamEditor";
import { resolveVideoId } from "../services/youtube";
import { WebCamEntry, loadEntries, saveEntries } from "../utils/webcamConfig";
import "./WebCams.css"; // <-- Use component-scoped CSS

const ICON_CACHE_BUSTER = import.meta.env.VITE_APP_VERSION
    ? `?v=${import.meta.env.VITE_APP_VERSION}`
    : '';

// Players render at this fixed size and are scaled up with CSS to fill their tile. YouTube picks the
// stream resolution from the iframe's layout size, so a small player keeps it on a low-resolution stream.
// YouTube will not play in a player smaller than 200x200, so this is the smallest 16:9 size it allows.
const PLAYER_WIDTH = 356;
const PLAYER_HEIGHT = 200;

interface PlayerObj {
    player: any;
    disableLiveEdgeUntil: number;
    ready: boolean;
    inView: boolean;
}

function createSVGButton(direction: "left" | "right") {
    return `
        <svg class="svg-icon" viewBox="0 0 100 100" preserveAspectRatio="xMidYMid meet">
            <circle cx="50" cy="50" r="48" fill="red" />
            ${direction === "left"
                ? `
                    <polygon points="60,30 35,50 60,70" fill="white" />
                    <polygon points="45,30 20,50 45,70" fill="white" />
                `
                : `
                    <polygon points="40,30 65,50 40,70" fill="white" />
                    <polygon points="55,30 80,50 55,70" fill="white" />
                `}
        </svg>
    `;
}

let youTubeApiPromise: Promise<void> | null = null;

function loadYouTubeApi(): Promise<void> {
    if (!youTubeApiPromise) {
        youTubeApiPromise = new Promise(resolve => {
            const w = window as any;
            if (w.YT?.Player) {
                resolve();
                return;
            }
            const previous = w.onYouTubeIframeAPIReady;
            w.onYouTubeIframeAPIReady = () => {
                previous?.();
                resolve();
            };
            const tag = document.createElement("script");
            tag.src = "https://www.youtube.com/iframe_api";
            document.body.appendChild(tag);
        });
    }
    return youTubeApiPromise;
}

function createVideoBlock(videoId: string) {
    const wrapper = document.createElement('div');
    wrapper.className = 'video-player-wrapper';
    wrapper.innerHTML = `
        <div class="player-frame"></div>
        <div class="overlay left">
            ${createSVGButton('left')}
            <div class="label">Rewind 15s</div>
        </div>
        <div class="overlay right">
            ${createSVGButton('right')}
            <div class="label">Forward 15s</div>
        </div>
        <div class="video-actions">
            <button class="live-button">Go to Live</button>
            <button class="open-button" data-url="https://www.youtube.com/watch?v=${encodeURIComponent(videoId)}">New Window</button>
        </div>
        <button class="play-button">▶ Play</button>
    `;
    return wrapper;
}

function setupPlayerControls(container: HTMLElement, playerObj: PlayerObj) {
    const rewind = container.querySelector('.overlay.left');
    const forward = container.querySelector('.overlay.right');
    const liveBtn = container.querySelector('.live-button');

    const rewindAction = (e: any) => {
        e.preventDefault?.();
        seek(playerObj, -15);
        resetInactivityTimer(container);
    };
    rewind?.addEventListener('click', rewindAction);
    rewind?.addEventListener('touchstart', rewindAction);

    const forwardAction = (e: any) => {
        e.preventDefault?.();
        seek(playerObj, 15);
        resetInactivityTimer(container);
    };
    forward?.addEventListener('click', forwardAction);
    forward?.addEventListener('touchstart', forwardAction);

    const liveAction = (e: any) => {
        e.preventDefault?.();
        checkLiveEdge(playerObj, true);
        resetInactivityTimer(container);
    };
    liveBtn?.addEventListener('click', liveAction);
    liveBtn?.addEventListener('touchstart', liveAction);

    // Click only: browsers block windows opened from touchstart because it isn't a user activation.
    const openBtn = container.querySelector<HTMLElement>('.open-button');
    openBtn?.addEventListener('click', e => {
        e.preventDefault();
        window.open(openBtn.dataset.url, '_blank', 'noopener,popup');
    });

    let inactivityTimer: any;
    const resetInactivityTimer = (container: HTMLElement) => {
        clearTimeout(inactivityTimer);
        container.classList.remove('inactive');
        inactivityTimer = setTimeout(() => {
            container.classList.add('inactive');
        }, 2000);
    };

    container.addEventListener('touchstart', () => resetInactivityTimer(container), { passive: true });
    container.addEventListener('mousemove', () => resetInactivityTimer(container));
    resetInactivityTimer(container);

    container.addEventListener('mousemove', (e: MouseEvent) => {
        if (window.innerWidth <= 768) return;
        const rect = container.getBoundingClientRect();
        const midpoint = rect.left + rect.width / 2;
        container.classList.toggle('show-left', e.clientX < midpoint);
        container.classList.toggle('show-right', e.clientX >= midpoint);
    });

    container.addEventListener('mouseleave', () => {
        container.classList.remove('show-left', 'show-right');
    });
}

function seek(playerObj: PlayerObj, seconds: number) {
    const { player } = playerObj;
    const time = player.getCurrentTime?.() || 0;
    player.seekTo(Math.max(0, time + seconds), true);
    playerObj.disableLiveEdgeUntil = Date.now() + 10000;
}

// Play a player only while its tile is on screen and the tab is visible; jump back to live on resume.
function updatePlayback(playerObj: PlayerObj) {
    if (!playerObj.ready) return;
    const { player } = playerObj;
    const PlayerState = (window as any).YT.PlayerState;
    try {
        const state = player.getPlayerState();
        if (playerObj.inView && !document.hidden) {
            if (state !== PlayerState.PLAYING && state !== PlayerState.BUFFERING) {
                player.playVideo();
                // Only a previously paused stream has fallen behind live; seeking before the first play stalls it.
                if (state === PlayerState.PAUSED) checkLiveEdge(playerObj, true);
            }
        } else if (state === PlayerState.PLAYING || state === PlayerState.BUFFERING) {
            player.pauseVideo();
        }
    } catch (e) {
        console.warn('Playback update failed:', e);
    }
}

function checkLiveEdge(playerObj: PlayerObj, force = false) {
    if (!force && Date.now() < playerObj.disableLiveEdgeUntil) return;
    const player = playerObj.player;
    try {
        const current = player.getCurrentTime?.();
        const duration = player.getDuration?.();
        if (duration - current > 5) {
            player.seekTo(duration, true);
            if (player.getPlayerState() !== (window as any).YT.PlayerState.PLAYING) {
                player.playVideo();
            }
        }
    } catch (e) {
        console.warn('Live edge check failed:', e);
    }
}

const WebCams: React.FC = () => {
    const containerRef = useRef<HTMLDivElement>(null);
    const playersRef = useRef<PlayerObj[]>([]);
    const [entries, setEntries] = useState<WebCamEntry[]>(loadEntries);
    const [message, setMessage] = useState<string | null>(null);
    const [menuOpen, setMenuOpen] = useState(false);
    const [editorOpen, setEditorOpen] = useState(false);

    // Build a player for every configured web cam, rebuilding whenever the configuration changes.
    useEffect(() => {
        const container = containerRef.current;
        if (!container) return;

        let cancelled = false;
        const players: PlayerObj[] = [];
        const playerByBlock = new Map<Element, PlayerObj>();
        playersRef.current = players;

        const resizeObserver = new ResizeObserver(observed => {
            observed.forEach(entry => {
                (entry.target as HTMLElement).style.setProperty(
                    '--player-scale', String(entry.contentRect.width / PLAYER_WIDTH));
            });
        });
        const intersectionObserver = new IntersectionObserver(observed => {
            observed.forEach(entry => {
                const playerObj = playerByBlock.get(entry.target);
                if (!playerObj) return;
                playerObj.inView = entry.isIntersecting;
                updatePlayback(playerObj);
            });
        });

        (async () => {
            await loadYouTubeApi();
            const ids = await Promise.all(entries.map(e => e.videoId || resolveVideoId(e.source)));
            if (cancelled) return;

            const videoIds = ids.filter((id): id is string => !!id);
            setMessage(videoIds.length
                ? null
                : 'No live videos found for the configured web cams. Use "Edit web cams" to set video IDs.');

            videoIds.forEach(videoId => {
                const block = createVideoBlock(videoId);
                container.appendChild(block);

                const player = new (window as any).YT.Player(block.querySelector('.player-frame'), {
                    videoId,
                    width: PLAYER_WIDTH,
                    height: PLAYER_HEIGHT,
                    playerVars: {
                        autoplay: 1,
                        mute: 1,
                        controls: 0,
                        disablekb: 1,
                        modestbranding: 1,
                        rel: 0,
                        playsinline: 1
                    },
                    events: {
                        onReady: () => {
                            playerObj.ready = true;
                            updatePlayback(playerObj);

                            const playBtn = block.querySelector('.play-button');
                            playBtn?.addEventListener('click', () => {
                                player.playVideo();
                                block.classList.add('playing');
                            });
                        },
                        onStateChange: (e: any) => {
                            if (e.data === (window as any).YT.PlayerState.PLAYING) {
                                block.classList.add('playing');
                            }
                        }
                    }
                });

                const playerObj: PlayerObj = { player, disableLiveEdgeUntil: 0, ready: false, inView: false };
                players.push(playerObj);
                playerByBlock.set(block, playerObj);
                resizeObserver.observe(block);
                intersectionObserver.observe(block);
                setupPlayerControls(block, playerObj);
            });
        })();

        return () => {
            cancelled = true;
            resizeObserver.disconnect();
            intersectionObserver.disconnect();
            players.forEach(p => {
                try {
                    p.player.destroy?.();
                } catch {
                    // The player may not have finished initializing.
                }
            });
            playersRef.current = [];
            container.replaceChildren();
        };
    }, [entries]);

    useEffect(() => {
        const keepLive = () => playersRef.current.forEach(p => {
            if (p.ready && p.inView && !document.hidden) checkLiveEdge(p);
        });
        const onVisibilityChange = () => playersRef.current.forEach(updatePlayback);
        const interval = setInterval(keepLive, 60000);
        document.addEventListener('visibilitychange', onVisibilityChange);
        return () => {
            clearInterval(interval);
            document.removeEventListener('visibilitychange', onVisibilityChange);
        };
    }, []);

    const handleSave = (updated: WebCamEntry[]) => {
        setMessage(null);
        setEntries(saveEntries(updated));
    };

    const menuItems = [
        {
            icon: <SquarePen color="#fff" size={26} strokeWidth={1.75} aria-hidden="true" />,
            label: 'Edit Web Cams',
            onClick: () => setEditorOpen(true),
            visible: true,
        },
    ];

    return (
        <div>
            <div style={{ position: 'absolute', top: 14, right: 10, zIndex: 1000 }}>
                <button
                    style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 0 }}
                    onClick={() => setMenuOpen(v => !v)}
                    aria-label="Open menu"
                >
                    <img src={`/icons/hamburger.svg${ICON_CACHE_BUSTER}`} alt="Menu" style={{ width: 32, height: 32 }} />
                </button>
                <HamburgerMenu items={menuItems} open={menuOpen} onClose={() => setMenuOpen(false)} />
            </div>
            <WebCamEditor entries={entries} open={editorOpen} onSave={handleSave} onClose={() => setEditorOpen(false)} />
            {message && <div className="webcam-message">{message}</div>}
            <div className="video-container-wrapper" ref={containerRef}></div>
        </div>
    );
};

export default WebCams;
