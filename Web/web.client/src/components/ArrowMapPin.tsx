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
        const unknownImageSrc = moving === true
            ? "/icons/unknown.svg" // Moving
            : moving === false
                ? "/icons/unknown-red.svg" // Stopped
                : "/icons/unknown.svg"; // Unknown state

        return (
            <div style={{ textAlign: 'center' }}>
                <img
                    src={unknownImageSrc}
                    alt="No direction"
                />
            </div>
        );
    }

    const angle = angleMap[direction];

    const arrowImageSrc = moving === true
        ? "/icons/arrow-green.svg" // Moving
        : moving === false
            ? "/icons/arrow-red.svg" // Stopped
            : "/icons/arrow.svg"; // Unknown state

    return (
        <div style={{ textAlign: 'center' }}>
            <img
                src={ arrowImageSrc }
                alt={`Direction ${direction}`}
                style={{ transform: `rotate(${angle}deg)` }}
            />
        </div>
    );
};

export default ArrowMapPin;
