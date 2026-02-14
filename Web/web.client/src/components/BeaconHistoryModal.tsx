import { useEffect, useState, useRef, useCallback } from 'react';
import {
    IconButton,
    Box,
    Typography,
    CircularProgress,
    Paper
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import { DataGrid, GridColDef, GridRenderCellParams } from '@mui/x-data-grid';
import { MapPinHistory } from '../types/MapPinHistory';
import { MapPin } from '../types/MapPin';
import { TrackedPin } from '../services/trackedPins';
import { format, parseISO } from 'date-fns';
import { getTrackedMapPins, updateTrackedPinSymbol, removeTrackedMapPin, getTrackedPinSymbol, refreshTrackedPinsFromApi, addTrackedMapPin } from '../services/trackedPins';
import { fetchBeaconHistory } from '../services/mapPinsHistory';
import TrackSymbolModal from './TrackSymbolModal';

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
}

export function BeaconHistoryModal({ open, onClose, beaconID, beaconName, subdivisionID, railroad: _railroad, subdivision: _subdivision, theme, lastUpdate, trackedPins: propTrackedPins }: BeaconHistoryModalProps) {
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
    const prevBeaconIDRef = useRef<string | null>(null);

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

    // If propTrackedPins is provided, always use it for rendering


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


    const columns: GridColDef[] = [
        { field: 'id', headerName: 'ID', width: 70 },
        {
            field: 'lastUpdate',
            headerName: 'Time',
            width: 85,
            valueFormatter: (params) => {
                try {
                    return format(parseISO(params as string), 'h:mm aa');
                } catch {
                    return params as string;
                }
            }
        },
        { 
            field: 'direction', 
            headerName: 'Direction', 
            width: 100,
            valueFormatter: (params) => {
                const dir = params as string;
                if (!dir) return '?';
                const directions: { [key: string]: string } = {
                    'N': 'North',
                    'S': 'South',
                    'E': 'East',
                    'W': 'West',
                    'NE': 'Northeast',
                    'NW': 'Northwest',
                    'SE': 'Southeast',
                    'SW': 'Southwest'
                };
                return directions[dir.toUpperCase()] || dir;
            }
        },
        {
            field: 'addresses',
            headerName: 'Addresses',
            flex: 1,
            minWidth: 150,
            maxWidth: 400,
            renderCell: (params: GridRenderCellParams<MapPinHistory>) => {
                const addresses = params.row?.addresses;

                if (!Array.isArray(addresses) || addresses.length === 0) return '';
                
                const addressText = addresses
                    .map((a: { source: string; addressID: number }) => `${a.addressID} ${a.source}`)
                    .join(', ');
                
                // Check if this train is currently tracked
                // Match history addresses with current MapPins, then check if those MapPins are tracked
                let isTracked = false;
                let trackedColor = undefined;
                let symbol = undefined;
                
                // Find if any address in history matches a tracked pin
                let matchedMapPinId: string | undefined;
                for (const addr of addresses) {
                    // Check if this address is in any tracked pin's addresses
                    const tracked = trackedPins.find(tp => 
                        Array.isArray(tp.addresses) && tp.addresses.some((a: { id: string; source: string }) => 
                            a.id === String(addr.addressID) && a.source === addr.source
                        )
                    );
                    if (tracked) {
                        isTracked = true;
                        trackedColor = tracked.color;
                        symbol = tracked.symbol;
                        matchedMapPinId = tracked.id;
                        break;
                    }
                }

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
                            cursor: 'pointer',
                            '&:hover': {
                                backgroundColor: 'rgba(255, 255, 255, 0.05)',
                            },
                        }}
                    >
                        {isTracked && trackedColor && (
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
                        )}
                        {!isTracked && (
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
                        )}
                        {symbol && (
                            <span 
                                onClick={handleSymbolClick}
                                style={{ 
                                    fontWeight: 'bold', 
                                    marginRight: '4px',
                                    color: trackedColor || '#FFD700',
                                    cursor: 'pointer',
                                    textDecoration: 'underline'
                                }}
                                title="Click to edit symbol"
                            >
                                {symbol}
                            </span>
                        )}
                        <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>
                            {addressText}
                        </span>
                    </Box>
                );
            },
        },
    ];

    const isDark = theme === 'dark';

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
                await removeTrackedMapPin(selectedMapPinId);
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
                            rowHeight={36}
                            columnHeaderHeight={0}
                            sx={{
                                height: history.length > 5 ? '260px' : `${(history.length * 36)}px`,
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
