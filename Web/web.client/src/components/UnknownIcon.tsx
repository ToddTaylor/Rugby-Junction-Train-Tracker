// ArrowIcon.tsx
import React from 'react';

export const UnknownIcon: React.FC<{
  iconSrc: string;
  brightness: number;
  trackColor?: string;
  size: number;
}> = ({ iconSrc, brightness, trackColor, size }) => (
  <div
    style={{
      width: size,
      height: size,
      border: trackColor ? `3px dashed ${trackColor}` : 'none',
      borderRadius: '50%',
      boxSizing: 'border-box',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: 'transparent',
      textAlign: 'center',
    }}
  >
    <img
      src={iconSrc}
      alt="No direction"
      style={{
        width: '100%',
        height: '100%',
        display: 'block',
        filter: `brightness(${brightness})`,
      }}
    />
  </div>
);