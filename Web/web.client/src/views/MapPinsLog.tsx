import { useEffect, useState } from "react";
import '../App.css';
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { Box, Typography, FormControl, InputLabel, Select, MenuItem, Checkbox, ListItemText } from '@mui/material';
import { MapPin } from '../types/MapPin';
import { format, parseISO } from "date-fns";

function MapPinsLog() {
    const [mapPins, setMapPins] = useState<MapPin[]>([]);
    const [beaconNameFilter, setBeaconNameFilter] = useState<string[]>([]);

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
                if (!Array.isArray(addresses)) return '';
                return addresses
                    .map((a: { source: string; addressID: number }) => `${a.addressID} ${a.source}`)
                    .join(', ');
            },
        },
        { field: 'direction', headerName: 'Direction', width: 100 },
        { field: 'moving', headerName: 'Moving', width: 90 },
    ];

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
                    maxHeight: 600, // or height: 600,
                    minHeight: 400,
                }}
            />
        </Box>
    );
}

export default MapPinsLog;