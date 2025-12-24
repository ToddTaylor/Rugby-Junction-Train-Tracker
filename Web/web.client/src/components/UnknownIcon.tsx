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
      position: 'relative',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: 'transparent',
      textAlign: 'center',
    }}
  >
    {trackColor && (
      <div
        style={{
          position: 'absolute',
          top: 0,
          left: '50%',
          width: 10,
          height: 10,
          backgroundColor: trackColor,
          borderRadius: '50%',
          border: '1px solid rgba(0, 0, 0, 0.3)',
          zIndex: 10,
          animation: 'pulse 1.5s ease-in-out infinite',
          marginLeft: -6,
        }}
      />
    )}
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