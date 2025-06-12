import { useEffect, useState } from "react";
import '../App.css';
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { Box, Typography, TextField } from '@mui/material';
import { Telemetry } from "../types/types";
import { format, parseISO } from "date-fns";

function TelemetryLog() {
    const [telemetries, setTelemetries] = useState<Telemetry[]>([]);
    const [addressIdFilter, setAddressIdFilter] = useState<string>('');

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
        .map((item, index) => ({
            ...item,
            id: item.id ?? `row-${index + 1}`,
        }));

    // Filter rows by addressID(s) if filter is set
    const filteredData = addressIdFilter.trim()
        ? sortedData.filter(row => {
            const filterValues = addressIdFilter
                .split(',')
                .map(v => v.trim())
                .filter(Boolean);
            return filterValues.includes(String(row.addressID));
        })
        : sortedData;

    const columns: GridColDef[] = [
        { field: 'id', headerName: 'ID' },
        { field: 'beaconID', headerName: 'Beacon ID', width: 100 },
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
            <TextField
                label="Filter by Address ID"
                variant="outlined"
                size="small"
                value={addressIdFilter}
                onChange={e => setAddressIdFilter(e.target.value)}
                sx={{
                    mb: 2,
                    backgroundColor: '#fff',
                    borderRadius: 1,
                    boxShadow: 1,
                    '& .MuiOutlinedInput-root': {
                        color: '#222', // input text color
                        backgroundColor: '#fff',
                        '& fieldset': {
                            borderColor: '#1976d2',
                        },
                        '&:hover fieldset': {
                            borderColor: '#1565c0',
                        },
                        '&.Mui-focused fieldset': {
                            borderColor: '#1976d2',
                        },
                    },
                    '& label': {
                        color: '#222',
                        backgroundColor: '#fff',
                        padding: '0 4px',
                    },
                    '& label.Mui-focused': {
                        color: '#1976d2',
                        backgroundColor: '#fff',
                    },
                }}
            />
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