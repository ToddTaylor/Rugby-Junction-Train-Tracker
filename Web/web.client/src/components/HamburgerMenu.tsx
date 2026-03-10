import React from 'react';

interface MenuItem {
    icon: React.ReactNode;
    label: string;
    onClick: () => void;
    visible: boolean;
}

interface HamburgerMenuProps {
    items: MenuItem[];
    open: boolean;
    onClose: () => void;
}

const HamburgerMenu: React.FC<HamburgerMenuProps> = ({ items, open, onClose }) => {
    if (!open) return null;
    return (
        <div style={{
            position: 'absolute',
            top: 54, // move menu below hamburger icon
            right: 0, // right align to parent
            zIndex: 1100,
            background: '#222',
            borderRadius: 8,
            boxShadow: '0 2px 12px rgba(0,0,0,0.2)',
            padding: '12px 0',
            minWidth: 220,
        }}>
            {items.filter(i => i.visible).map((item, idx) => (
                <div
                    key={idx}
                    style={{
                        display: 'flex',
                        alignItems: 'center',
                        padding: '8px 18px',
                        cursor: 'pointer',
                        borderBottom: idx !== items.length - 1 ? '1px solid #333' : 'none',
                    }}
                    onClick={() => {
                        item.onClick();
                        onClose();
                    }}
                >
                    {item.icon}
                    <span style={{ marginLeft: 16, color: '#fff', fontSize: 16 }}>{item.label}</span>
                </div>
            ))}
        </div>
    );
};

export default HamburgerMenu;
