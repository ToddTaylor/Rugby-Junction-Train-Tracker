import React, { useState, useEffect } from 'react';

interface TrackSymbolModalProps {
    open: boolean;
    currentSymbol: string;
    onSave: (symbol: string) => Promise<void>;
    onUntrack: () => Promise<void>;
    onClose: () => void;
    theme?: 'dark' | 'light';
    showUntrackButton?: boolean;
    trainId?: string | null;
    mapPinId?: number;
    addresses?: Array<{id: string, source: string}>;
}

const TrackSymbolModal: React.FC<TrackSymbolModalProps> = ({
    open,
    currentSymbol,
    onSave,
    onUntrack,
    onClose,
    theme = 'light',
    showUntrackButton = true,
    addresses = []
}) => {
    const [symbol, setSymbol] = useState(currentSymbol);

    useEffect(() => {
        setSymbol(currentSymbol);
    }, [currentSymbol]);

    const handleSave = async () => {
        const trimmedSymbol = symbol.trim();
        try {
            await onSave(trimmedSymbol.toUpperCase().substring(0, 10));
            onClose();
        } catch (error) {
            console.error('Error saving symbol:', error);
            // Keep modal open on error
        }
    };

    const handleUntrack = async () => {
        try {
            await onUntrack();
            onClose();
        } catch (error) {
            console.error('Error untracking:', error);
            // Keep modal open on error
        }
    };

    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
        if (e.key === 'Enter') {
            handleSave();
        } else if (e.key === 'Escape') {
            onClose();
        }
    };

    if (!open) return null;

    // Theme-aware colors
    const isDark = theme === 'dark';
    const bgColor = isDark ? '#1a1a1a' : '#fff';
    const textColor = isDark ? '#eaf3ff' : '#333';
    const inputBgColor = isDark ? '#2a2a2a' : '#f9f9f9';
    const inputBorderColor = isDark ? '#444' : '#ddd';
    const inputTextColor = isDark ? '#eaf3ff' : '#333';
    const buttonBgCancel = isDark ? '#333' : '#f5f5f5';
    const buttonBgCancelHover = isDark ? '#444' : '#e0e0e0';
    const buttonTextCancel = isDark ? '#eaf3ff' : '#333';

    return (
        <div style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: isDark ? 'rgba(0, 0, 0, 0.7)' : 'rgba(0, 0, 0, 0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 10000,
            padding: '16px'
        }}>
            <div style={{
                backgroundColor: bgColor,
                borderRadius: '8px',
                padding: '24px',
                width: '100%',
                maxWidth: '400px',
                boxShadow: isDark ? '0 4px 12px rgba(0, 0, 0, 0.5)' : '0 4px 12px rgba(0, 0, 0, 0.2)',
                fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
                border: isDark ? '1px solid #333' : 'none'
            }}>
                <h2 style={{
                    margin: '0 0 16px 0',
                    fontSize: '18px',
                    fontWeight: 600,
                    color: textColor
                }}>
                    Enter a Symbol
                </h2>
                {addresses && addresses.length > 0 && (
                    <div style={{
                        marginBottom: '16px',
                        padding: '12px',
                        backgroundColor: isDark ? 'rgba(255, 255, 255, 0.03)' : 'rgba(0, 0, 0, 0.03)',
                        borderRadius: '4px',
                        fontSize: '13px',
                        color: isDark ? 'rgba(234, 243, 255, 0.7)' : 'rgba(0, 0, 0, 0.6)',
                        borderLeft: `3px solid ${isDark ? '#555' : '#ccc'}`
                    }}>
                        <strong style={{display: 'block', marginBottom: '8px', opacity: 0.8}}>Addresses:</strong>
                        {addresses.map((addr, idx) => (
                            <div key={idx} style={{marginBottom: idx < addresses.length - 1 ? '4px' : '0'}}>
                                <span>{addr.id}</span> <span style={{opacity: 0.7}}>({addr.source})</span>
                            </div>
                        ))}
                    </div>
                )}
                <input
                    type="text"
                    value={symbol}
                    onChange={(e) => setSymbol(e.target.value.toUpperCase().substring(0, 10))}
                    onKeyDown={handleKeyDown}
                    placeholder="Max 10 characters"
                    autoFocus
                    style={{
                        width: '100%',
                        padding: '10px 12px',
                        fontSize: '14px',
                        border: `1px solid ${inputBorderColor}`,
                        borderRadius: '4px',
                        boxSizing: 'border-box',
                        marginBottom: '16px',
                        fontFamily: 'inherit',
                        backgroundColor: inputBgColor,
                        color: inputTextColor,
                        transition: 'border-color 0.2s, box-shadow 0.2s'
                    }}
                    onFocus={(e) => {
                        e.currentTarget.style.borderColor = isDark ? '#666' : '#007bff';
                        e.currentTarget.style.boxShadow = isDark ? '0 0 4px rgba(100, 100, 100, 0.3)' : '0 0 4px rgba(0, 123, 255, 0.3)';
                    }}
                    onBlur={(e) => {
                        e.currentTarget.style.borderColor = inputBorderColor;
                        e.currentTarget.style.boxShadow = 'none';
                    }}
                />
                <div style={{
                    display: 'flex',
                    gap: '8px',
                    justifyContent: 'flex-end',
                    flexWrap: 'wrap-reverse'
                }}>
                    <button
                        onClick={onClose}
                        style={{
                            padding: '8px 16px',
                            fontSize: '14px',
                            border: `1px solid ${inputBorderColor}`,
                            borderRadius: '4px',
                            backgroundColor: buttonBgCancel,
                            color: buttonTextCancel,
                            cursor: 'pointer',
                            fontWeight: 500,
                            transition: 'background-color 0.2s',
                            flex: '1',
                            minWidth: '70px'
                        }}
                        onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = buttonBgCancelHover)}
                        onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = buttonBgCancel)}
                    >
                        Cancel
                    </button>
                    {showUntrackButton && (
                        <button
                            onClick={handleUntrack}
                            style={{
                                padding: '8px 16px',
                                fontSize: '14px',
                                border: '1px solid #dc3545',
                                borderRadius: '4px',
                                backgroundColor: '#dc3545',
                                color: '#fff',
                                cursor: 'pointer',
                                fontWeight: 500,
                                transition: 'background-color 0.2s',
                                flex: '1',
                                minWidth: '80px'
                            }}
                            onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = '#c82333')}
                            onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = '#dc3545')}
                        >
                            Untrack
                        </button>
                    )}
                    <button
                        onClick={handleSave}
                        style={{
                            padding: '8px 16px',
                            fontSize: '14px',
                            border: '1px solid #007bff',
                            borderRadius: '4px',
                            backgroundColor: '#007bff',
                            color: '#fff',
                            cursor: 'pointer',
                            fontWeight: 500,
                            transition: 'background-color 0.2s',
                            flex: '1',
                            minWidth: '80px'
                        }}
                        onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = '#0056b3')}
                        onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = '#007bff')}
                    >
                        Save
                    </button>
                </div>
            </div>
        </div>
    );
};

export default TrackSymbolModal;
