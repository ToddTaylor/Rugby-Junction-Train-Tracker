import { useState } from "react";
import '../App.css';
import { useSignalR } from "../hooks/useSignalR";
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { Box, Typography } from '@mui/material';
import { MapAlert } from "../types/types";

function TelemetryLog() {
    const [alerts, setAlerts] = useState<MapAlert[]>([]);

    useSignalR((alert: MapAlert) => {
        setAlerts(prev => [...prev, alert]);
    });

    const sortedData: MapAlert[] = Array.from(alerts.values())
        .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
        .map((alert, index) => ({
            ...alert,
            id: `row-${index + 1}`,
        }));

    const columns: GridColDef[] = [
        { field: 'id', headerName: 'ID' },
        { field: 'source', headerName: 'Source', width: 100 },
        { field: 'addressID', headerName: 'Address ID', width: 100 },
        { field: 'latitude', headerName: 'Latitude', width: 100 },
        { field: 'longitude', headerName: 'Longitude', width: 100 },
        { field: 'direction', headerName: 'Direction', width: 100 },
        { field: 'moving', headerName: 'Moving', width: 100 },
        {
            field: 'Timestamp',
            headerName: 'Timestamp',
            width: 200,
            valueFormatter: (params) =>
                new Date(params as string).toLocaleString(),
        },
    ];

    return (
        <Box sx={{ height: 400, width: '100%', padding: 4 }}>
            <Typography variant="h5" gutterBottom>
                Telemetry Alerts
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
            />
        </Box>
    );
}

export default TelemetryLog;