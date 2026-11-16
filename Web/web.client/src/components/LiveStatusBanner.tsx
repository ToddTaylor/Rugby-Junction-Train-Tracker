import React, { useEffect, useState } from 'react';
import {
    LIVE_STATUS_BANNER_DELAY_MS,
    LIVE_STATUS_BANNER_TEXT,
    isLiveConnectionDown,
    type LiveConnectionStatus
} from '../services/liveConnection';

interface LiveStatusBannerProps {
    status: LiveConnectionStatus | null;
}

/**
 * Tells the user the map has stopped updating, so trains that have not moved are not mistaken for trains
 * standing still. Shown once the connection has been down for a few seconds and removed as soon as it is back.
 */
const LiveStatusBanner: React.FC<LiveStatusBannerProps> = ({ status }) => {
    const down = isLiveConnectionDown(status);
    const [visible, setVisible] = useState(false);

    useEffect(() => {
        if (!down) {
            setVisible(false);
            return;
        }

        const timer = setTimeout(() => setVisible(true), LIVE_STATUS_BANNER_DELAY_MS);
        return () => clearTimeout(timer);
    }, [down]);

    if (!visible) {
        return null;
    }

    return (
        <div
            role="status"
            aria-live="polite"
            style={{
                position: 'absolute',
                top: 12,
                left: '50%',
                transform: 'translateX(-50%)',
                background: 'rgba(146, 64, 14, 0.92)',
                color: '#fff',
                padding: '6px 14px',
                borderRadius: 6,
                zIndex: 900,
                fontSize: 14,
                letterSpacing: '0.3px',
                whiteSpace: 'nowrap',
                pointerEvents: 'none',
                boxShadow: '0 1px 6px rgba(0,0,0,0.35)'
            }}
        >
            {LIVE_STATUS_BANNER_TEXT}
        </div>
    );
};

export default LiveStatusBanner;
