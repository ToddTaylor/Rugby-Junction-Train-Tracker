import React from 'react';
import { RailroadOwnerSummary } from '../utils/railroadOwners';

interface RailroadFilterPanelProps {
    open: boolean;
    owners: RailroadOwnerSummary[];
    selectedOwners: ReadonlySet<string>;
    onToggleOwner: (code: string) => void;
    onSelectAll: () => void;
    onSelectNone: () => void;
    onSelectDefault: () => void;
    onClose: () => void;
}

const linkButtonStyle: React.CSSProperties = {
    background: 'none',
    border: 'none',
    color: '#8ab4f8',
    cursor: 'pointer',
    fontSize: 14,
    padding: 0,
};

// Lets the user pick which railroads' tracks (RROWNER1/2/3) are highlighted. Other tracks stay on the map, dimmed.
const RailroadFilterPanel: React.FC<RailroadFilterPanelProps> = ({ open, owners, selectedOwners, onToggleOwner, onSelectAll, onSelectNone, onSelectDefault, onClose }) => {
    if (!open) return null;
    return (
        <div
            role="dialog"
            aria-label="Railroads"
            style={{
                position: 'absolute',
                top: 54,
                right: 0,
                zIndex: 1100,
                background: '#222',
                color: '#fff',
                borderRadius: 8,
                boxShadow: '0 2px 12px rgba(0,0,0,0.2)',
                padding: '12px 0',
                width: 280,
                maxWidth: 'calc(100vw - 20px)',
            }}
        >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 18px 8px' }}>
                <span style={{ fontSize: 16, fontWeight: 600 }}>Railroads</span>
                <button onClick={onClose} aria-label="Close railroads" style={{ ...linkButtonStyle, color: '#fff', fontSize: 20, lineHeight: 1 }}>×</button>
            </div>
            <div style={{ padding: '0 18px 8px', fontSize: 13, color: '#aaa' }}>
                Selected railroads are highlighted. Other tracks stay on the map, dimmed.
            </div>
            <div style={{ display: 'flex', gap: 16, padding: '0 18px 8px', borderBottom: '1px solid #333' }}>
                <button style={linkButtonStyle} onClick={onSelectAll}>All</button>
                <button style={linkButtonStyle} onClick={onSelectNone}>None</button>
                <button style={linkButtonStyle} onClick={onSelectDefault}>Default</button>
            </div>
            {owners.length === 0 ? (
                <div style={{ padding: '8px 18px', fontSize: 14, color: '#aaa' }}>Track data is still loading.</div>
            ) : (
                <div style={{ maxHeight: '55vh', overflowY: 'auto' }}>
                    {owners.map(owner => (
                        <label
                            key={owner.code}
                            style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '8px 18px', cursor: 'pointer', fontSize: 15 }}
                        >
                            <input
                                type="checkbox"
                                checked={selectedOwners.has(owner.code)}
                                onChange={() => onToggleOwner(owner.code)}
                                style={{ width: 18, height: 18, flexShrink: 0 }}
                            />
                            <span style={{ flex: 1 }}>
                                {owner.name}
                                {owner.name !== owner.code && <span style={{ color: '#aaa' }}> ({owner.code})</span>}
                            </span>
                            <span style={{ color: '#aaa', fontSize: 13 }}>{owner.segmentCount}</span>
                        </label>
                    ))}
                </div>
            )}
        </div>
    );
};

export default RailroadFilterPanel;
