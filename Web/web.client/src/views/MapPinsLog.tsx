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

    const sortedData: MapPin[] = mapPins
        .sort((a, b) => new Date(b.lastUpdate).getTime() - new Date(a.lastUpdate).getTime())
        .map((pin) => ({
            ...pin,
            id: pin.id ,
        }));

    console.log('sortedData', sortedData);

    const columns: GridColDef[] = [
        { field: 'id', headerName: 'ID' },
        {
            field: 'lastUpdate',
            headerName: 'Last Update',
            width: 100,
            valueFormatter: (params) =>
                format(parseISO(params as string), 'h:mm aa'),
        },
        { field: 'railroad', headerName: 'Railroad', width: 75 },
        { field: 'subdivision', headerName: 'Subdivision', width: 100 },
        { field: 'milepost', headerName: 'Milepost', width: 100 },
        {
            field: 'addresses',
            headerName: 'Addresses',
            width: 300,
            renderCell: (params: any) => {
                const addresses = params.row?.addresses;
                if (!Array.isArray(addresses)) return '';
                return addresses
                    .map((a: { source: string; addressID: number }) => `${a.addressID} ${a.source}`)
                    .join(', ');
            },
        },
        { field: 'latitude', headerName: 'Latitude', width: 100 },
        { field: 'longitude', headerName: 'Longitude', width: 100 },
        { field: 'direction', headerName: 'Direction', width: 100 },
        { field: 'moving', headerName: 'Moving', width: 100 },
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
                Map Pins Log
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

export default MapPinsLog;