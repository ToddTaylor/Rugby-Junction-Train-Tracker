import { adminDataGridSlots, adminDataGridSx } from '../components/DataGridStyles';
import { useEffect, useState } from "react";
import './Admin.css';
import '../App.css';
import './AdminSubdivisions.css';
import './AdminTelemetryLog.css';
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { Box, TextField, FormControl, InputLabel, Select, MenuItem, IconButton, Tooltip, Checkbox, ListItemText } from '@mui/material';
import ClearIcon from '@mui/icons-material/Clear';
import RefreshIcon from '@mui/icons-material/Refresh';
import { Telemetry } from '../types/Telemetry';
import { format, parseISO } from "date-fns";
import AdminPageHeader from '../components/admin/AdminPageHeader';
import './AdminSkin.css';
import { adminClearButtonSx } from '../components/admin/adminSx';

function AdminTelemetryLog() {
    const [telemetries, setTelemetries] = useState<Telemetry[]>([]);
    const [addressIdFilter, setAddressIdFilter] = useState<string>('');
        // Handler for clicking addressID hyperlink
        const handleAddressIdClick = (addressID: string) => {
            setBeaconNameFilter([]);
            setAddressIdFilter('');
            setTrainIdFilter('');
            setSourceFilter([]);
            setAddressIdFilter(addressID);
        };
    const [trainIdFilter, setTrainIdFilter] = useState<string>('');
        // Handler for clicking trainID hyperlink
        const handleTrainIdClick = (trainID: string) => {
            setBeaconNameFilter([]);
            setAddressIdFilter('');
            setTrainIdFilter('');
            setSourceFilter([]);
            setTrainIdFilter(trainID);
        };
    const [beaconNameFilter, setBeaconNameFilter] = useState<string[]>([]);
        // Handler for clicking beaconName hyperlink
        const handleBeaconNameClick = (beaconName: string) => {
            setBeaconNameFilter([]);
            setAddressIdFilter('');
            setTrainIdFilter('');
            setSourceFilter([]);
            setBeaconNameFilter([beaconName]);
        };
    const [sourceFilter, setSourceFilter] = useState<string[]>([]);
    const [isLoading, setIsLoading] = useState(false);

    const filterMenuProps = {
        PaperProps: {
            sx: {
                backgroundColor: 'var(--admin-bg-surface-alt)',
                color: 'var(--admin-text-primary)',
                border: '1px solid var(--admin-border)',
                '& .MuiMenuItem-root': {
                    color: 'var(--admin-text-primary)',
                },
                '& .MuiMenuItem-root:hover': {
                    backgroundColor: 'var(--admin-row-hover)',
                },
                '& .MuiMenuItem-root.Mui-selected': {
                    backgroundColor: 'rgba(44, 137, 232, 0.22)',
                },
                '& .MuiMenuItem-root.Mui-selected:hover': {
                    backgroundColor: 'rgba(44, 137, 232, 0.3)',
                },
                '& .MuiCheckbox-root': {
                    color: 'var(--admin-text-secondary)',
                },
                '& .MuiCheckbox-root.Mui-checked': {
                    color: 'var(--admin-accent)',
                },
            },
        },
    };

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
        {
            field: 'beaconName',
            headerName: 'Beacon Name',
            width: 190,
            renderCell: (params: any) => {
                const beaconName = params.row?.beaconName;
                if (!beaconName) return <span>-</span>;
                return (
                    <a
                        href="#"
                        style={{ color: 'var(--admin-accent)', textDecoration: 'underline', cursor: 'pointer' }}
                        onClick={e => {
                            e.preventDefault();
                            handleBeaconNameClick(beaconName);
                        }}
                    >
                        {beaconName}
                    </a>
                );
            },
        },
        {
            field: 'addressID',
            headerName: 'Address ID',
            width: 155,
            renderCell: (params: any) => {
                const addressID = params.row?.addressID;
                if (addressID === undefined || addressID === null) return <span>-</span>;
                return (
                    <a
                        href="#"
                        style={{ color: 'var(--admin-accent)', textDecoration: 'underline', cursor: 'pointer' }}
                        onClick={e => {
                            e.preventDefault();
                            handleAddressIdClick(String(addressID));
                        }}
                    >
                        {addressID}
                    </a>
                );
            },
        },
        {
            field: 'trainID',
            headerName: 'Train ID',
            width: 120,
            renderCell: (params: any) => {
                const trainID = params.row?.trainID;
                if (trainID === undefined || trainID === null) return <span>-</span>;
                return (
                    <a
                        href="#"
                        style={{ color: 'var(--admin-accent)', textDecoration: 'underline', cursor: 'pointer' }}
                        onClick={e => {
                            e.preventDefault();
                            handleTrainIdClick(String(trainID));
                        }}
                    >
                        {trainID}
                    </a>
                );
            },
        },
        {
            field: 'moving',
            headerName: 'Moving',
            width: 110,
            type: 'boolean',
                renderCell: (params: any) => {
                    const moving = params.row?.moving;
                    if (moving === null || moving === undefined) {
                        return <span>-</span>;
                    }
                    return moving
                        ? <span style={{ color: '#4caf50', fontSize: '1.2em', fontWeight: 'bold' }}>✓</span>
                        : <span style={{ color: '#f44336', fontSize: '1.2em', fontWeight: 'bold' }}>✕</span>;
                },
        },
        {
            field: 'brakePipePressure',
            headerName: 'BP',
            width: 90,
            renderCell: (params: any) =>
                params.row?.brakePipePressure !== undefined && params.row?.brakePipePressure !== null
                    ? <span>{params.row.brakePipePressure}</span>
                    : <span>-</span>,
        },
        { field: 'source', headerName: 'Source', width: 120 },
        {
            field: 'lastUpdate',
            headerName: 'Last Update',
            width: 200,
            renderCell: (params: any) =>
                    params.row?.lastUpdate
                        ? format(parseISO(params.row.lastUpdate), 'yyyy-MM-dd HH:mm:ss')
                        : '',
        },
        {
            field: 'discarded',
            headerName: 'Discarded',
            width: 300,
            flex: 1,
            renderCell: (params: any) => {
                const reason = params.row?.discardReason;
                if (reason && reason.trim()) {
                    return <span>{reason}</span>;
                }
                return <span>{params.row?.discarded ? 'Yes' : 'No'}</span>;
            },
        },
    ];

    // Ensure proper wrapping of all JSX elements
    return (
        <Box
            className="admin-page admin-page--xwide"
            sx={{
                width: '100%',
                minHeight: '100vh',
                boxSizing: 'border-box',
            }}
        >
            <Box
                sx={{
                    maxWidth: 1400,
                    margin: '0 auto',
                }}
            >
                <AdminPageHeader
                    title="Telemetry Log"
                    description="Inspect raw telemetry packets and quickly filter by beacon, source, and train identifiers."
                />

                <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
                    <FormControl sx={{ minWidth: 220, backgroundColor: 'var(--admin-bg-surface-alt)', borderRadius: 1, border: '1px solid var(--admin-border)' }} size="small">
                        <InputLabel
                            id="beacon-name-select-label"
                            sx={{
                                color: 'var(--admin-text-primary)',
                                backgroundColor: 'var(--admin-bg-surface-alt)',
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
                            MenuProps={filterMenuProps}
                            onChange={e => {
                                const value = e.target.value;
                                setBeaconNameFilter(typeof value === 'string' ? value.split(',') : value);
                            }}
                            renderValue={(selected) =>
                                selected.length === 0 ? <em>All</em> : selected.join(', ')
                            }
                            sx={{
                                backgroundColor: 'var(--admin-bg-surface-alt)',
                                color: 'var(--admin-text-primary)',
                                borderRadius: 1,
                                '.MuiSelect-icon': { color: 'var(--admin-text-primary)' },
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
                    <FormControl sx={{ minWidth: 160, backgroundColor: 'var(--admin-bg-surface-alt)', borderRadius: 1, border: '1px solid var(--admin-border)' }} size="small">
                        <InputLabel
                            id="source-select-label"
                            sx={{
                                color: 'var(--admin-text-primary)',
                                backgroundColor: 'var(--admin-bg-surface-alt)',
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
                            MenuProps={filterMenuProps}
                            onChange={e => {
                                const value = e.target.value;
                                setSourceFilter(typeof value === 'string' ? value.split(',') : value);
                            }}
                            renderValue={(selected) =>
                                selected.length === 0 ? <em>All</em> : selected.join(', ')
                            }
                            sx={{
                                backgroundColor: 'var(--admin-bg-surface-alt)',
                                color: 'var(--admin-text-primary)',
                                borderRadius: 1,
                                '.MuiSelect-icon': { color: 'var(--admin-text-primary)' },
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
                        slotProps={{ inputLabel: { shrink: true } }}
                        sx={{
                            backgroundColor: 'var(--admin-bg-surface-alt)',
                            borderRadius: 1,
                            '& .MuiOutlinedInput-root': {
                                color: 'var(--admin-text-primary)',
                                backgroundColor: 'var(--admin-bg-surface-alt)',
                                '& fieldset': { borderColor: 'var(--admin-border)' },
                                '&:hover fieldset': { borderColor: 'var(--admin-border)' },
                                '&.Mui-focused fieldset': { borderColor: 'var(--admin-accent)' },
                            },
                            '& label': { color: 'var(--admin-text-secondary)', backgroundColor: 'var(--admin-bg-surface-alt)', padding: '0 4px' },
                            '& label.Mui-focused': { color: 'var(--admin-accent)', backgroundColor: 'var(--admin-bg-surface-alt)' },
                        }}
                    />
                    <TextField
                        label="Filter by Train ID"
                        variant="outlined"
                        size="small"
                        value={trainIdFilter}
                        onChange={e => setTrainIdFilter(e.target.value)}
                        slotProps={{ inputLabel: { shrink: true } }}
                        sx={{
                            backgroundColor: 'var(--admin-bg-surface-alt)',
                            borderRadius: 1,
                            '& .MuiOutlinedInput-root': {
                                color: 'var(--admin-text-primary)',
                                backgroundColor: 'var(--admin-bg-surface-alt)',
                                '& fieldset': { borderColor: 'var(--admin-border)' },
                                '&:hover fieldset': { borderColor: 'var(--admin-border)' },
                                '&.Mui-focused fieldset': { borderColor: 'var(--admin-accent)' },
                            },
                            '& label': { color: 'var(--admin-text-secondary)', backgroundColor: 'var(--admin-bg-surface-alt)', padding: '0 4px' },
                            '& label.Mui-focused': { color: 'var(--admin-accent)', backgroundColor: 'var(--admin-bg-surface-alt)' },
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
                            sx={{ ml: 1, ...adminClearButtonSx }}
                        >
                            <ClearIcon />
                        </IconButton>
                    </Tooltip>
                    <Tooltip title="Refresh data">
                        <span>
                            <IconButton
                                aria-label="refresh data"
                                onClick={fetchTelemetries}
                                disabled={isLoading}
                                sx={{ ...adminClearButtonSx, '&.Mui-disabled': { color: 'var(--admin-text-secondary)' } }}
                            >
                                <RefreshIcon />
                            </IconButton>
                        </span>
                    </Tooltip>
                </Box>
                <DataGrid
                    autoHeight
                    rows={filteredData}
                    columns={columns}
                    pageSizeOptions={[5, 10, 25]}
                    initialState={{
                        pagination: { paginationModel: { pageSize: 25, page: 0 } },
                    }}
                    sortingOrder={['asc', 'desc', null]}
                    disableColumnMenu
                    slots={adminDataGridSlots}
                    sx={adminDataGridSx}
                    getRowClassName={(params) => params.row.discarded ? 'discarded-row' : ''}
                />
            </Box>
        </Box>
    );
}

export default AdminTelemetryLog;
