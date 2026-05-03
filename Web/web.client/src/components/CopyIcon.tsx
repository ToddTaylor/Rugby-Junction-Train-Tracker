import React from 'react';

interface CopyIconProps {
    size?: number;
    color?: string;
}

export const CopyIcon: React.FC<CopyIconProps> = ({ size = 16, color = 'currentColor' }) => (
    <svg
        width={size}
        height={size}
        viewBox="0 0 24 24"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
        aria-hidden="true"
    >
        <circle cx="18" cy="5" r="3" stroke={color} strokeWidth="2" />
        <circle cx="6" cy="12" r="3" stroke={color} strokeWidth="2" />
        <circle cx="18" cy="19" r="3" stroke={color} strokeWidth="2" />
        <path d="M8.7 10.8L15.3 6.2" stroke={color} strokeWidth="2" strokeLinecap="round" />
        <path d="M8.7 13.2L15.3 17.8" stroke={color} strokeWidth="2" strokeLinecap="round" />
    </svg>
);
