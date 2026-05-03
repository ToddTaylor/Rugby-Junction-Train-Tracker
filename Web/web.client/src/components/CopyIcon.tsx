import React from 'react';

interface CopyIconProps {
    size?: number;
    color?: string;
}

export const CopyIcon: React.FC<CopyIconProps> = ({ size = 16, color = 'currentColor' }) => (
    <svg
        width={size}
        height={size}
        viewBox="0 0 16 16"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
        aria-hidden="true"
    >
        <rect x="5" y="2.5" width="8" height="10" rx="1.5" stroke={color} strokeWidth="1.25" />
        <path d="M3.5 10.5H3C2.44772 10.5 2 10.0523 2 9.5V4C2 3.44772 2.44772 3 3 3H8.5" stroke={color} strokeWidth="1.25" strokeLinecap="round" />
    </svg>
);
