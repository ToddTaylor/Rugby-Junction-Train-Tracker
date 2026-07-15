import React, { useEffect, useMemo, useState } from 'react';
import TextField from '@mui/material/TextField';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import ClearIcon from '@mui/icons-material/Clear';
import RefreshIcon from '@mui/icons-material/Refresh';
import './AdminRailroads.css';
import './AdminSkin.css';
import { createAmtrakTrackedTrain, deleteAmtrakTrackedTrain, getAmtrakPollingConfiguration, getAmtrakTrackedTrains, updateAmtrakPollingConfiguration } from '../api/amtrak';
import { AmtrakTrackedTrain } from '../types/Amtrak';
import AdminPageHeader from '../components/admin/AdminPageHeader';
import { adminClearButtonSx } from '../components/admin/adminSx';

const AdminAmtrak: React.FC = () => {
  const [trackedTrains, setTrackedTrains] = useState<AmtrakTrackedTrain[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [newTrainNumber, setNewTrainNumber] = useState('');
  const [pollIntervalMinutes, setPollIntervalMinutes] = useState(2);
  const [searchTerm, setSearchTerm] = useState('');
  const [savingInterval, setSavingInterval] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(10);

  useEffect(() => {
    loadData();
  }, []);

  async function loadData() {
    setLoading(true);
    const [trainsResponse, pollingResponse] = await Promise.all([
      getAmtrakTrackedTrains(),
      getAmtrakPollingConfiguration(),
    ]);

    if (trainsResponse.errors.length > 0) {
      setError(trainsResponse.errors.join(', '));
    } else {
      setTrackedTrains(trainsResponse.data || []);
    }

    if (pollingResponse.errors.length > 0) {
      setError(prev => prev ? `${prev}; ${pollingResponse.errors.join(', ')}` : pollingResponse.errors.join(', '));
    } else if (pollingResponse.data) {
      setPollIntervalMinutes(pollingResponse.data.pollIntervalMinutes);
    }

    setLoading(false);
  }

  const filteredTrains = useMemo(() => {
    setCurrentPage(1);
    const term = searchTerm.trim().toLowerCase();
    if (!term) {
      return trackedTrains;
    }

    return trackedTrains.filter(train => train.trainNumber.toLowerCase().includes(term));
  }, [trackedTrains, searchTerm]);

  const paginatedTrains = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage;
    return filteredTrains.slice(startIndex, startIndex + itemsPerPage);
  }, [filteredTrains, currentPage, itemsPerPage]);

  const totalPages = Math.ceil(filteredTrains.length / itemsPerPage);

  async function handleAddTrain(e: React.FormEvent) {
    e.preventDefault();
    setError(undefined);

    const requestedTrainNumbers = newTrainNumber
      .split(',')
      .map(value => value.trim())
      .filter(value => value.length > 0);

    if (requestedTrainNumbers.length === 0) {
      setError('Train number is required.');
      return;
    }

    if (pollIntervalMinutes < 1 || pollIntervalMinutes > 30) {
      setError('Poll interval must be between 1 and 30 minutes.');
      return;
    }

    const pollResponse = await updateAmtrakPollingConfiguration({ pollIntervalMinutes });
    if (pollResponse.errors.length > 0) {
      setError(pollResponse.errors.join(', '));
      return;
    }

    const knownTrainNumbers = new Set(trackedTrains.map(train => train.trainNumber.toLowerCase()));
    const uniqueTrainNumbersToAdd: string[] = [];

    for (const trainNumber of requestedTrainNumbers) {
      const key = trainNumber.toLowerCase();
      if (!knownTrainNumbers.has(key)) {
        uniqueTrainNumbersToAdd.push(trainNumber);
        knownTrainNumbers.add(key);
      }
    }

    const addErrors: string[] = [];

    for (const trainNumber of uniqueTrainNumbersToAdd) {
      const response = await createAmtrakTrackedTrain({ trainNumber });
      if (response.errors.length > 0) {
        const alreadyExists = response.errors.some(error => error.toLowerCase().includes('already configured'));
        if (!alreadyExists) {
          addErrors.push(`${trainNumber}: ${response.errors.join(', ')}`);
        }
      }
    }

    if (addErrors.length > 0) {
      setError(addErrors.join('; '));
    }

    setNewTrainNumber('');
    await loadData();
  }

  async function handleDeleteTrain(id: number) {
    if (!confirm('Delete this tracked Amtrak train number?')) {
      return;
    }

    const response = await deleteAmtrakTrackedTrain(id);
    if (response.errors.length > 0) {
      setError(response.errors.join(', '));
      return;
    }

    await loadData();
  }

  async function handleSavePollingInterval() {
    setError(undefined);
    if (pollIntervalMinutes < 1 || pollIntervalMinutes > 30) {
      setError('Poll interval must be between 1 and 30 minutes.');
      return;
    }

    setSavingInterval(true);
    const response = await updateAmtrakPollingConfiguration({ pollIntervalMinutes });
    setSavingInterval(false);
    if (response.errors.length > 0) {
      setError(response.errors.join(', '));
      return;
    }

    await loadData();
  }

  if (loading) {
    return <div className="admin-loading">Loading...</div>;
  }

  return (
    <div className="admin-railroads admin-page admin-page--narrow">
      <AdminPageHeader
        title="Amtrak"
        description="Manage tracked train numbers and polling cadence for Amtrak feeds."
      />

      {error && <div className="error-message">{error}</div>}

      <div className="admin-controls admin-amtrak-toolbar">
        <form onSubmit={handleAddTrain} className="admin-amtrak-control-form">
          <TextField
            label="Tracked Train Number(s)"
            variant="outlined"
            size="small"
            className="admin-input"
            sx={{ width: 210 }}
            slotProps={{ inputLabel: { shrink: true } }}
            value={newTrainNumber}
            onChange={(e) => setNewTrainNumber(e.target.value)}
          />
          <button className="btn-primary admin-amtrak-button" type="submit">Add Train</button>
        </form>

        <div className="admin-amtrak-control-group">
          <TextField
            label="Poll Interval Minutes"
            variant="outlined"
            size="small"
            type="number"
            className="admin-input"
            sx={{ width: 150 }}
            slotProps={{ inputLabel: { shrink: true } }}
            inputProps={{ min: 1, max: 30 }}
            value={pollIntervalMinutes}
            onChange={(e) => setPollIntervalMinutes(Number(e.target.value))}
          />
          <button className="btn-primary admin-amtrak-button" type="button" onClick={handleSavePollingInterval} disabled={savingInterval}>
            {savingInterval ? 'Saving...' : 'Save Interval'}
          </button>
        </div>

        <div className="search-container">
          <TextField
            label="Filter by Train Number"
            variant="outlined"
            size="small"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="admin-input"
            sx={{ width: 200 }}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <Tooltip title="Clear filters">
            <IconButton
              sx={adminClearButtonSx}
              aria-label="clear filters"
              onClick={() => setSearchTerm('')}
            >
              <ClearIcon />
            </IconButton>
          </Tooltip>
          <Tooltip title="Refresh data">
            <IconButton
              sx={adminClearButtonSx}
              aria-label="refresh data"
              onClick={loadData}
            >
              <RefreshIcon />
            </IconButton>
          </Tooltip>
        </div>
      </div>
      <p className="admin-amtrak-helper-text">Enter one or more train numbers separated by commas.</p>

      <div className="admin-table-container">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Train Number</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {paginatedTrains.map(train => (
              <tr key={train.id}>
                <td>{train.trainNumber}</td>
                <td>{new Date(train.createdAt).toLocaleString()}</td>
                <td className="actions-cell">
                  <button className="btn-delete" onClick={() => handleDeleteTrain(train.id)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="admin-table-footer-pager">
          <span>Rows per page:</span>
          <select
            className="admin-table-footer-select"
            value={itemsPerPage}
            onChange={(e) => {
              setItemsPerPage(Number(e.target.value));
              setCurrentPage(1);
            }}
          >
            <option value={10}>10</option>
            <option value={25}>25</option>
            <option value={50}>50</option>
          </select>
          <span>
            {filteredTrains.length === 0
              ? '0-0 of 0'
              : `${((currentPage - 1) * itemsPerPage) + 1}-${Math.min(currentPage * itemsPerPage, filteredTrains.length)} of ${filteredTrains.length}`}
          </span>
          <button
            type="button"
            className="admin-table-footer-btn"
            onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
            disabled={currentPage === 1 || filteredTrains.length === 0}
            aria-label="Go to previous page"
          >
            ‹
          </button>
          <button
            type="button"
            className="admin-table-footer-btn"
            onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
            disabled={currentPage === totalPages || filteredTrains.length === 0}
            aria-label="Go to next page"
          >
            ›
          </button>
        </div>
      </div>
    </div>
  );
};

export default AdminAmtrak;