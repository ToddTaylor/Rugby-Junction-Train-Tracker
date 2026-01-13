import { useEffect, useState } from "react";
import '../App.css';
import './TelemetryLog.css';
import './AdminSubdivisions.css';
import './AdminTelemetryLog.css';
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { Box, Typography, TextField, FormControl, InputLabel, Select, MenuItem, IconButton, Tooltip, Checkbox, ListItemText } from '@mui/material';
import ClearIcon from '@mui/icons-material/Clear';
import { Telemetry } from '../types/Telemetry';
import { format, parseISO } from "date-fns";

function AdminTelemetryLog() {
    const [telemetries, setTelemetries] = useState<Telemetry[]>([]);
    const [addressIdFilter, setAddressIdFilter] = useState<string>('');
    const [trainIdFilter, setTrainIdFilter] = useState<string>('');
    const [beaconNameFilter, setBeaconNameFilter] = useState<string[]>([]);
    const [sourceFilter, setSourceFilter] = useState<string[]>([]);

    useEffect(() => {
        const fetchTelemetries = async () => {
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
            }
        };

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
        { field: 'beaconName', headerName: 'Beacon Name', width: 160 },
        { field: 'addressID', headerName: 'Address ID', width: 100 },
        { field: 'trainID', headerName: 'Train ID', width: 100 },
        { field: 'moving', headerName: 'Moving', width: 100, type: 'boolean' },
        { field: 'source', headerName: 'Source', width: 100 },
        {
            field: 'createdAt',
            headerName: 'Created At',
            width: 200,
            renderCell: (params: any) =>
                params.row?.createdAt
                    ? format(parseISO(params.row.createdAt), 'yyyy-MM-dd h:mm:ss aa')
                    : '',
        },
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
            width: 180,
            renderCell: (params: any) => {
                const reason = params.row?.discardReason;
                if (reason && reason.trim()) {
                    return <span style={{ color: '#d32f2f', fontWeight: 500 }}>{reason}</span>;
                }
                return <span>{params.row?.discarded ? 'Yes' : 'No'}</span>;
            },
        },
    ];

    return (
        <div className="admin-telemetrylog-container">
            <div className="admin-header">
                <h1>Admin Telemetry Log</h1>
            </div>
            <div className="admin-controls">
                    <FormControl sx={{ minWidth: 180, backgroundColor: '#2a2a2a', borderRadius: 1, border: '1px solid #444', mr: 0 }} size="small">
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
                    <FormControl sx={{ minWidth: 140, backgroundColor: '#2a2a2a', borderRadius: 1, border: '1px solid #444', ml: 1 }} size="small">
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
                        ml: 1,
                        backgroundColor: '#2a2a2a',
                        color: '#e0e0e0',
                        borderRadius: 1,
                        border: '1px solid #444',
                        fontSize: '1rem',
                        '& .MuiOutlinedInput-root': {
                            color: '#e0e0e0',
                            backgroundColor: '#2a2a2a',
                            '& fieldset': { borderColor: '#444' },
                            '&:hover fieldset': { borderColor: '#888' },
                            '&.Mui-focused fieldset': { borderColor: '#4a9eff' },
                        },
                        '& label': { color: '#e0e0e0', backgroundColor: '#2a2a2a', padding: '0 4px' },
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
                        ml: 1,
                        backgroundColor: '#2a2a2a',
                        color: '#e0e0e0',
                        borderRadius: 1,
                        border: '1px solid #444',
                        fontSize: '1rem',
                        '& .MuiOutlinedInput-root': {
                            color: '#e0e0e0',
                            backgroundColor: '#2a2a2a',
                            '& fieldset': { borderColor: '#444' },
                            '&:hover fieldset': { borderColor: '#888' },
                            '&.Mui-focused fieldset': { borderColor: '#4a9eff' },
                        },
                        '& label': { color: '#e0e0e0', backgroundColor: '#2a2a2a', padding: '0 4px' },
                        '& label.Mui-focused': { color: '#4a9eff', backgroundColor: '#2a2a2a' },
                    }}
                />
                    <Tooltip title="Clear filters">
                        <IconButton
                            sx={{ ml: 1, color: '#fff', backgroundColor: '#222', '&:hover': { backgroundColor: '#444' }, height: '40px', width: '40px' }}
                            aria-label="clear filters"
                            onClick={() => {
                                setBeaconNameFilter([]);
                                setAddressIdFilter('');
                                setTrainIdFilter('');
                                setSourceFilter([]);
                            }}
                        >
                            <ClearIcon />
                        </IconButton>
                    </Tooltip>
            </div>
            <div style={{ marginTop: 16 }}>
                <table className="subdivisions-table">
                    <thead>
                        <tr>
                            <th>Beacon Name</th>
                            <th>Source</th>
                            <th>Address ID</th>
                            <th>Train ID</th>
                            <th>Moving</th>
                            <th>Created At</th>
                            <th>Last Update</th>
                            <th>Discarded</th>
                        </tr>
                    </thead>
                    <tbody>
                        {filteredData.map((row) => {
                            const isDiscarded = row.discardReason && row.discardReason.trim();
                            const strikeStyle = isDiscarded ? { textDecoration: 'line-through' } : {};
                            return (
                                <tr key={row.id}>
                                    <td style={strikeStyle}>{row.beaconName}</td>
                                    <td style={strikeStyle}>{row.source}</td>
                                    <td style={strikeStyle}>{row.addressID}</td>
                                    <td style={strikeStyle}>{row.trainID}</td>
                                    <td style={strikeStyle}>{row.moving ? 'Yes' : 'No'}</td>
                                    <td style={strikeStyle}>{row.createdAt ? format(parseISO(row.createdAt), 'yyyy-MM-dd h:mm:ss aa') : ''}</td>
                                    <td style={strikeStyle}>{row.lastUpdate ? format(parseISO(row.lastUpdate), 'yyyy-MM-dd h:mm:ss aa') : ''}</td>
                                    <td style={strikeStyle}>
                                        {row.discardReason && row.discardReason.trim()
                                            ? row.discardReason
                                            : (row.discarded ? 'Yes' : 'No')}
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>
        </div>
    );
}

export default AdminTelemetryLog;
