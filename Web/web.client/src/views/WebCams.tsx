import React, { useEffect, useRef } from "react";
import "./WebCams.css"; // <-- Use component-scoped CSS

const videoNames = [
    'The Press Box LIVE Railcam - Fond du Lac, WI',
    'Slinger Depot Museum Live Railcam',
    'Bulldog Builders LIVE Railcam - Waukesha, WI',
    'Burlington, WI Dougs Auto Service Live Railcam',
    'Wauwatosa Historical Society Live Railcam',
    'Thirsty Bird Saloon LIVE Railcam - Oconomowoc, WI',
    'Julies Java House Live Railcam',
    'Wash Station LIVE Railcam - Wauwatosa (Butler), WI'
];

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

const players: any[] = [];

const WebCams: React.FC = () => {
    const containerRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        (window as any).onYouTubeIframeAPIReady = async () => {
            const container = containerRef.current;
            let videoData = getCachedVideos();

            if (!videoData) {
                videoData = await Promise.all(videoNames.map(async name => {
                    const videoId = await fetchVideoIdByName(name);
                    return { name, videoId };
                }));
                cacheVideos(videoData);
            }

            interface VideoData {
                name: string;
                videoId: string | null;
            }

            if (!videoData.some((v: VideoData) => v.videoId)) {
                if (container) {
                    container.innerHTML = "<div style='color:white;padding:2em;'>No live videos found for the configured railcams.</div>";
                }
            }

            videoData.forEach(({ videoId }: { videoId: string | null }, index: number) => {
                if (!videoId || !container) return;

                const block = createVideoBlock(index);
                container.appendChild(block);

                // Setup YouTube Player
                const player = new (window as any).YT.Player(`player-${index}`, {
                    videoId,
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
                            player.playVideo();
                            setTimeout(() => {
                                const state = player.getPlayerState();
                                if (state === (window as any).YT.PlayerState.PLAYING) {
                                    block.classList.add('playing');
                                }
                            }, 1000);

                            const playBtn = block.querySelector('.play-button');
                            playBtn?.addEventListener('click', () => {
                                player.playVideo();
                                block.classList.add('playing');
                            });
                        }
                    }
                });

                const playerObj = { player, disableLiveEdgeUntil: 0 };
                players.push(playerObj);
                setupPlayerControls(block, playerObj);
            });

            setInterval(() => players.forEach(p => checkLiveEdge(p)), 60000);
        };

        if (!(window as any).YT) {
            const tag = document.createElement("script");
            tag.src = "https://www.youtube.com/iframe_api";
            document.body.appendChild(tag);
        } else if ((window as any).YT && (window as any).YT.Player) {
            // API already loaded, call callback manually
            (window as any).onYouTubeIframeAPIReady();
        }

        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) players.forEach(p => checkLiveEdge(p));
        });

        // Helper functions
        function getCachedVideos() {
            const cached = localStorage.getItem('youtube-videos');
            const timestamp = parseInt(localStorage.getItem('youtube-videos-time') || "0", 10);
            const now = Date.now();
            return (cached && timestamp && now - timestamp < 7 * 24 * 60 * 60 * 1000)
                ? JSON.parse(cached)
                : null;
        }

        function cacheVideos(videos: any) {
            localStorage.setItem('youtube-videos', JSON.stringify(videos));
            localStorage.setItem('youtube-videos-time', Date.now().toString());
        }

        async function fetchVideoIdByName(query: string) {
            const apiKey = import.meta.env.VITE_YOUTUBE_API_KEY;
            const res = await fetch(`https://www.googleapis.com/youtube/v3/search?part=snippet&q=${encodeURIComponent(query)}&type=video&eventType=live&maxResults=1&key=${apiKey}`);
            const data = await res.json();
            const videoId = data.items?.[0]?.id?.videoId ?? null;
            console.log(`Query: ${query}, VideoId: ${videoId}`, data);
            return videoId;
        }

        function createVideoBlock(index: number) {
            const wrapper = document.createElement('div');
            wrapper.className = 'video-player-wrapper';
            wrapper.innerHTML = `
                <div id="player-${index}" class="player-frame"></div>
                <div class="overlay left">
                    ${createSVGButton('left')}
                    <div class="label">Rewind 15s</div>
                </div>
                <div class="overlay right">
                    ${createSVGButton('right')}
                    <div class="label">Forward 15s</div>
                </div>
                <button class="live-button">Go to Live</button>
                <button class="play-button">▶ Play</button>
            `;
            return wrapper;
        }

        function setupPlayerControls(container: HTMLElement, playerObj: any) {
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

        function seek(playerObj: any, seconds: number) {
            const { player } = playerObj;
            const time = player.getCurrentTime?.() || 0;
            player.seekTo(Math.max(0, time + seconds), true);
            playerObj.disableLiveEdgeUntil = Date.now() + 10000;
        }

        function checkLiveEdge(playerObj: any, force = false) {
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

        // Authenticate and load API
        //(window as any).authenticateUser();
    }, []);

    return (
        <div>
            <div className="video-container-wrapper" ref={containerRef}></div>
            <button id="logout" style={{ display: "none" }}>Logout</button>
            <div id="content" style={{ marginTop: 20 }}></div>
        </div>
    );
};

export default WebCams;