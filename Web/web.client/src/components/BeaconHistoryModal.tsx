import { format, parseISO } from 'date-fns';
import { CopyIcon } from './CopyIcon';
import { useEffect, useState, useRef, useCallback } from 'react';
import { Typography, Box, IconButton, CircularProgress, Paper, Switch } from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import { DataGrid, GridColDef, GridRenderCellParams } from '@mui/x-data-grid';
import { MapPinHistory, AddressSnapshot } from '../types/MapPinHistory';
import { MapPin } from '../types/MapPin';
import { TrackedPin, getTrackedMapPins, refreshTrackedPinsFromApi, addTrackedMapPin, updateTrackedPinSymbol, removeTrackedMapPin, copyTrackedPinShareUrl, buildTrackedPinShareUrl, getTrackedPinSymbol } from '../services/trackedPins';
import { fetchBeaconHistory } from '../services/mapPinsHistory';
import { toggleSubdivisionLocalTrainAddress } from '../api/subdivisions';
import TrackSymbolModal from './TrackSymbolModal';

const DIRECTION_ABBREVIATIONS: { [key: string]: string } = {
    'N': 'N',
    'S': 'S',
    'E': 'E',
    'W': 'W',
    'NE': 'NE',
    'NW': 'NW',
    'SE': 'SE',
    'SW': 'SW',
};

export function formatDirectionAbbreviation(dir?: string | null): string {
    if (!dir) return '?';
    return DIRECTION_ABBREVIATIONS[dir.toUpperCase()] || dir;
}

// History rows don't carry an isActive flag per address (unlike live map pins),
// so the first address is always used, mirroring the pop-up's find(a => a.isActive) ?? addresses[0] fallback.
export function getPrimaryLocalToggleAddress(addresses?: AddressSnapshot[]): AddressSnapshot | undefined {
    return Array.isArray(addresses) ? addresses[0] : undefined;
}

interface BeaconHistoryModalProps {
    open: boolean;
    onClose: () => void;
    beaconID: string;
    beaconName: string;
    subdivisionID?: string;
    railroad?: string;
    subdivision?: string;
    theme: 'dark' | 'light';
    lastUpdate?: string | null;
    mapPins?: MapPin[];
    trackedPins?: TrackedPin[];
    hourFormat?: string;
    canViewSupportAddresses?: boolean;
    isAdmin?: boolean;
    isCustodian?: boolean;
    currentUserId?: number | null;
    onMapPinLocalStatusChanged?: (mapPinId: number, isLocal: boolean) => void;
}

