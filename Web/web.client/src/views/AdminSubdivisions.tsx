import React, { useState, useEffect, useMemo } from 'react';
import { Subdivision, CreateSubdivision, UpdateSubdivision } from '../types/Subdivision';
import { Railroad } from '../types/Railroad';
import { getSubdivisions, createSubdivision, updateSubdivision, deleteSubdivision } from '../api/subdivisions';
import { getRailroads } from '../api/railroads';
import { getTrackageRights, replaceTrackageRights } from '../api/subdivisionTrackageRights';
import './AdminSubdivisions.css';

type SortField = 'name' | 'railroad';
type SortDirection = 'asc' | 'desc' | null;

export const AdminSubdivisions: React.FC = () => {
  const [subdivisions, setSubdivisions] = useState<Subdivision[]>([]);
  const [railroads, setRailroads] = useState<Railroad[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [sortField, setSortField] = useState<SortField | null>('name');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');
  const [currentPage, setCurrentPage] = useState(1);
  const [showModal, setShowModal] = useState(false);
  const [modalMode, setModalMode] = useState<'create' | 'edit'>('create');
  const [selectedSubdivision, setSelectedSubdivision] = useState<Subdivision | null>(null);
  const [formData, setFormData] = useState<CreateSubdivision>({
    name: '',
    railroadID: 0,
    dpuCapable: false,
    localTrainAddressIDs: '',
  });
  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [selectedTrackageRailroad, setSelectedTrackageRailroad] = useState<number>(0);
  const [selectedTrackageSubdivisions, setSelectedTrackageSubdivisions] = useState<number[]>([]);

  const itemsPerPage = 10;

  useEffect(() => {
    loadData();
  }, []);

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

  const handleSort = (field: SortField) => {
    if (sortField === field) {
      if (sortDirection === 'asc') {
        setSortDirection('desc');
      } else if (sortDirection === 'desc') {
        setSortDirection(null);
        setSortField(null);
      } else {
        setSortDirection('asc');
      }
    } else {
      setSortField(field);
      setSortDirection('asc');
    }
    setCurrentPage(1);
  };

  const sortedSubdivisions = useMemo(() => {
    let filtered = subdivisions.filter(
      (sub) =>
        sub.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        sub.railroad.toLowerCase().includes(searchTerm.toLowerCase())
    );

    if (sortField && sortDirection) {
      filtered = [...filtered].sort((a, b) => {
        const aValue = a[sortField];
        const bValue = b[sortField];
        const modifier = sortDirection === 'asc' ? 1 : -1;
        return aValue.localeCompare(bValue) * modifier;
      });
    }

    return filtered;
  }, [subdivisions, searchTerm, sortField, sortDirection]);

  const paginatedSubdivisions = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage;
    return sortedSubdivisions.slice(startIndex, startIndex + itemsPerPage);
  }, [sortedSubdivisions, currentPage]);

  const totalPages = Math.ceil(sortedSubdivisions.length / itemsPerPage);

  const handleCreate = () => {
    setModalMode('create');
    setSelectedSubdivision(null);
    setFormData({
      name: '',
      railroadID: railroads.length > 0 ? railroads[0].id : 0,
      dpuCapable: false,
      localTrainAddressIDs: '',
    });
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
    });
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
      const result = await createSubdivision(formData);
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
      const updateData: UpdateSubdivision = {
        id: selectedSubdivision.id,
        ...formData,
      };
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

  const getSortIcon = (field: SortField) => {
    const icon = sortField !== field ? '⇅' : sortDirection === 'asc' ? '⬆' : '⬇';
    return <span style={{ fontSize: '1.2em', marginLeft: '0.3em' }}>{icon}</span>;
  };

  if (loading) {
    return <div className="admin-subdivisions-container"><p>Loading subdivisions...</p></div>;
  }

  return (
    <div className="admin-subdivisions-container">
      <div className="admin-header">
        <h1>Subdivisions</h1>
        <button className="btn-primary" onClick={handleCreate}>
          Add Subdivision
        </button>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="admin-controls">
        <div className="search-bar">
          <input
            type="text"
            placeholder="Search subdivisions..."
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setCurrentPage(1);
            }}
          />
        </div>
        <div className="right-controls">
          <div className="results-info">
            Showing {((currentPage - 1) * itemsPerPage) + 1}-{Math.min(currentPage * itemsPerPage, sortedSubdivisions.length)} of {sortedSubdivisions.length} subdivisions
          </div>
          {totalPages > 1 && (
            <div className="pagination">
              {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                <button
                  key={page}
                  className={currentPage === page ? 'active' : ''}
                  onClick={() => setCurrentPage(page)}
                >
                  {page}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      <table className="subdivisions-table">
        <thead>
          <tr>
            <th onClick={() => handleSort('name')} className="sortable">
              Name {getSortIcon('name')}
            </th>
            <th onClick={() => handleSort('railroad')} className="sortable">
              Railroad {getSortIcon('railroad')}
            </th>
            <th>DPU Capable</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {paginatedSubdivisions.map((subdivision) => (
            <tr key={subdivision.id}>
              <td>{subdivision.name}</td>
              <td>{subdivision.railroad}</td>
              <td>{subdivision.dpuCapable ? 'Yes' : 'No'}</td>
              <td>
                <button
                  className="btn-edit"
                  onClick={() => handleEdit(subdivision)}
                >
                  Edit
                </button>
                <button
                  className="btn-delete"
                  onClick={() => handleDelete(subdivision.id)}
                >
                  Delete
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

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
                  onChange={(e) =>
                    setFormData({ ...formData, name: e.target.value })
                  }
                  required
                />
              </div>
              <div className="form-group">
                <label htmlFor="railroad">Railroad:</label>
                <select
                  id="railroad"
                  value={formData.railroadID}
                  onChange={(e) =>
                    setFormData({ ...formData, railroadID: parseInt(e.target.value) })
                  }
                  required
                >
                  <option value={0}>-- Select Railroad --</option>
                  {railroads.map((railroad) => (
                    <option key={railroad.id} value={railroad.id}>
                      {railroad.name}
                    </option>
                  ))}
                </select>
              </div>
              
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

              {selectedTrackageRailroad > 0 && (
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
              
              <div className="form-group checkbox-group">
                <label htmlFor="dpuCapable">
                  <input
                    type="checkbox"
                    id="dpuCapable"
                    checked={formData.dpuCapable}
                    onChange={(e) =>
                      setFormData({ ...formData, dpuCapable: e.target.checked })
                    }
                  />
                  DPU Capable
                </label>
              </div>
              <div className="form-group">
                <label htmlFor="localTrainAddressIDs">Local Train Address IDs:</label>
                <div className="form-help">Enter Address IDs that are considered local trains in this subdivision. Separate with commas or new lines.</div>
                <textarea
                  id="localTrainAddressIDs"
                  value={formData.localTrainAddressIDs || ''}
                  onChange={(e) =>
                    setFormData({ ...formData, localTrainAddressIDs: e.target.value })
                  }
                  placeholder="Enter comma or line-separated Address IDs (e.g., 1234, 5678 or one per line)"
                  rows={4}
                />
              </div>
              <div className="modal-actions">
                <button type="submit" className="btn-primary">
                  {modalMode === 'create' ? 'Create' : 'Update'}
                </button>
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => setShowModal(false)}
                >
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
