import { useEffect, useState, useMemo } from "react";
import './Admin.css';
import '../App.css';
import './TelemetryLog.css';
import './AdminSubdivisions.css';
import './AdminTelemetryLog.css';
import { TextField, FormControl, InputLabel, Select, MenuItem, IconButton, Tooltip, Checkbox, ListItemText } from '@mui/material';
import ClearIcon from '@mui/icons-material/Clear';
import { Telemetry } from '../types/Telemetry';
import { format, parseISO } from "date-fns";

function AdminTelemetryLog() {
    const [telemetries, setTelemetries] = useState<Telemetry[]>([]);
    const [addressIdFilter, setAddressIdFilter] = useState<string>('');
    const [trainIdFilter, setTrainIdFilter] = useState<string>('');
    const [beaconNameFilter, setBeaconNameFilter] = useState<string[]>([]);
    const [sourceFilter, setSourceFilter] = useState<string[]>([]);
    const [currentPage, setCurrentPage] = useState(1);
    const itemsPerPage = 10;

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

    const paginatedData = useMemo(() => {
        const startIndex = (currentPage - 1) * itemsPerPage;
        return filteredData.slice(startIndex, startIndex + itemsPerPage);
    }, [filteredData, currentPage]);

    const totalPages = Math.ceil(filteredData.length / itemsPerPage);

    const handlePageChange = (page: number) => {
        setCurrentPage(page);
    };

    // Updated pagination logic to limit the number of visible page buttons
    const renderPagination = () => {
        const pageButtons = [];
        const maxVisiblePages = 5; // Number of visible page buttons
        const startPage = Math.max(1, currentPage - Math.floor(maxVisiblePages / 2));
        const endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);

        if (startPage > 1) {
            pageButtons.push(
                <button
                    key={1}
                    className={`btn-page ${currentPage === 1 ? 'active' : ''}`}
                    onClick={() => handlePageChange(1)}
                >
                    1
                </button>
            );
            if (startPage > 2) {
                pageButtons.push(<span key="ellipsis-start">...</span>);
            }
        }

        for (let i = startPage; i <= endPage; i++) {
            pageButtons.push(
                <button
                    key={i}
                    className={`btn-page ${currentPage === i ? 'active' : ''}`}
                    onClick={() => handlePageChange(i)}
                    disabled={currentPage === i}
                >
                    {i}
                </button>
            );
        }

        if (endPage < totalPages) {
            if (endPage < totalPages - 1) {
                pageButtons.push(<span key="ellipsis-end">...</span>);
            }
            pageButtons.push(
                <button
                    key={totalPages}
                    className={`btn-page ${currentPage === totalPages ? 'active' : ''}`}
                    onClick={() => handlePageChange(totalPages)}
                >
                    {totalPages}
                </button>
            );
        }

        return pageButtons;
    };

    // Ensure proper wrapping of all JSX elements
    return (
        <div className="admin-telemetrylog-wrapper">
            <div className="admin-telemetrylog-container">
                <div className="admin-header">
                    <h1>Telemetry Log</h1>
                </div>
                <div className="admin-controls-wrapper" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap' }}>
                    <div className="filter-controls" style={{ display: 'flex', flexWrap: 'wrap', gap: '4px' }}>
                        <FormControl sx={{ minWidth: 210, backgroundColor: '#2a2a2a', borderRadius: 1, border: '1px solid #444' }} size="small">
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
                        <FormControl sx={{ minWidth: 200, backgroundColor: '#2a2a2a', borderRadius: 1, border: '1px solid #444' }} size="small">
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
                            className="admin-input"
                        />
                        <TextField
                            label="Filter by Train ID"
                            variant="outlined"
                            size="small"
                            value={trainIdFilter}
                            onChange={e => setTrainIdFilter(e.target.value)}
                            className="admin-input"
                        />
                        <Tooltip title="Clear filters">
                            <IconButton
                                sx={{ color: '#fff', backgroundColor: '#222', '&:hover': { backgroundColor: '#444' }, height: '40px', width: '40px' }}
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
                    <div className="pagination-wrapper" style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0px' }}>
                        <div className="pagination">
                            {renderPagination()}
                        </div>
                        <div className="results-info">
                            Showing {Math.min((currentPage - 1) * itemsPerPage + 1, filteredData.length)}-
                            {Math.min(currentPage * itemsPerPage, filteredData.length)} of {filteredData.length} entries
                        </div>
                    </div>
                </div>
                <div style={{ marginTop: 16 }}>
                    <table className="admin-table">
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
                            {paginatedData.map((row) => {
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
        </div>
    );
}

export default AdminTelemetryLog;