export function BeaconHistoryModal({ open, onClose, beaconID, beaconName, subdivisionID, railroad: _railroad, subdivision: _subdivision, theme, lastUpdate, trackedPins: propTrackedPins, hourFormat, canViewSupportAddresses = false, isAdmin = false, isCustodian = false, currentUserId = null, onMapPinLocalStatusChanged }: BeaconHistoryModalProps) {
    const canManageLocalTrains = isAdmin || isCustodian;
    const [loading, setLoading] = useState(false);
    const [history, setHistory] = useState<MapPinHistory[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [modalOpen, setModalOpen] = useState(false);
    const [modalSymbol, setModalSymbol] = useState('');
    const [selectedMapPinId, setSelectedMapPinId] = useState<string | null>(null);
    const [modalAddresses, setModalAddresses] = useState<Array<{id: string, source: string}>>([]);
    const [isTrackingNew, setIsTrackingNew] = useState(false);
    const [trackingBeaconID, setTrackingBeaconID] = useState<string | undefined>(undefined);
    const [trackingSubdivisionID, setTrackingSubdivisionID] = useState<string | undefined>(undefined);
    const [selectedHistoryRecord, setSelectedHistoryRecord] = useState<MapPinHistory | null>(null);
    // Always use propTrackedPins if provided, else fallback to local state
    const [trackedPins, setTrackedPins] = useState(() => propTrackedPins || getTrackedMapPins());
    const [refreshKey, setRefreshKey] = useState(0);
    const [copiedShareCode, setCopiedShareCode] = useState<string | null>(null);
    const [togglingLocalRowIds, setTogglingLocalRowIds] = useState<Set<number>>(new Set());
    const [localToggleErrors, setLocalToggleErrors] = useState<{ [rowId: number]: string }>({});
    const prevBeaconIDRef = useRef<string | null>(null);
    const copyFeedbackTimeoutRef = useRef<number | null>(null);
    const localToggleFeedbackTimeoutsRef = useRef<{ [rowId: number]: number }>({});
    const copyIconColor = theme === 'dark' ? '#d5d9df' : '#4b5563';

    const fetchHistory = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const data = await fetchBeaconHistory(beaconID, subdivisionID, 10);
            setHistory(data || []);
        } catch (err) {
            console.error('Error fetching beacon history:', err);
            setError('Failed to load beacon history. Please try again.');
        } finally {
            setLoading(false);
        }
    }, [beaconID, subdivisionID]);

    // Update local state when prop changes
    useEffect(() => {
        if (propTrackedPins) {
            setTrackedPins(propTrackedPins);
            setRefreshKey(k => k + 1); // Force DataGrid re-render
        }
    }, [propTrackedPins]);

    useEffect(() => {
        const localToggleFeedbackTimeouts = localToggleFeedbackTimeoutsRef.current;
        return () => {
            if (copyFeedbackTimeoutRef.current) {
                window.clearTimeout(copyFeedbackTimeoutRef.current);
            }
            Object.values(localToggleFeedbackTimeouts).forEach(id => window.clearTimeout(id));
        };
    }, []);

    useEffect(() => {
        if (open && beaconID) {
            const prevBeaconID = prevBeaconIDRef.current;
            if (prevBeaconID !== beaconID) {
                setHistory([]); // Clear history when switching beacons
            }
            prevBeaconIDRef.current = beaconID;
            fetchHistory();
        } else if (!open) {
            setHistory([]); // Clear history when modal closes
        }
    }, [open, beaconID, subdivisionID, lastUpdate, fetchHistory]); // Re-fetch when lastUpdate changes

    // Periodic refresh of history every 30 seconds when modal is open
    useEffect(() => {
        if (!open || !beaconID) return;
        const interval = setInterval(() => {
            fetchHistory();
        }, 30000);
        return () => clearInterval(interval);
    }, [open, beaconID, subdivisionID, fetchHistory]);

    // Update tracked pins state and listen for changes (only if not using prop)
    useEffect(() => {
        const updateTrackedPins = () => {
            setTrackedPins(getTrackedMapPins());
        };

        // Initial load
        updateTrackedPins();


        // Listen for storage events (changes in other tabs/windows) - refresh from API
        const handleStorageChange = () => {
            refreshTrackedPinsFromApi().then(setTrackedPins).catch(updateTrackedPins);
        };
        window.addEventListener('storage', handleStorageChange);

        // Refresh from API when tab becomes visible
        const handleVisibilityChange = () => {
            if (!document.hidden) {
                refreshTrackedPinsFromApi().then(setTrackedPins).catch(updateTrackedPins);
            }
        };
        document.addEventListener('visibilitychange', handleVisibilityChange);

        // Poll for changes every 2 seconds (for local updates)
        const interval = setInterval(updateTrackedPins, 2000);

        return () => {
            window.removeEventListener('storage', handleStorageChange);
            document.removeEventListener('visibilitychange', handleVisibilityChange);
            clearInterval(interval);
        };
    }, [propTrackedPins]);

    const handleLocalToggle = useCallback(async (row: MapPinHistory, nextIsLocal: boolean) => {
        const primaryAddress = getPrimaryLocalToggleAddress(row.addresses);
        if (!primaryAddress) {
            return;
        }

        setLocalToggleErrors(prev => {
            if (!(row.id in prev)) return prev;
            const next = { ...prev };
            delete next[row.id];
            return next;
        });
        setTogglingLocalRowIds(prev => new Set(prev).add(row.id));
        // Optimistically flip the badge so the Train column updates immediately.
        setHistory(prev => prev.map(h => h.id === row.id ? { ...h, isLocal: nextIsLocal } : h));

        try {
            const result = await toggleSubdivisionLocalTrainAddress(
                Number(row.subdivisionID),
                Number(primaryAddress.addressID),
                { isAdmin, currentUserId }
            );

            if (result.errors.length > 0 || !result.data) {
                // Revert on rejection (e.g. a non-owning Custodian, enforced server-side).
                setHistory(prev => prev.map(h => h.id === row.id ? { ...h, isLocal: !nextIsLocal } : h));
                const message = result.errors[0] || 'Failed to update local status.';
                setLocalToggleErrors(prev => ({ ...prev, [row.id]: message }));
            } else {
                setHistory(prev => prev.map(h => h.id === row.id ? { ...h, isLocal: result.data!.isLocal } : h));
                // Patch the live map marker too - it's only otherwise refreshed by a fresh SignalR
                // telemetry push, which may not arrive for a while for a stationary train.
                const liveMapPinId = row.originalMapPinID ?? row.id;
                onMapPinLocalStatusChanged?.(Number(liveMapPinId), result.data.isLocal);
            }
        } catch (err) {
            console.error('Failed to toggle local status:', err);
            setHistory(prev => prev.map(h => h.id === row.id ? { ...h, isLocal: !nextIsLocal } : h));
            setLocalToggleErrors(prev => ({ ...prev, [row.id]: 'Failed to update local status.' }));
        } finally {
            setTogglingLocalRowIds(prev => {
                const next = new Set(prev);
                next.delete(row.id);
                return next;
            });

            if (localToggleFeedbackTimeoutsRef.current[row.id]) {
                window.clearTimeout(localToggleFeedbackTimeoutsRef.current[row.id]);
            }
            localToggleFeedbackTimeoutsRef.current[row.id] = window.setTimeout(() => {
                setLocalToggleErrors(prev => {
                    if (!(row.id in prev)) return prev;
                    const next = { ...prev };
                    delete next[row.id];
                    return next;
                });
                delete localToggleFeedbackTimeoutsRef.current[row.id];
            }, 4000);
        }
    }, [isAdmin, currentUserId, onMapPinLocalStatusChanged]);

    const columns: GridColDef[] = [
        { field: 'id', headerName: 'ID', width: 70 },
        {
            field: 'lastUpdate',
            headerName: 'Time',
            width: 72,
            valueFormatter: (params) => {
                try {
                    if (hourFormat === '12') {
                        return format(parseISO(params as string), 'h:mm aa');
                    } else {
                        return format(parseISO(params as string), 'HH:mm');
                    }
                } catch {
                    return params as string;
                }
            }
        },
        {
            field: 'direction',
            headerName: 'Direction',
            width: 44,
            valueFormatter: (params) => formatDirectionAbbreviation(params as string),
        },
        {
            field: 'addresses',
            headerName: 'Train',
            flex: 1,
            minWidth: 120,
            maxWidth: 400,
            renderCell: (params: GridRenderCellParams<MapPinHistory>) => {
                const addresses = Array.isArray(params.row?.addresses) ? params.row.addresses : [];
                const shareCode = params.row?.shareCode;
                
                // Build address text or DPU indicator based on viewing permission
                let addressText: string;
                if (canViewSupportAddresses) {
                    addressText = addresses
                        .map((a: { source: string; addressID: number }) => `${a.addressID} ${a.source}`)
                        .join(', ');
                } else {
                    // For Viewer role, show available source types (for example HOT/EOT/DPU).
                    addressText = Array.isArray(params.row.addressSourceTypes)
                        ? params.row.addressSourceTypes.join(' / ')
                        : '';
                }
                
                // Check if this train is currently tracked
                // Resolve to the best tracked pin candidate for this history row.
                // Prefer the row's original map pin ID, then beacon/subdivision-aligned matches,
                // then any address overlap as a final fallback.
                let isTracked = false;
                let trackedColor = undefined;
                
                let matchedMapPinId: string | undefined;
                const rowOriginalMapPinId = params.row?.originalMapPinID ? String(params.row.originalMapPinID) : undefined;
                const trackedByShareCode = shareCode
                    ? trackedPins.find(tp => tp.shareCode === shareCode)
                    : undefined;

                const addressMatchedTrackedPins = trackedPins.filter(tp =>
                    addresses.length > 0 && Array.isArray(tp.addresses) && tp.addresses.some((a: { id: string; source: string }) =>
                        addresses.some((addr: { source: string; addressID: number }) =>
                            a.id === String(addr.addressID) && a.source === addr.source
                        )
                    )
                );

                const trackedByOriginalMapPinId = rowOriginalMapPinId
                    ? trackedPins.find(tp => String(tp.id) === rowOriginalMapPinId)
                    : undefined;

                const trackedAtCurrentBeacon = addressMatchedTrackedPins.find(tp =>
                    String(tp.lastBeaconID || '') === String(beaconID || '') &&
                    String(tp.lastSubdivisionID || '') === String(subdivisionID || '')
                );

                const bestTrackedPin =
                    trackedByOriginalMapPinId ??
                    trackedByShareCode ??
                    trackedAtCurrentBeacon ??
                    addressMatchedTrackedPins[0];

                if (bestTrackedPin) {
                    isTracked = true;
                    trackedColor = bestTrackedPin.color;
                    matchedMapPinId = bestTrackedPin.id;
                }

                const trackedSymbol = bestTrackedPin?.symbol?.trim();
                const trainLabel = trackedSymbol || (shareCode ? `Train ${shareCode}` : (addressText || 'Train'));

                const handleSymbolClick = (e: React.MouseEvent) => {
                    e.stopPropagation();
                    if (matchedMapPinId) {
                        const currentSymbol = getTrackedPinSymbol(matchedMapPinId) || '';
                        const addressList = addresses.map((a: { source: string; addressID: number }) => ({id: String(a.addressID), source: a.source}));
                        setSelectedMapPinId(matchedMapPinId);
                        setModalSymbol(currentSymbol);
                        setModalAddresses(addressList);
                        setIsTrackingNew(false);
                        setModalOpen(true);
                    }
                };

                const handleTrackClick = (e: React.MouseEvent) => {
                    e.stopPropagation();
                    const addressList = addresses.map((a: { source: string; addressID: number }) => ({id: String(a.addressID), source: a.source}));
                    setSelectedMapPinId(null); // Clear for new tracking
                    setModalSymbol('');
                    setModalAddresses(addressList);
                    setTrackingBeaconID(beaconID);
                    setTrackingSubdivisionID(subdivisionID);
                    setSelectedHistoryRecord(params.row); // Store the exact history record that was clicked
                    setIsTrackingNew(true);
                    setModalOpen(true);
                };

                const handleAddressClick = (e: React.MouseEvent) => {
                    e.stopPropagation();
                    // If tracked, edit the symbol; if untracked, track it
                    if (isTracked && matchedMapPinId) {
                        handleSymbolClick(e);
                    } else if (!isTracked) {
                        handleTrackClick(e);
                    }
                };
                
                return (
                    <Box 
                        onClick={handleAddressClick}
                        sx={{ 
                            display: 'flex', 
                            alignItems: 'center', 
                            gap: 0.5,
                            width: '100%',
                            height: '100%',
                            cursor: 'pointer',
                            '&:hover': {
                                backgroundColor: 'rgba(255, 255, 255, 0.05)',
                            },
                        }}
                    >
                        {params.row.isLocal ? (
                            <Box
                                sx={{
                                    width: 14,
                                    height: 14,
                                    backgroundColor: '#FFD700',
                                    borderRadius: '50%',
                                    border: '1px solid rgba(0, 0, 0, 0.5)',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    fontSize: '9px',
                                    fontWeight: '900',
                                    color: '#000',
                                    flexShrink: 0,
                                }}
                                title="Local Train"
                            >
                                L
                            </Box>
                        ) : (
                            isTracked && trackedColor ? (
                                <Box
                                    sx={{
                                        width: 14,
                                        height: 14,
                                        backgroundColor: trackedColor,
                                        borderRadius: '50%',
                                        border: '1px solid rgba(0, 0, 0, 0.5)',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        fontSize: '9px',
                                        fontWeight: '900',
                                        color: '#000',
                                        flexShrink: 0,
                                    }}
                                    title="Tracked"
                                >
                                    T
                                </Box>
                            ) : (
                                <Box
                                    onClick={handleTrackClick}
                                    sx={{
                                        width: 14,
                                        height: 14,
                                        backgroundColor: '#cccccc',
                                        borderRadius: '50%',
                                        border: '1px solid rgba(0, 0, 0, 0.3)',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        fontSize: '9px',
                                        fontWeight: '900',
                                        color: '#666',
                                        flexShrink: 0,
                                        cursor: 'pointer',
                                    }}
                                    title="Track this train"
                                >
                                    T
                                </Box>
                            )
                        )}
                        <Box
                            sx={{
                                minWidth: 0,
                                display: 'flex',
                                flexDirection: 'column',
                                justifyContent: 'center',
                                lineHeight: 1.2,
                            }}
                        >
                            <span
                                onClick={handleAddressClick}
                                style={{
                                    whiteSpace: 'nowrap',
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    color: isTracked ? (trackedColor || '#FFD700') : 'inherit',
                                    cursor: 'pointer',
                                    textDecoration: isTracked ? 'underline' : 'none',
                                    fontWeight: isTracked ? 700 : 400,
                                }}
                                title={isTracked ? 'Click to edit tracked train' : 'Click to track this train'}
                            >
                                {trainLabel}
                            </span>
                            {addressText && (canViewSupportAddresses ? !!shareCode : true) && (
                                <span style={{ display: 'block', fontSize: '11px', opacity: 0.75, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', marginTop: '2px' }}>
                                    {addressText}
                                </span>
                            )}
                        </Box>
                    </Box>
                );
            },
        },
        {
            field: 'isLocal',
            headerName: 'Local',
            width: 52,
            sortable: false,
            filterable: false,
            renderCell: (params: GridRenderCellParams<MapPinHistory>) => {
                if (!canManageLocalTrains) {
                    return null;
                }

                const row = params.row;
                const primaryAddress = getPrimaryLocalToggleAddress(row.addresses);
                const canToggle = !!primaryAddress && Number.isInteger(Number(row.subdivisionID)) && Number(row.subdivisionID) > 0;
                const toggleError = localToggleErrors[row.id];

                return (
                    <Box
                        onClick={(e) => e.stopPropagation()}
                        sx={{ display: 'flex', alignItems: 'center', width: '100%', height: '100%', position: 'relative' }}
                    >
                        <Switch
                            size="small"
                            checked={!!row.isLocal}
                            disabled={!canToggle || togglingLocalRowIds.has(row.id)}
                            onChange={(e) => handleLocalToggle(row, e.target.checked)}
                            title={row.isLocal ? 'Remove local status' : 'Mark as local'}
                        />
                        {toggleError && (
                            <span
                                title={toggleError}
                                style={{
                                    position: 'absolute',
                                    left: 'calc(100% + 4px)',
                                    top: '50%',
                                    transform: 'translateY(-50%)',
                                    fontSize: '11px',
                                    lineHeight: 1,
                                    color: '#7f1d1d',
                                    background: 'rgba(254, 226, 226, 0.95)',
                                    border: '1px solid rgba(248, 113, 113, 0.45)',
                                    borderRadius: '9999px',
                                    padding: '3px 6px',
                                    whiteSpace: 'nowrap',
                                    pointerEvents: 'none',
                                    zIndex: 1,
                                }}
                            >
                                Failed
                            </span>
                        )}
                    </Box>
                );
            },
        },
        {
            field: 'shareCode',
            headerName: 'Share',
            width: 62,
            sortable: false,
            filterable: false,
            renderCell: (params: GridRenderCellParams<MapPinHistory>) => {
                if (!params.row?.shareCode) {
                    return '';
                }

                return (
                    <Box
                        onClick={async (e) => {
                            e.stopPropagation();
                            const shareUrl = buildTrackedPinShareUrl(params.row.shareCode!);
                            const shareTitle = params.row.shareCode ? `Train ${params.row.shareCode}` : (params.row.beaconName || 'Train');
                            // Try Web Share API first
                            if (navigator.share) {
                                try {
                                    await navigator.share({
                                        url: shareUrl,
                                        title: shareTitle,
                                        text: `Check out this train on Rugby Junction: ${shareTitle}`
                                    });
                                    setCopiedShareCode(params.row.shareCode!);
                                    if (copyFeedbackTimeoutRef.current) {
                                        window.clearTimeout(copyFeedbackTimeoutRef.current);
                                    }
                                    copyFeedbackTimeoutRef.current = window.setTimeout(() => {
                                        setCopiedShareCode(null);
                                    }, 3000);
                                    return;
                                } catch (err) {
                                    // If user cancels or share fails, fall through to copy
                                    console.warn('Web Share API failed or was cancelled, falling back to copy.', err);
                                }
                            }
                            // Fallback: copy to clipboard
                            try {
                                await copyTrackedPinShareUrl(params.row.shareCode!);
                                setCopiedShareCode(params.row.shareCode!);
                                if (copyFeedbackTimeoutRef.current) {
                                    window.clearTimeout(copyFeedbackTimeoutRef.current);
                                }
                                copyFeedbackTimeoutRef.current = window.setTimeout(() => {
                                    setCopiedShareCode(null);
                                }, 3000);
                            } catch (error) {
                                console.error('Failed to copy share link:', error);
                            }
                        }}
                        sx={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            width: '100%',
                            height: '100%',
                            cursor: 'pointer',
                            color: copyIconColor,
                            position: 'relative',
                            overflow: 'visible',
                        }}
                        title={`Share link for ${params.row.shareCode}`}
                    >
                        <CopyIcon size={16} color={copyIconColor} />
                        {copiedShareCode === params.row.shareCode && (
                            <span
                                className="copied-feedback-badge"
                                style={{
                                    display: 'inline-flex',
                                    alignItems: 'center',
                                    gap: '4px',
                                    fontSize: '11px',
                                    lineHeight: 1,
                                    color: '#14532d',
                                    background: 'rgba(220, 252, 231, 0.95)',
                                    border: '1px solid rgba(34, 197, 94, 0.35)',
                                    borderRadius: '9999px',
                                    padding: '3px 6px',
                                    position: 'absolute',
                                    left: 'calc(100% + 4px)',
                                    top: '50%',
                                    transform: 'translateY(-50%)',
                                    whiteSpace: 'nowrap',
                                    pointerEvents: 'none',
                                    zIndex: 1,
                                }}
                            >
                                <span
                                    style={{
                                        width: '12px',
                                        height: '12px',
                                        borderRadius: '9999px',
                                        background: '#16a34a',
                                        color: '#ffffff',
                                        display: 'inline-flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        fontSize: '9px',
                                        fontWeight: 700,
                                    }}
                                >
                                    ✓
                                </span>
                                Shared!
                            </span>
                        )}
                    </Box>
                );
            }
        },
    ];

    const isDark = theme === 'dark';

    useEffect(() => {
        const styleId = 'copied-feedback-touch-style';
        if (document.getElementById(styleId)) {
            return;
        }

        const style = document.createElement('style');
        style.id = styleId;
        style.innerHTML = `
            @media (pointer: coarse) {
                .copied-feedback-badge {
                    font-size: 12px !important;
                    padding: 4px 8px !important;
                }

                .copied-feedback-badge > span {
                    width: 14px !important;
                    height: 14px !important;
                    font-size: 10px !important;
                }
            }
        `;

        document.head.appendChild(style);

        return () => {
            style.remove();
        };
    }, []);

    const handleModalSave = async (newSymbol: string) => {
        if (isTrackingNew && selectedHistoryRecord) {
            // New tracking mode - use the exact history record that was selected
            try {
                // Use originalMapPinID if available, otherwise fall back to id
                const mapPinId = selectedHistoryRecord.originalMapPinID || selectedHistoryRecord.id;
                await addTrackedMapPin(
                    String(mapPinId),
                    trackingBeaconID || String(selectedHistoryRecord.beaconID),
                    trackingSubdivisionID || String(selectedHistoryRecord.subdivisionID),
                    beaconName || selectedHistoryRecord.beaconName,
                    newSymbol
                );
                // Immediately refresh tracked pins from API to update the grid
                const updatedPins = await refreshTrackedPinsFromApi();
                if (!propTrackedPins) {
                    setTrackedPins(updatedPins);
                }
                setRefreshKey(k => k + 1); // Force DataGrid re-render
            } catch (error) {
                console.error('Failed to track train:', error);
                throw error; // Re-throw to keep modal open
            }
        } else if (selectedMapPinId) {
            // Edit mode - update existing tracked pin
            try {
                await updateTrackedPinSymbol(selectedMapPinId, newSymbol);
                // Immediately refresh tracked pins from API to update the grid
                const updatedPins = await refreshTrackedPinsFromApi();
                if (!propTrackedPins) {
                    setTrackedPins(updatedPins);
                }
                setRefreshKey(k => k + 1); // Force DataGrid re-render
            } catch (error) {
                console.error('Failed to update symbol:', error);
                throw error; // Re-throw to keep modal open
            }
        }
    };

    const handleModalUntrack = async () => {
        if (selectedMapPinId) {
            try {
                const selectedAddressSet = new Set(
                    (modalAddresses || []).map(addr => `${addr.id}|${addr.source}`)
                );

                const overlappingTrackedIds = trackedPins
                    .filter(tp =>
                        Array.isArray(tp.addresses) &&
                        tp.addresses.some(addr => selectedAddressSet.has(`${addr.id}|${addr.source}`))
                    )
                    .map(tp => tp.id);

                const idsToRemove = Array.from(new Set([selectedMapPinId, ...overlappingTrackedIds]));

                for (const trackedId of idsToRemove) {
                    await removeTrackedMapPin(trackedId);
                }
                // Immediately refresh tracked pins from API to update the grid
                const updatedPins = await refreshTrackedPinsFromApi();
                if (!propTrackedPins) {
                    setTrackedPins(updatedPins);
                }
                setRefreshKey(k => k + 1); // Force DataGrid re-render
            } catch (error) {
                console.error('Failed to untrack:', error);
                throw error; // Re-throw to keep modal open
            }
        }
    };
    
    if (!open) return null;
    
    return (
        <>
            <Paper
                elevation={8}
                sx={{
                    position: 'fixed',
                    bottom: 0,
                    left: 0,
                    right: { xs: 0, md: 'auto' },
                    width: { xs: '100%', md: '450px' },
                maxHeight: '40vh',
                zIndex: 1000,
                backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                color: isDark ? '#e0e0e0' : '#333333',
                display: 'flex',
                flexDirection: 'column',
            }}
        >
            <Box sx={{ 
                p: 1.5, 
                display: 'flex', 
                alignItems: 'center', 
                justifyContent: 'space-between',
                backgroundColor: isDark ? '#1a1a1a' : '#f5f5f5',
                color: isDark ? '#e0e0e0' : '#333333',
                borderBottom: isDark ? '2px solid #444' : '2px solid #ddd',
            }}>
                <Box>
                    <Typography variant="h6" sx={{ color: isDark ? '#e0e0e0' : '#333333' }}>
                        {beaconName}
                    </Typography>
                    {(history.length > 0 || _railroad || _subdivision) && (
                        <Typography variant="body2" sx={{ color: isDark ? '#b0b0b0' : '#666666', mt: 0.25 }}>
                            {history.length > 0 
                                ? `${history[0].railroad} - ${history[0].subdivision} - MP ${history[0].milepost}`
                                : [_railroad, _subdivision].filter(Boolean).join(' - ')}
                        </Typography>
                    )}
                </Box>
                <IconButton
                    aria-label="close"
                    onClick={onClose}
                    sx={{
                        color: isDark ? '#e0e0e0' : '#666666',
                    }}
                >
                    <CloseIcon />
                </IconButton>
            </Box>
            <Box 
                sx={{
                    flex: 1,
                    overflow: 'auto',
                    px: 1,
                    pt: 1,
                    pb: 0,
                    backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                    minHeight: '240px',
                    position: 'relative',
                }}
            >
                {loading && history.length > 0 && (
                    <Box 
                        sx={{
                            position: 'absolute',
                            top: 8,
                            right: 8,
                            zIndex: 10,
                        }}
                    >
                        <CircularProgress size={24} />
                    </Box>
                )}
                
                {loading && history.length === 0 && (
                    <Box display="flex" justifyContent="center" alignItems="center" height="240px">
                        <CircularProgress />
                    </Box>
                )}
                
                {!loading && error && (
                    <Box display="flex" justifyContent="center" alignItems="center" height="240px">
                        <Typography color="error">{error}</Typography>
                    </Box>
                )}

                {!loading && !error && history.length === 0 && (
                    <Box display="flex" justifyContent="center" alignItems="center" height="240px">
                        <Typography color="textSecondary">No history available for this beacon.</Typography>
                    </Box>
                )}

                {history.length > 0 && (
                    <Box sx={{ width: '100%' }}>
                        <DataGrid
                            key={`${history.length}-${refreshKey}`} // Force re-render when history or tracked pins change
                            rows={history.slice(0, 10)}
                            columns={columns}
                            hideFooter
                            columnVisibilityModel={{
                                id: false,
                            }}
                            disableRowSelectionOnClick
                            disableColumnMenu
                            disableColumnSelector
                            disableDensitySelector
                            rowHeight={56}
                            columnHeaderHeight={0}
                            sx={{
                                height: history.length > 5 ? '260px' : `${(history.length * 56)}px`,
                                width: '100%',
                                backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                                color: isDark ? '#ccc' : '#333333',
                                border: 'none',
                                '& .MuiDataGrid-root': {
                                    border: 'none',
                                },
                                '& .MuiDataGrid-main': {
                                    backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                                },
                                '& .MuiDataGrid-overlay': {
                                    backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                                },
                                '& .MuiDataGrid-cell': {
                                    borderBottom: isDark ? '1px solid #333' : '1px solid #e0e0e0',
                                    color: isDark ? '#ccc' : '#333333',
                                    padding: '0 8px',
                                    display: 'flex',
                                    alignItems: 'center',
                                },
                                '& .MuiDataGrid-columnHeaders': {
                                    display: 'none',
                                },
                                '& .MuiDataGrid-scrollbar': {
                                    backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                                },
                                '& .MuiDataGrid-scrollbar--vertical': {
                                    backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                                },
                                '& .MuiDataGrid-scrollbar--horizontal': {
                                    backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                                },
                                '& .MuiDataGrid-row': {
                                    '&:hover': {
                                        backgroundColor: isDark ? '#252525' : '#f9f9f9',
                                    },
                                },
                                '& .MuiDataGrid-virtualScroller': {
                                    backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                                    overflowY: history.length > 5 ? 'scroll !important' : 'auto',
                                    overflowX: 'hidden !important',
                                    msOverflowStyle: 'scrollbar',
                                    scrollbarWidth: 'thin',
                                },
                                '& .MuiDataGrid-virtualScrollerContent': {
                                    backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                                },
                                '& .MuiDataGrid-virtualScrollerRenderZone': {
                                    backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                                },
                                '& .MuiDataGrid-footerContainer': {
                                    backgroundColor: isDark ? '#2a2a2a' : '#ffffff',
                                    borderTop: isDark ? '1px solid #333' : '1px solid #e0e0e0',
                                },
                            }}
                        />
                    </Box>
                )}
            </Box>
            </Paper>
            <TrackSymbolModal
                key={`${isTrackingNew}-${selectedHistoryRecord?.id || selectedMapPinId}`}
                open={modalOpen}
                currentSymbol={modalSymbol}
                onSave={handleModalSave}
                onUntrack={handleModalUntrack}
                onClose={() => {
                    setModalOpen(false);
                    setModalSymbol('');
                    setModalAddresses([]);
                    setSelectedMapPinId(null);
                    setSelectedHistoryRecord(null);
                    setIsTrackingNew(false);
                    setTrackingBeaconID(undefined);
                    setTrackingSubdivisionID(undefined);
                }}
                theme={theme}
                showUntrackButton={!isTrackingNew}
                addresses={modalAddresses}
                isTrackingNew={isTrackingNew}
            />
        </>
    );
}
