import React from 'react';
import { Direction } from '../types/types';

interface DirectionIconProps {
    direction?: Direction;
    useRotation?: boolean;
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

const DirectionIcon: React.FC<DirectionIconProps> = ({ direction, useRotation = false }) => {
    if (!direction) {
        return (
            <div style={{ textAlign: 'center' }}>
                <img
                    src="/icons/unknown.svg" // Use a neutral fallback icon
                    alt="No direction"
                    style={{ width: '40px', height: '40px', opacity: 0.5 }}
                />
                <div style={{ fontSize: '0.75rem', color: '#FFF' }}>Unknown</div>
            </div>
        );
    }

    const angle = angleMap[direction];

    return (
        <div style={{ textAlign: 'center' }}>
            <img
                src={useRotation ? '/icons/arrow.svg' : `/icons/arrow-${direction.toLowerCase()}.svg`}
                alt={`Direction ${direction}`}
                style={{
                    width: '40px',
                    height: '40px',
                    transform: useRotation ? `rotate(${angle}deg)` : 'none',
                }}
            />
            <div style={{ fontSize: '0.75rem', color: '#FFF' }}>{direction}</div>
        </div>
    );
};

export default DirectionIcon;
