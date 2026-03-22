import { adminDataGridSx } from '../components/DataGridStyles';
import React, { useState, useEffect, useMemo } from 'react';
import { DataGrid } from '@mui/x-data-grid';
import { Subdivision, CreateSubdivision, UpdateSubdivision } from '../types/Subdivision';
import { Railroad } from '../types/Railroad';
import { User } from '../types/User';
import { getSubdivisions, createSubdivision, updateSubdivision, deleteSubdivision } from '../api/subdivisions';
import { getRailroads } from '../api/railroads';
import { getTrackageRights, replaceTrackageRights } from '../api/subdivisionTrackageRights';
import { getUsers } from '../api/users';
import './AdminSubdivisions.css';
import TextField from '@mui/material/TextField';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import ClearIcon from '@mui/icons-material/Clear';
import { useAuth } from '../hooks/useAuth';


export const AdminSubdivisions: React.FC = () => {
  const [subdivisions, setSubdivisions] = useState<Subdivision[]>([]);
  const [railroads, setRailroads] = useState<Railroad[]>([]);
  const [custodianUsers, setCustodianUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [modalMode, setModalMode] = useState<'create' | 'edit'>('create');
  const [selectedSubdivision, setSelectedSubdivision] = useState<Subdivision | null>(null);
  const [formData, setFormData] = useState<CreateSubdivision>({
    name: '',
    railroadID: 0,
    dpuCapable: false,
    localTrainAddressIDs: '',
    custodianId: null,
  });
  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [selectedTrackageRailroad, setSelectedTrackageRailroad] = useState<number>(0);
  const [selectedTrackageSubdivisions, setSelectedTrackageSubdivisions] = useState<number[]>([]);
  const [selectedCustodianId, setSelectedCustodianId] = useState<number | null>(null);

  const { session } = useAuth();
  const isCustodian = session?.roles?.includes('Custodian');
  const userId = session?.userId;

  useEffect(() => {
    loadData();
    loadCustodians();
  }, []);

  async function loadCustodians() {
    const usersResult = await getUsers();
    if (usersResult.errors.length === 0 && usersResult.data) {
      setCustodianUsers(usersResult.data.filter(u => u.roles.includes('Custodian')));
    }
  }

  async function loadData() {
    setLoading(true);
    setError(null);

    const [subdivisionsResult, railroadsResult] = await Promise.all([
      getSubdivisions(),
      getRailroads()
    ]);

    if (subdivisionsResult.errors.length > 0) {
      setError(subdivisionsResult.errors.join(', '));
    } else {
      setSubdivisions(subdivisionsResult.data || []);
    }

    if (railroadsResult.errors.length > 0) {
      setError((prev) => prev ? `${prev}; ${railroadsResult.errors.join(', ')}` : railroadsResult.errors.join(', '));
    } else {
      setRailroads(railroadsResult.data || []);
    }

    setLoading(false);
  }

  // Filter subdivisions for search only (custodians see all, but edit only their own)
  const filteredSubdivisions = useMemo(() => {
    let filtered = subdivisions;
    if (searchTerm.trim()) {
      const term = searchTerm.trim().toLowerCase();
      filtered = filtered.filter(s =>
        s.name.toLowerCase().includes(term) ||
        s.railroad.toLowerCase().includes(term)
      );
    }
    return filtered;
  }, [subdivisions, searchTerm]);

  const handleCreate = () => {
    setModalMode('create');
    setSelectedSubdivision(null);
    setFormData({
      name: '',
      railroadID: railroads.length > 0 ? railroads[0].id : 0,
      dpuCapable: false,
      localTrainAddressIDs: '',
      custodianId: null,
    });
    setSelectedCustodianId(null);
    setFormErrors([]);
    setSelectedTrackageRailroad(0);
    setSelectedTrackageSubdivisions([]);
    setShowModal(true);
  };

  const handleEdit = async (subdivision: Subdivision) => {
    setModalMode('edit');
    setSelectedSubdivision(subdivision);
    setFormData({
      name: subdivision.name,
      railroadID: subdivision.railroadID,
      dpuCapable: subdivision.dpuCapable,
      localTrainAddressIDs: subdivision.localTrainAddressIDs || '',
      custodianId: subdivision.custodianId ?? null,
    });
    setSelectedCustodianId(subdivision.custodianId ?? null);
    setFormErrors([]);

    // Load trackage rights for this subdivision
    const result = await getTrackageRights(subdivision.id);
    if (result.errors.length === 0 && result.data) {
      setSelectedTrackageSubdivisions(result.data.map(tr => tr.toSubdivisionID));
      // Set initial railroad if trackage rights exist
      if (result.data.length > 0) {
        const firstRailroad = railroads.find(r => r.name === result.data[0].toRailroadName);
        if (firstRailroad) {
          setSelectedTrackageRailroad(firstRailroad.id);
        }
      } else {
        setSelectedTrackageRailroad(0);
      }
    } else {
      setSelectedTrackageRailroad(0);
      setSelectedTrackageSubdivisions([]);
    }

    setShowModal(true);
  };

  const handleDelete = async (id: number) => {
    if (!confirm('Are you sure you want to delete this subdivision?')) {
      return;
    }

    const result = await deleteSubdivision(id);
    if (result.errors.length > 0) {
      setError(result.errors.join(', '));
    } else {
      await loadData();
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormErrors([]);

    if (!formData.name.trim()) {
      setFormErrors(['Name is required']);
      return;
    }

    if (formData.railroadID === 0) {
      setFormErrors(['Railroad is required']);
      return;
    }

    // Validate Local Train Address IDs
    if (formData.localTrainAddressIDs && formData.localTrainAddressIDs.trim()) {
      const addressIDs = formData.localTrainAddressIDs
        .split(/[\r\n,]+/)
        .map(id => id.trim())
        .filter(id => id.length > 0);

      const invalidIDs = addressIDs.filter(id => {
        // Check if it's numeric and no longer than 6 digits
        return !/^\d{1,6}$/.test(id);
      });

      if (invalidIDs.length > 0) {
        setFormErrors([`Invalid Address IDs: ${invalidIDs.join(', ')}. Each ID must be numeric and no longer than 6 digits.`]);
        return;
      }
    }

    if (modalMode === 'create') {
      const result = await createSubdivision({ ...formData, custodianId: selectedCustodianId });
      if (result.errors.length > 0) {
        setFormErrors(result.errors);
      } else {
        // If subdivision created successfully and trackage rights selected, save them
        if (result.data && selectedTrackageSubdivisions.length > 0) {
          await replaceTrackageRights(result.data.id, selectedTrackageSubdivisions);
        }
        setShowModal(false);
        await loadData();
      }
    } else if (selectedSubdivision) {
      let updateData: UpdateSubdivision;
      if (isCustodian) {
        // Only update localTrainAddressIDs for custodians
        updateData = {
          id: selectedSubdivision.id,
          railroadID: selectedSubdivision.railroadID,
          dpuCapable: selectedSubdivision.dpuCapable,
          name: selectedSubdivision.name,
          localTrainAddressIDs: formData.localTrainAddressIDs,
          custodianId: selectedSubdivision.custodianId,
        };
      } else {
        updateData = {
          id: selectedSubdivision.id,
          ...formData,
          custodianId: selectedCustodianId,
        };
      }
      const result = await updateSubdivision(selectedSubdivision.id, updateData);
      if (result.errors.length > 0) {
        setFormErrors(result.errors);
      } else {
        // Save trackage rights
        await replaceTrackageRights(selectedSubdivision.id, selectedTrackageSubdivisions);
        setShowModal(false);
        await loadData();
      }
    }
  };

  const handleToggleTrackageSubdivision = (subdivisionID: number) => {
    setSelectedTrackageSubdivisions((prev) => {
      if (prev.includes(subdivisionID)) {
        return prev.filter(id => id !== subdivisionID);
      } else {
        return [...prev, subdivisionID];
      }
    });
  };

  if (loading) {
    return <div className="admin-subdivisions-container"><p>Loading subdivisions...</p></div>;
  }

  return (
    <div className="admin-subdivisions-container">
      {error && <div className="error-message">{error}</div>}
      <div className="admin-controls">
        <div className="search-bar">
          <TextField
            label="Filter by Name or Railroad"
            variant="outlined"
            size="small"
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
            }}
            className="admin-input"
            fullWidth
          />
          <Tooltip title="Clear filters">
            <IconButton
              sx={{ color: '#fff', backgroundColor: '#222', '&:hover': { backgroundColor: '#444' }, height: '40px', width: '40px' }}
              aria-label="clear filters"
              onClick={() => {
                setSearchTerm('');
              }}
            >
              <ClearIcon />
            </IconButton>
          </Tooltip>
        </div>
        <div className="right-controls">
          {!isCustodian && (
            <button className="btn-primary" onClick={handleCreate}>
              Add Subdivision
            </button>
          )}
        </div>
      </div>
      <div style={{ height: 600, width: '100%', marginTop: 16 }}>
        <DataGrid
          rows={filteredSubdivisions.map(sub => ({
            ...sub,
            custodianName: custodianUsers.find(u => u.id === sub.custodianId)
              ? `${custodianUsers.find(u => u.id === sub.custodianId)!.firstName} ${custodianUsers.find(u => u.id === sub.custodianId)!.lastName}`
              : '',
          }))}
          columns={[
            { field: 'name', headerName: 'Name', flex: 1 },
            { field: 'railroad', headerName: 'Railroad', flex: 1 },
            { field: 'dpuCapable', headerName: 'DPU Capable', flex: 1, valueGetter: (params: any) => params.row && params.row.dpuCapable ? 'Yes' : 'No' },
            { field: 'custodianName', headerName: 'Custodian', flex: 1 },
            {
              field: 'actions',
              headerName: 'Actions',
              flex: 1,
              sortable: false,
              renderCell: (params: any) => {
                const canEdit = !isCustodian || (isCustodian && userId && params.row.custodianId === userId);
                return (
                  <>
                    {canEdit && (
                      <button
                        className="btn-edit"
                        onClick={() => handleEdit(params.row)}
                      >
                        Edit
                      </button>
                    )}
                    {!isCustodian && (
                      <button className="btn-delete" onClick={() => handleDelete(params.row.id)}>Delete</button>
                    )}
                  </>
                );
              },
            },
          ]}
          pageSizeOptions={[10, 25, 50]}
          initialState={{
            pagination: { paginationModel: { pageSize: 25, page: 0 } },
          }}
          disableRowSelectionOnClick
          sx={adminDataGridSx}
        />
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <h2>{modalMode === 'create' ? 'Add Subdivision' : 'Edit Subdivision'}</h2>
            <form onSubmit={handleSubmit}>
              {formErrors.length > 0 && (
                <div className="error-message">
                  {formErrors.map((err, idx) => (
                    <div key={idx}>{err}</div>
                  ))}
                </div>
              )}
              <div className="form-group">
                <label htmlFor="name">Name:</label>
                <input
                  type="text"
                  id="name"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  required
                  readOnly={isCustodian && modalMode === 'edit'}
                  disabled={isCustodian && modalMode === 'edit'}
                  style={isCustodian && modalMode === 'edit' ? { backgroundColor: '#222', color: '#888', cursor: 'not-allowed' } : {}}
                />
              </div>
              <div className="form-group">
                <label htmlFor="railroad">Railroad:</label>
                <select
                  id="railroad"
                  value={formData.railroadID}
                  onChange={(e) => setFormData({ ...formData, railroadID: parseInt(e.target.value) })}
                  required
                  disabled={isCustodian && modalMode === 'edit'}
                >
                  <option value={0}>-- Select Railroad --</option>
                  {railroads.map((railroad) => (
                    <option key={railroad.id} value={railroad.id}>
                      {railroad.name}
                    </option>
                  ))}
                </select>
              </div>
              {!isCustodian && (
                <div className="form-group">
                  <label htmlFor="trackageRailroad">Trackage Rights on Railroad:</label>
                  <select
                    id="trackageRailroad"
                    value={selectedTrackageRailroad}
                    onChange={(e) => {
                      setSelectedTrackageRailroad(parseInt(e.target.value) || 0);
                    }}
                  >
                    <option value={0}>-- None / Select a railroad --</option>
                    {railroads
                      .filter(r => r.id !== formData.railroadID)
                      .map(railroad => (
                        <option key={railroad.id} value={railroad.id}>
                          {railroad.name}
                        </option>
                      ))}
                  </select>
                </div>
              )}
              {!isCustodian && selectedTrackageRailroad > 0 && (
                <div className="form-group">
                  <label>Select Subdivisions to Grant Trackage Rights:</label>
                  <div className="checkbox-list">
                    {subdivisions
                      .filter(s => s.railroadID === selectedTrackageRailroad)
                      .map(subdivision => (
                        <div key={subdivision.id} className="checkbox-item">
                          <input
                            type="checkbox"
                            id={`trackage-${subdivision.id}`}
                            checked={selectedTrackageSubdivisions.includes(subdivision.id)}
                            onChange={() => handleToggleTrackageSubdivision(subdivision.id)}
                          />
                          <label htmlFor={`trackage-${subdivision.id}`}>
                            {subdivision.name}
                          </label>
                        </div>
                      ))}
                  </div>
                </div>
              )}
              {!isCustodian && (
                <div className="form-group checkbox-group">
                  <label htmlFor="dpuCapable">
                    <input
                      type="checkbox"
                      id="dpuCapable"
                      checked={formData.dpuCapable}
                      onChange={(e) => setFormData({ ...formData, dpuCapable: e.target.checked })}
                    />
                    DPU Capable
                  </label>
                </div>
              )}
              <div className="form-group">
                <label htmlFor="localTrainAddressIDs">Local Train Address IDs:</label>
                <div className="form-help">Enter Address IDs that are considered local trains in this subdivision. Separate with commas or new lines.</div>
                <textarea
                  id="localTrainAddressIDs"
                  value={formData.localTrainAddressIDs || ''}
                  onChange={(e) => setFormData({ ...formData, localTrainAddressIDs: e.target.value })}
                  rows={4}
                />
              </div>
              <div className="modal-actions">
                <button type="submit" className="btn-primary">
                  {modalMode === 'create' ? 'Create' : 'Update'}
                </button>
                <button type="button" className="btn-secondary" onClick={() => setShowModal(false)}>
                  Cancel
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
