import { useEffect, useState } from "react";
import './Admin.css';
import '../App.css';
import './TelemetryLog.css';
import './AdminSubdivisions.css';
import './AdminTelemetryLog.css';
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { Box, TextField, FormControl, InputLabel, Select, MenuItem, IconButton, Tooltip, Checkbox, ListItemText } from '@mui/material';
import ClearIcon from '@mui/icons-material/Clear';
import RefreshIcon from '@mui/icons-material/Refresh';
import { Telemetry } from '../types/Telemetry';
import { format, parseISO } from "date-fns";

function AdminTelemetryLog() {
    const [telemetries, setTelemetries] = useState<Telemetry[]>([]);
    const [addressIdFilter, setAddressIdFilter] = useState<string>('');
    const [trainIdFilter, setTrainIdFilter] = useState<string>('');
    const [beaconNameFilter, setBeaconNameFilter] = useState<string[]>([]);
    const [sourceFilter, setSourceFilter] = useState<string[]>([]);
    const [isLoading, setIsLoading] = useState(false);

    const fetchTelemetries = async () => {
        setIsLoading(true);
        try {
            const apiUrl = import.meta.env.VITE_API_URL + "/api/v1/Telemetries";
            const response = await fetch(apiUrl, {
                headers: {
                    'X-Api-Key': import.meta.env.VITE_API_KEY,
                    'Content-Type': 'application/json'
                }
            });
            if (!response.ok) throw new Error('Failed to fetch telemetries');

            const { data } = await response.json();
            setTelemetries(data);
        } catch (error) {
            console.error('Error fetching telemetries:', error);
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        fetchTelemetries();
    }, []);

    const sortedData: Telemetry[] = telemetries
        .sort((a, b) => new Date(b.lastUpdate).getTime() - new Date(a.lastUpdate).getTime())
        .map((item) => ({
            ...item,
            id: item.id,
        }));

    // Get unique beacon names for dropdown
    const beaconNames = Array.from(new Set(sortedData.map(row => row.beaconName).filter(Boolean))).sort();

    // Filter rows by addressID, trainID, beaconName, and source (multi-select)
    const filteredData = sortedData.filter(row => {
        // Address ID filter
        if (addressIdFilter.trim()) {
            const addrTokens = addressIdFilter.split(',').map(v => v.trim()).filter(Boolean);
            if (!addrTokens.includes(String(row.addressID))) return false;
        }
        // Train ID filter
        if (trainIdFilter.trim()) {
            const trainTokens = trainIdFilter.split(',').map(v => v.trim()).filter(Boolean);
            if (!trainTokens.includes(String(row.trainID))) return false;
        }
        // Beacon Name filter (multi-select)
        if (beaconNameFilter.length > 0) {
            if (!beaconNameFilter.includes(row.beaconName)) return false;
        }
        // Source filter (multi-select)
        if (sourceFilter.length > 0) {
            if (!sourceFilter.includes(row.source)) return false;
        }
        return true;
    });

    const columns: GridColDef[] = [
        { field: 'id', headerName: 'ID' },
        { field: 'beaconName', headerName: 'Beacon Name', width: 240 },
        { field: 'addressID', headerName: 'Address ID', width: 100 },
        { field: 'trainID', headerName: 'Train ID', width: 100 },
        {
            field: 'moving',
            headerName: 'Moving',
            width: 100,
            type: 'boolean',
            renderCell: (params: any) =>
                params.row?.moving
                    ? <span style={{ color: '#4caf50', fontSize: '1.2em', fontWeight: 'bold' }}>✓</span>
                    : <span style={{ color: '#f44336', fontSize: '1.2em', fontWeight: 'bold' }}>✕</span>,
        },
        { field: 'source', headerName: 'Source', width: 100 },
        {
            field: 'lastUpdate',
            headerName: 'Last Update',
            width: 200,
            renderCell: (params: any) =>
                params.row?.lastUpdate
                    ? format(parseISO(params.row.lastUpdate), 'yyyy-MM-dd h:mm:ss aa')
                    : '',
        },
        {
            field: 'discarded',
            headerName: 'Discarded',
            width: 360,
            renderCell: (params: any) => {
                const reason = params.row?.discardReason;
                if (reason && reason.trim()) {
                    return <span style={{ color: '#d32f2f', fontWeight: 500 }}>{reason}</span>;
                }
                return <span>{params.row?.discarded ? 'Yes' : 'No'}</span>;
            },
        },
    ];

    // Ensure proper wrapping of all JSX elements
    return (
        <Box
            sx={{
                width: '100%',
                minHeight: '100vh',
                backgroundColor: '#1a1a1a',
                padding: 4,
                boxSizing: 'border-box',
            }}
        >
            <Box
                sx={{
                    maxWidth: 1400,
                    margin: '0 auto',
                }}
            >
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
                    <FormControl sx={{ minWidth: 220, backgroundColor: '#2a2a2a', borderRadius: 1, border: '1px solid #444' }} size="small">
                        <InputLabel
                            id="beacon-name-select-label"
                            sx={{
                                color: '#e0e0e0',
                                backgroundColor: '#2a2a2a',
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
                            sx={{
                                backgroundColor: '#2a2a2a',
                                color: '#e0e0e0',
                                borderRadius: 1,
                                '.MuiSelect-icon': { color: '#e0e0e0' },
                            }}
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
                    <FormControl sx={{ minWidth: 160, backgroundColor: '#2a2a2a', borderRadius: 1, border: '1px solid #444' }} size="small">
                        <InputLabel
                            id="source-select-label"
                            sx={{
                                color: '#e0e0e0',
                                backgroundColor: '#2a2a2a',
                                padding: '0 4px',
                                zIndex: 1,
                            }}
                        >
                            Filter by Source
                        </InputLabel>
                        <Select
                            labelId="source-select-label"
                            multiple
                            value={sourceFilter}
                            label="Filter by Source"
                            onChange={e => {
                                const value = e.target.value;
                                setSourceFilter(typeof value === 'string' ? value.split(',') : value);
                            }}
                            renderValue={(selected) =>
                                selected.length === 0 ? <em>All</em> : selected.join(', ')
                            }
                            sx={{
                                backgroundColor: '#2a2a2a',
                                color: '#e0e0e0',
                                borderRadius: 1,
                                '.MuiSelect-icon': { color: '#e0e0e0' },
                            }}
                        >
                            <MenuItem value="" disabled>
                                <em>All</em>
                            </MenuItem>
                            {['EOT', 'HOT', 'DPU'].map(source => (
                                <MenuItem key={source} value={source}>
                                    <Checkbox checked={sourceFilter.indexOf(source) > -1} />
                                    <ListItemText primary={source} />
                                </MenuItem>
                            ))}
                        </Select>
                    </FormControl>
                    <TextField
                        label="Filter by Address ID"
                        variant="outlined"
                        size="small"
                        value={addressIdFilter}
                        onChange={e => setAddressIdFilter(e.target.value)}
                        sx={{
                            backgroundColor: '#2a2a2a',
                            borderRadius: 1,
                            '& .MuiOutlinedInput-root': {
                                color: '#e0e0e0',
                                backgroundColor: '#2a2a2a',
                                '& fieldset': { borderColor: '#444' },
                                '&:hover fieldset': { borderColor: '#666' },
                                '&.Mui-focused fieldset': { borderColor: '#4a9eff' },
                            },
                            '& label': { color: '#888', backgroundColor: '#2a2a2a', padding: '0 4px' },
                            '& label.Mui-focused': { color: '#4a9eff', backgroundColor: '#2a2a2a' },
                        }}
                    />
                    <TextField
                        label="Filter by Train ID"
                        variant="outlined"
                        size="small"
                        value={trainIdFilter}
                        onChange={e => setTrainIdFilter(e.target.value)}
                        sx={{
                            backgroundColor: '#2a2a2a',
                            borderRadius: 1,
                            '& .MuiOutlinedInput-root': {
                                color: '#e0e0e0',
                                backgroundColor: '#2a2a2a',
                                '& fieldset': { borderColor: '#444' },
                                '&:hover fieldset': { borderColor: '#666' },
                                '&.Mui-focused fieldset': { borderColor: '#4a9eff' },
                            },
                            '& label': { color: '#888', backgroundColor: '#2a2a2a', padding: '0 4px' },
                            '& label.Mui-focused': { color: '#4a9eff', backgroundColor: '#2a2a2a' },
                        }}
                    />
                    <Tooltip title="Clear filters">
                        <IconButton
                            aria-label="clear filters"
                            onClick={() => {
                                setBeaconNameFilter([]);
                                setAddressIdFilter('');
                                setTrainIdFilter('');
                                setSourceFilter([]);
                            }}
                            sx={{ ml: 1, color: '#fff', backgroundColor: '#222', '&:hover': { backgroundColor: '#444' } }}
                        >
                            <ClearIcon />
                        </IconButton>
                    </Tooltip>
                    <Tooltip title="Refresh data">
                        <IconButton
                            aria-label="refresh data"
                            onClick={fetchTelemetries}
                            disabled={isLoading}
                            sx={{ ml: 1, color: '#fff', backgroundColor: '#222', '&:hover': { backgroundColor: '#444' }, '&.Mui-disabled': { color: '#666' } }}
                        >
                            <RefreshIcon />
                        </IconButton>
                    </Tooltip>
                </Box>
                <DataGrid
                    rows={filteredData}
                    columns={columns}
                    pageSizeOptions={[5, 10, 25]}
                    columnVisibilityModel={{
                        id: false,
                    }}
                    initialState={{
                        pagination: {
                            paginationModel: { pageSize: 25, page: 0 },
                        },
                    }}
                    sx={{
                        maxHeight: 750,
                        minHeight: 550,
                        width: '100%',
                        backgroundColor: '#2a2a2a',
                        color: '#e0e0e0',
                        border: '1px solid #444',
                        borderRadius: 1,
                        '& .MuiDataGrid-main': {
                            backgroundColor: '#2a2a2a',
                        },
                        '& .MuiDataGrid-virtualScroller': {
                            backgroundColor: '#2a2a2a',
                        },
                        '& .MuiDataGrid-filler': {
                            backgroundColor: '#333 !important',
                            borderColor: '#444 !important',
                        },
                        '& .MuiDataGrid-scrollbarFiller': {
                            backgroundColor: '#333 !important',
                        },
                        '& .MuiDataGrid-columnHeadersWrapper': {
                            backgroundColor: '#333 !important',
                            borderColor: '#444 !important',
                        },
                        '& .MuiDataGrid-columnHeadersInner': {
                            backgroundColor: '#333 !important',
                        },
                        '& .MuiDataGrid-columnHeaders': {
                            backgroundColor: '#333 !important',
                            color: '#e0e0e0',
                            borderColor: '#444 !important',
                        },
                        '& .MuiDataGrid-columnHeader': {
                            backgroundColor: '#333 !important',
                            color: '#e0e0e0',
                            borderColor: '#444 !important',
                        },
                        '& .MuiDataGrid-columnHeaderTitle': {
                            color: '#e0e0e0',
                            fontWeight: 600,
                        },
                        '& .MuiDataGrid-columnSeparator': {
                            backgroundColor: '#444 !important',
                        },
                        '& .MuiDataGrid-cell': {
                            color: '#e0e0e0',
                            borderColor: '#444',
                        },
                        '& .MuiDataGrid-row:hover': {
                            backgroundColor: '#3a3a3a',
                        },
                        '& .MuiDataGrid-row.Mui-selected': {
                            backgroundColor: '#1e3a5f !important',
                            '&:hover': {
                                backgroundColor: '#0d47a1 !important',
                            },
                        },
                        '& .MuiTablePagination-root': {
                            color: '#e0e0e0',
                        },
                        '& .MuiTablePagination-toolbar': {
                            backgroundColor: '#333',
                        },
                        '& .MuiIconButton-root': {
                            color: '#e0e0e0',
                        },
                        '& .MuiIconButton-root.Mui-disabled': {
                            color: '#555 !important',
                            opacity: 0.5,
                        },
                    }}
                    getRowClassName={(params) => params.row.discarded ? 'discarded-row' : ''}
                />
            </Box>
        </Box>
    );
}

export default AdminTelemetryLog;
