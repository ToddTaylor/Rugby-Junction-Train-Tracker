import { useEffect, useState } from "react";
import '../App.css';
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { Box, Typography } from '@mui/material';
import { Telemetry } from "../types/types";
import { format, parseISO } from "date-fns";

function TelemetryLog() {
    const [telemetries, setTelemetries] = useState<Telemetry[]>([]);

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
            <DataGrid
                rows={sortedData}
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
                    maxHeight: 600, // or height: 600,
                    minHeight: 400,
                }}
            />
        </Box>
    );
}

export default TelemetryLog;