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
      filter: `brightness(${brightness})`,
      border: `3px dotted ${trackColor ?? 'transparent'}`,
      borderRadius: '50%',
      boxSizing: 'border-box',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: 'transparent',
    }}
  >
    <img
      src={iconSrc}
      alt="Train direction"
      style={{
        width: '100%',
        height: '100%',
        display: 'block',
        transform: `rotate(${rotation}deg)`,
      }}
    />
  </div>
);