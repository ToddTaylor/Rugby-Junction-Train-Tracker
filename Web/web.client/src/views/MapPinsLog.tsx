import { useEffect, useState } from "react";
import '../App.css';
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { Box, Typography } from '@mui/material';
import { MapPin } from "../types/types";
import { format, parseISO } from "date-fns";

function MapPinsLog() {
    const [mapPins, setMapPins] = useState<MapPin[]>([]);

    useEffect(() => {
        const fetchMapPins = async () => {
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
        };
        
        fetchMapPins();
    }, []);

    const sortedData: MapPin[] = Array.from(mapPins.values())
        .sort((a, b) => new Date(b.lastUpdate).getTime() - new Date(a.lastUpdate).getTime())
        .map((alert, index) => ({
            ...alert,
            id: `row-${index + 1}`,
        }));

    const columns: GridColDef[] = [
        { field: 'id', headerName: 'ID' },
        { field: 'source', headerName: 'Source', width: 100 },
        { field: 'addressID', headerName: 'Address ID', width: 100 },
        { field: 'milepost', headerName: 'Milepost', width: 100 },
        { field: 'latitude', headerName: 'Latitude', width: 100 },
        { field: 'longitude', headerName: 'Longitude', width: 100 },
        { field: 'direction', headerName: 'Direction', width: 100 },
        { field: 'moving', headerName: 'Moving', width: 100 },
        {
            field: 'lastUpdate',
            headerName: 'Last Update',
            width: 200,
            valueFormatter: (params) =>
                format(parseISO(params as string), 'h:mm aa'),
        },
    ];

    return (
        <Box sx={{ height: '100%', width: '100%', padding: 4 }}>
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

export default MapPinsLog;