// ArrowIcon.tsx
import React from 'react';

export const ArrowIcon: React.FC<{
  iconSrc: string;
  brightness: number;
  trackColor?: string;
  size: number;
  rotation: number;
}> = ({ iconSrc, brightness, trackColor, size, rotation }) => (
  <div
    style={{
      width: size,
      height: size,
      position: 'relative',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: 'transparent',
    }}
  >
    {trackColor && (
      <svg
        style={{
          position: 'absolute',
          top: -3,
          left: -3,
          width: size + 6,
          height: size + 6,
          pointerEvents: 'none',
        }}
      >
        <circle
          cx={(size + 6) / 2}
          cy={(size + 6) / 2}
          r={(size + 6) / 2 - 1.5}
          fill="none"
          stroke={trackColor}
          strokeWidth="3"
          strokeDasharray="8 8"
        />
      </svg>
    )}
    <img
      src={iconSrc}
      alt="Train direction"
      style={{
        width: '100%',
        height: '100%',
        display: 'block',
        transform: `rotate(${rotation}deg)`,
        filter: `brightness(${brightness})`,
      }}
    />
  </div>
);