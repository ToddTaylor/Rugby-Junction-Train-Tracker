import { useEffect, useState } from 'react';
import {
    IconButton,
    Box,
    Typography,
    CircularProgress,
    Paper
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { MapPinHistory } from '../types/MapPinHistory';
import { format, parseISO } from 'date-fns';
import { getTrackedMapPins, updateTrackedPinSymbol, removeTrackedMapPin, getTrackedPinSymbol } from '../services/trackedPins';
import TrackSymbolModal from './TrackSymbolModal';

interface BeaconHistoryModalProps {
    open: boolean;
    onClose: () => void;
    beaconID: string;
    beaconName: string;
    theme: 'dark' | 'light';
    lastUpdate?: string | null;
    mapPins?: any[];
}

export function BeaconHistoryModal({ open, onClose, beaconID, beaconName, theme, lastUpdate, mapPins = [] }: BeaconHistoryModalProps) {
    const [loading, setLoading] = useState(false);
    const [history, setHistory] = useState<MapPinHistory[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [modalOpen, setModalOpen] = useState(false);
    const [modalSymbol, setModalSymbol] = useState('');
    const [selectedMapPinId, setSelectedMapPinId] = useState<string | null>(null);

    useEffect(() => {
        if (open && beaconID) {
            fetchHistory();
        }
    }, [open, beaconID, lastUpdate]); // Added lastUpdate to trigger refresh on MapPin updates


    const fetchHistory = async () => {
        setLoading(true);
        setError(null);
        try {
            const apiUrl = `${import.meta.env.VITE_API_URL}/api/v1/MapPins/History/${beaconID}?limit=5`;
            const response = await fetch(apiUrl, {
                headers: {
                    'X-Api-Key': import.meta.env.VITE_API_KEY,
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error('Failed to fetch beacon history');
            }

            const { data } = await response.json();
            setHistory(data || []);
        } catch (err) {
            console.error('Error fetching beacon history:', err);
            setError('Failed to load beacon history. Please try again.');
        } finally {
            setLoading(false);
        }
    };

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
            renderCell: (params: any) => {
                const addresses = params.row?.addresses;
                const isLocal = params.row?.isLocal;
                if (!Array.isArray(addresses) || addresses.length === 0) return '';
                
                const addressText = addresses
                    .map((a: { source: string; addressID: number }) => `${a.addressID} ${a.source}`)
                    .join(', ');
                
                // Check if this train is currently tracked
                // Match history addresses with current MapPins, then check if those MapPins are tracked
                const trackedPins = getTrackedMapPins();
                let isTracked = false;
                let trackedColor = undefined;
                let symbol = undefined;
                
                // Find if any address in history matches a tracked MapPin
                let matchedMapPinId: string | undefined;
                for (const addr of addresses) {
                    const matchingMapPin = mapPins.find((mp: any) => 
                        mp.addresses?.some((a: any) => a.addressID === addr.addressID && a.source === addr.source)
                    );
                    if (matchingMapPin) {
                        const tracked = trackedPins.find(tp => String(tp.id) === String(matchingMapPin.id));
                        if (tracked) {
                            isTracked = true;
                            trackedColor = tracked.color;
                            symbol = tracked.symbol;
                            matchedMapPinId = String(matchingMapPin.id);
                            break;
                        }
                    }
                }

                const handleSymbolClick = (e: React.MouseEvent) => {
                    e.stopPropagation();
                    if (matchedMapPinId) {
                        const currentSymbol = getTrackedPinSymbol(matchedMapPinId) || '';
                        setSelectedMapPinId(matchedMapPinId);
                        setModalSymbol(currentSymbol);
                        setModalOpen(true);
                    }
                };
                
                return (
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
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
                            >
                                T
                            </Box>
                        )}
                        {isLocal && (
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
                            >
                                L
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

    const handleModalSave = (newSymbol: string) => {
        if (selectedMapPinId) {
            updateTrackedPinSymbol(selectedMapPinId, newSymbol);
            // Force refresh by triggering a re-fetch
            window.dispatchEvent(new Event('storage'));
        }
    };

    const handleModalUntrack = () => {
        if (selectedMapPinId) {
            removeTrackedMapPin(selectedMapPinId);
            // Force refresh by triggering a re-fetch
            window.dispatchEvent(new Event('storage'));
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
                    {history.length > 0 && (
                        <Typography variant="body2" sx={{ color: isDark ? '#b0b0b0' : '#666666', mt: 0.25 }}>
                            {history[0].railroad} - {history[0].subdivision} - MP {history[0].milepost}
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
                            rows={history}
                            columns={columns}
                            hideFooter
                            columnVisibilityModel={{
                                id: false,
                            }}
                            disableRowSelectionOnClick
                            disableColumnMenu
                            disableColumnSelector
                            disableDensitySelector
                            autoHeight
                            rowHeight={36}
                            columnHeaderHeight={40}
                            sx={{
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
                                    backgroundColor: isDark ? '#1a1a1a' : '#f5f5f5',
                                    borderBottom: isDark ? '2px solid #444' : '2px solid #ddd',
                                    minHeight: '40px !important',
                                    maxHeight: '40px !important',
                                },
                                '& .MuiDataGrid-columnHeader': {
                                    backgroundColor: isDark ? '#1a1a1a' : '#f5f5f5',
                                    color: isDark ? '#e0e0e0' : '#333333',
                                    padding: '0 8px',
                                },
                                '& .MuiDataGrid-columnHeaderTitle': {
                                    color: isDark ? '#e0e0e0' : '#333333',
                                    fontWeight: 600,
                                },
                                '& .MuiDataGrid-columnSeparator': {
                                    color: isDark ? '#444' : '#ddd',
                                },
                                '& .MuiDataGrid-sortIcon': {
                                    color: isDark ? '#e0e0e0' : '#333333',
                                    opacity: 1,
                                },
                                '& .MuiDataGrid-iconButtonContainer': {
                                    visibility: 'visible',
                                    width: 'auto',
                                },
                                '& .MuiDataGrid-menuIcon': {
                                    visibility: 'hidden',
                                    width: 0,
                                },
                                '& .MuiDataGrid-filler': {
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
                open={modalOpen}
                currentSymbol={modalSymbol}
                onSave={(symbol) => {
                    handleModalSave(symbol);
                    setModalOpen(false);
                }}
                onUntrack={() => {
                    handleModalUntrack();
                    setModalOpen(false);
                }}
                onClose={() => setModalOpen(false)}
                theme={theme}
                showUntrackButton={true}
            />
        </>
    );
}
