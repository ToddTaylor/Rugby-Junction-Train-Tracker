import React, { useEffect, useMemo, useState } from 'react';
import TextField from '@mui/material/TextField';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import ClearIcon from '@mui/icons-material/Clear';
import './AdminRailroads.css';
import { createAmtrakTrackedTrain, deleteAmtrakTrackedTrain, getAmtrakPollingConfiguration, getAmtrakTrackedTrains, updateAmtrakPollingConfiguration } from '../api/amtrak';
import { AmtrakTrackedTrain } from '../types/Amtrak';

const AdminAmtrak: React.FC = () => {
  const [trackedTrains, setTrackedTrains] = useState<AmtrakTrackedTrain[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [newTrainNumber, setNewTrainNumber] = useState('');
  const [pollIntervalMinutes, setPollIntervalMinutes] = useState(2);
  const [searchTerm, setSearchTerm] = useState('');
  const [savingInterval, setSavingInterval] = useState(false);

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
    const term = searchTerm.trim().toLowerCase();
    if (!term) {
      return trackedTrains;
    }

    return trackedTrains.filter(train => train.trainNumber.toLowerCase().includes(term));
  }, [trackedTrains, searchTerm]);

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
    <div className="admin-railroads">
      <div className="admin-header">
        <h1>Amtrak</h1>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="admin-controls" style={{ alignItems: 'flex-end', gap: '1rem', flexWrap: 'wrap' }}>
        <form onSubmit={handleAddTrain} style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <TextField
            label="Tracked Train Number(s)"
            variant="outlined"
            size="small"
            className="admin-input"
            value={newTrainNumber}
            onChange={(e) => setNewTrainNumber(e.target.value)}
            helperText="Enter one or more train numbers separated by commas"
          />
          <button className="btn-primary" type="submit">Add Train</button>
        </form>

        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <TextField
            label="Poll Interval Minutes"
            variant="outlined"
            size="small"
            type="number"
            className="admin-input"
            inputProps={{ min: 1, max: 30 }}
            value={pollIntervalMinutes}
            onChange={(e) => setPollIntervalMinutes(Number(e.target.value))}
          />
          <button className="btn-primary" type="button" onClick={handleSavePollingInterval} disabled={savingInterval}>
            {savingInterval ? 'Saving...' : 'Save Interval'}
          </button>
        </div>
      </div>

      <div className="admin-controls">
        <div className="search-container">
          <TextField
            label="Filter by Train Number"
            variant="outlined"
            size="small"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="admin-input"
            fullWidth
          />
          <Tooltip title="Clear filters">
            <IconButton
              sx={{ color: '#fff', backgroundColor: '#222', '&:hover': { backgroundColor: '#444' }, height: '40px', width: '40px' }}
              aria-label="clear filters"
              onClick={() => setSearchTerm('')}
            >
              <ClearIcon />
            </IconButton>
          </Tooltip>
        </div>
      </div>

      <div className="admin-table-container">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Train Number</th>
              <th>Active</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {filteredTrains.map(train => (
              <tr key={train.id}>
                <td>{train.trainNumber}</td>
                <td>{train.isActive ? 'Yes' : 'No'}</td>
                <td>{new Date(train.createdAt).toLocaleString()}</td>
                <td className="actions-cell">
                  <button className="btn-delete" onClick={() => handleDeleteTrain(train.id)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default AdminAmtrak;