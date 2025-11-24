import { useEffect, useState } from "react";
import '../App.css';
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { Box, Typography, TextField, FormControl, InputLabel, Select, MenuItem, IconButton, Tooltip } from '@mui/material';
import ClearIcon from '@mui/icons-material/Clear';
import { Telemetry } from '../types/Telemetry';
import { format, parseISO } from "date-fns";

function TelemetryLog() {
    const [telemetries, setTelemetries] = useState<Telemetry[]>([]);
    const [addressIdFilter, setAddressIdFilter] = useState<string>('');
    const [beaconNameFilter, setBeaconNameFilter] = useState<string>('');

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

    // Filter rows by addressID and beaconName (dropdown selection)
    const filteredData = sortedData.filter(row => {
        // Address ID filter
        if (addressIdFilter.trim()) {
            const addrTokens = addressIdFilter.split(',').map(v => v.trim()).filter(Boolean);
            if (!addrTokens.includes(String(row.addressID))) return false;
        }
        // Beacon Name filter (exact match from dropdown)
        if (beaconNameFilter) {
            if (row.beaconName !== beaconNameFilter) return false;
        }
        return true;
    });

    const columns: GridColDef[] = [
        { field: 'id', headerName: 'ID' },
        { field: 'id', headerName: 'ID' },
        { field: 'beaconName', headerName: 'Beacon Name', width: 160 },
        { field: 'addressID', headerName: 'Address ID', width: 100 },
        { field: 'trainID', headerName: 'Train ID', width: 100 },
        { field: 'moving', headerName: 'Moving', width: 100, type: 'boolean' },
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
    ];

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
                Telemetry Log
            </Typography>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
                <FormControl sx={{ minWidth: 220, backgroundColor: '#fff', borderRadius: 1, boxShadow: 1 }} size="small">
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
                        value={beaconNameFilter}
                        label="Filter by Beacon Name"
                        onChange={e => setBeaconNameFilter(e.target.value)}
                    >
                        <MenuItem value=""><em>All</em></MenuItem>
                        {beaconNames.map(name => (
                            <MenuItem key={name} value={name}>{name}</MenuItem>
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
                        backgroundColor: '#fff',
                        borderRadius: 1,
                        boxShadow: 1,
                        '& .MuiOutlinedInput-root': {
                            color: '#222',
                            backgroundColor: '#fff',
                            '& fieldset': { borderColor: '#1976d2' },
                            '&:hover fieldset': { borderColor: '#1565c0' },
                            '&.Mui-focused fieldset': { borderColor: '#1976d2' },
                        },
                        '& label': { color: '#222', backgroundColor: '#fff', padding: '0 4px' },
                        '& label.Mui-focused': { color: '#1976d2', backgroundColor: '#fff' },
                    }}
                />
                <Tooltip title="Clear filters">
                    <IconButton
                        aria-label="clear filters"
                        onClick={() => {
                            setBeaconNameFilter('');
                            setAddressIdFilter('');
                        }}
                        sx={{ ml: 1, color: '#fff', backgroundColor: '#222', '&:hover': { backgroundColor: '#444' } }}
                    >
                        <ClearIcon />
                    </IconButton>
                </Tooltip>
            </Box>
            {/* Removed duplicate Address ID filter */}
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
                    maxHeight: 600,
                    minHeight: 400,
                }}
            />
        </Box>
    );
}

export default TelemetryLog;