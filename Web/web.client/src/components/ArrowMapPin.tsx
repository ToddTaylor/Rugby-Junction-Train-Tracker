import React from 'react';
import { Direction } from '../types/types';

interface ArrowMapPinProps {
    direction?: Direction;
    moving?: boolean;
}

const angleMap: Record<Direction, number> = {
    N: 0,
    NE: 45,
    E: 90,
    SE: 135,
    S: 180,
    SW: 225,
    W: 270,
    NW: 315,
};

const ArrowMapPin: React.FC<ArrowMapPinProps> = ({ direction, moving }) => {
    if (!direction) {
        return (
            <div style={{ textAlign: 'center' }}>
                <img
                    src="/icons/unknown.svg" // Use a neutral fallback icon
                    alt="No direction"
                />
            </div>
        );
    }

    const angle = angleMap[direction];

    const imageSrc = moving === true
        ? "/icons/arrow-green.svg" // Moving
        : moving === false
            ? "/icons/arrow-red.svg" // Stopped
            : "/icons/arrow.svg"; // Unknown state

    return (
        <div style={{ textAlign: 'center' }}>
            <img
                src={ imageSrc }
                alt={`Direction ${direction}`}
                style={{ transform: `rotate(${angle}deg)` }}
            />
        </div>
    );
};

export default ArrowMapPin;
