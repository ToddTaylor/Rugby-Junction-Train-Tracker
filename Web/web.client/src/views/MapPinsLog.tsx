import { useCallback, useEffect, useState, useMemo } from "react";
import '../App.css';
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { Box, Typography, FormControl, InputLabel, Select, MenuItem, Checkbox, ListItemText } from '@mui/material';
import { MapPin } from '../types/MapPin';
import { format, parseISO } from "date-fns";
import { getTrackedMapPins, TrackedPin, updateTrackedPinSymbol, refreshTrackedPinsFromApi, applyTrackedPinAddedOrUpdatedFromServer, applyTrackedPinRemovedFromServer } from '../services/trackedPins';
import { useSignalR } from '../hooks/useSignalR';
import { removeMapPin, updateMapPins } from '../utils/updateHelpers';
import { sameTrackedPins } from '../utils/trackingDisplay';

function MapPinsLog() {
    const [mapPins, setMapPins] = useState<MapPin[]>([]);
    const [beaconNameFilter, setBeaconNameFilter] = useState<string[]>([]);
    const [trackedPins, setTrackedPinsRaw] = useState<TrackedPin[]>([]);
    // Keeps the old array when nothing changed, so re-reading the cache does not rebuild the grid.
    const setTrackedPins = useCallback((next: TrackedPin[]) => {
        setTrackedPinsRaw(prev => sameTrackedPins(prev, next) ? prev : next);
    }, []);

    const fetchMapPins = useCallback(async () => {
        try {
            const apiUrl = import.meta.env.VITE_API_URL + "/api/v1/MapPins";
            const response = await fetch(apiUrl, {
                headers: {
                    'X-Api-Key': import.meta.env.VITE_API_KEY,
                    'Content-Type': 'application/json'
                }
            });
            if (!response.ok) throw new Error('Failed to fetch map pins');

            const { data: pins } = await response.json();
            setMapPins(pins);
        } catch (error) {
            console.error('Error fetching map pins:', error);
        }
    }, []);

    useEffect(() => {
        fetchMapPins();
    }, [fetchMapPins]);

    // SignalR real-time updates
    useSignalR({
        // Updates sent while the connection was down are not replayed, so fetch the list again.
        Reconnected: fetchMapPins,
        MapPinUpdate: (mapPin: MapPin) => {
            setMapPins((prevPins: MapPin[]) => updateMapPins(prevPins, mapPin));
        },
        MapPinRemoved: (mapPinId: number) => {
            setMapPins((prevPins: MapPin[]) => removeMapPin(prevPins, mapPinId));
        },
        TrackedPinAdded: (payload: any) => {
            const updated = applyTrackedPinAddedOrUpdatedFromServer(payload);
            setTrackedPins(updated);
        },
        TrackedPinUpdated: (payload: any) => {
            const updated = applyTrackedPinAddedOrUpdatedFromServer(payload);
            setTrackedPins(updated);
        },
        TrackedPinRemoved: (mapPinId: number) => {
            const updated = applyTrackedPinRemovedFromServer(mapPinId);
            setTrackedPins(updated);
        }
    });

    // Track changes to tracked pins
    useEffect(() => {
        let disposed = false;

        const updateTrackedPins = () => {
            setTrackedPins(getTrackedMapPins());
        };

        // On mount, pull latest from API to hydrate local cache
        refreshTrackedPinsFromApi().then(pins => {
            if (!disposed) {
                setTrackedPins(pins);
            }
        }).catch(() => updateTrackedPins());

        // Also set immediate local state
        updateTrackedPins();
        
        // Listen for storage events (changes in other tabs/windows) - refresh from API to ensure sync
        const handleStorageChange = () => {
            refreshTrackedPinsFromApi().then(pins => {
                if (!disposed) {
                    setTrackedPins(pins);
                }
            }).catch(() => updateTrackedPins());
        };
        window.addEventListener('storage', handleStorageChange);

        // Refresh from API when tab becomes visible
        const handleVisibilityChange = () => {
            if (!document.hidden) {
                refreshTrackedPinsFromApi().then(pins => {
                    if (!disposed) {
                        setTrackedPins(pins);
                    }
                }).catch(() => updateTrackedPins());
            }
        };
        document.addEventListener('visibilitychange', handleVisibilityChange);
        
        // Re-read the cache every few seconds so expired tracked pins drop off. Same-tab changes made on this
        // page set the state directly, and changes made elsewhere arrive through SignalR or the storage event.
        const interval = setInterval(updateTrackedPins, 5000);

        // Also refresh from API every 10 seconds to ensure sync across devices
        const apiInterval = setInterval(() => {
            refreshTrackedPinsFromApi().then(pins => {
                if (!disposed) {
                    setTrackedPins(pins);
                }
            }).catch(() => updateTrackedPins());
        }, 10000);
        
        return () => {
            disposed = true;
            window.removeEventListener('storage', handleStorageChange);
            document.removeEventListener('visibilitychange', handleVisibilityChange);
            clearInterval(interval);
            clearInterval(apiInterval);
        };
    }, [setTrackedPins]);

    // Newest first. Sorts a copy, because sorting the state array in place would change it behind React's back.
    const sortedData: MapPin[] = useMemo(
        () => [...mapPins].sort((a, b) => new Date(b.lastUpdate).getTime() - new Date(a.lastUpdate).getTime()),
        [mapPins]);

    const columns: GridColDef[] = useMemo(() => [
        { field: 'id', headerName: 'ID' },
        {
            field: 'lastUpdate',
            headerName: 'Last Update',
            width: 110,
            valueFormatter: (params) => format(parseISO(params as string), 'h:mm aa'),
        },
        { field: 'beaconName', headerName: 'Beacon Name', width: 160 },
        { field: 'railroad', headerName: 'Railroad', width: 90 },
        { field: 'subdivision', headerName: 'Subdivision', width: 130 },
        { field: 'milepost', headerName: 'Milepost', width: 100 },
        {
            field: 'addresses',
            headerName: 'Addresses',
            width: 300,
            renderCell: (params: any) => {
                const addresses = params.row?.addresses;
                const isLocal = params.row?.isLocal;
                if (!Array.isArray(addresses)) return '';
                
                const addressText = addresses
                    .map((a: { source: string; addressID: number }) => `${a.addressID} ${a.source}`)
                    .join(', ');
                
                // Check if this train is currently tracked (using state)
                const tracked = trackedPins.find(tp => String(tp.id) === String(params.row.id));
                const isTracked = !!tracked;
                const trackedColor = tracked?.color;
                const symbol = tracked?.symbol;

                const handleSymbolClick = (e: React.MouseEvent) => {
                    e.stopPropagation();
                    const newSymbol = prompt('Enter a new Symbol (optional, max 10 characters, all caps):', symbol || '');
                    if (newSymbol !== null) {
                        const symbolToSave = newSymbol.trim().toUpperCase().substring(0, 10);
                        // The update returns the tracked pins as saved, so the new symbol shows once it is stored.
                        updateTrackedPinSymbol(String(params.row.id), symbolToSave)
                            .then(setTrackedPins)
                            .catch(error => console.error('Error updating tracked pin symbol:', error));
                    }
                };
                
                return (
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                        {isLocal ? (
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
                        ) : (
                            isTracked && trackedColor && (
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
                            )
                        )}
                        {symbol && (
                            <span 
                                onClick={handleSymbolClick}
                                style={{ 
                                    fontWeight: 'bold', 
                                    marginRight: '4px',
                                    color: '#0056b3',
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
        { field: 'direction', headerName: 'Direction', width: 100 },
        { field: 'moving', headerName: 'Moving', width: 90 },
    ], [trackedPins, setTrackedPins]);

    // Get unique beacon names for dropdown
    const beaconNames = Array.from(new Set(sortedData.map(row => row.beaconName).filter(Boolean))).sort();

    // Filter by beacon name(s) if any selected
    const filteredData: MapPin[] = beaconNameFilter.length > 0
        ? sortedData.filter(row => beaconNameFilter.includes(row.beaconName))
        : sortedData;

    return (
        <Box
            sx={{
                width: '100%',
                maxWidth: 1200,
                margin: '0 auto',
                padding: 4,
                boxSizing: 'border-box',
            }}
        >
            <Typography variant="h5" gutterBottom>
                Map Pins Log
            </Typography>
            <FormControl sx={{ minWidth: 220, mb: 2, backgroundColor: '#fff', borderRadius: 1, boxShadow: 1 }} size="small">
                <InputLabel
                    id="beacon-name-select-label"
                    sx={{
                        color: '#222',
                        backgroundColor: '#fff',
                        padding: '0 4px',
                        zIndex: 1,
                    }}
                >
                    Filter by Beacon Name
                </InputLabel>
                <Select
                    labelId="beacon-name-select-label"
                    multiple
                    value={beaconNameFilter}
                    label="Filter by Beacon Name"
                    onChange={e => {
                        const value = e.target.value;
                        setBeaconNameFilter(typeof value === 'string' ? value.split(',') : value);
                    }}
                    renderValue={(selected) =>
                        selected.length === 0 ? <em>All</em> : selected.join(', ')
                    }
                >
                    <MenuItem value="" disabled>
                        <em>All</em>
                    </MenuItem>
                    {beaconNames.map(name => (
                        <MenuItem key={name} value={name}>
                            <Checkbox checked={beaconNameFilter.indexOf(name) > -1} />
                            <ListItemText primary={name} />
                        </MenuItem>
                    ))}
                </Select>
            </FormControl>
            <DataGrid
                key={JSON.stringify(trackedPins.map(t => ({ id: t.id, symbol: t.symbol })))}
                rows={filteredData.slice(0, 10)}
                columns={columns}
                pageSizeOptions={[10]}
                hideFooter
                columnVisibilityModel={{
                    id: false,
                }}
                rowHeight={52}
                sx={{
                    height: filteredData.length > 5 ? 340 : 'auto',
                    minHeight: 300,
                    '& .MuiDataGrid-virtualScroller': {
                        overflowY: filteredData.length > 5 ? 'scroll' : 'visible',
                    },
                }}
            />
        </Box>
    );
}

export default MapPinsLog;