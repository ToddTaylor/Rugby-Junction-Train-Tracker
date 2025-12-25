// ArrowIcon.tsx
import React from 'react';

export const UnknownIcon: React.FC<{
  iconSrc: string;
  brightness: number;
  trackColor?: string;
  size: number;
  isLocal?: boolean;
}> = ({ iconSrc, brightness, trackColor, size, isLocal }) => (
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
    {isLocal && (
      <div
        style={{
          position: 'absolute',
          bottom: -2,
          right: 0,
          width: 10,
          height: 10,
          backgroundColor: '#FFD700',
          borderRadius: '50%',
          border: '1px solid rgba(0, 0, 0, 0.5)',
          zIndex: 10,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: '9px',
          fontWeight: '900',
          color: '#000',
          lineHeight: '12px',
        }}
      >
        L
      </div>
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