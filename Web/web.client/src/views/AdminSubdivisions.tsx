import React, { useState, useEffect, useMemo } from 'react';
import { Subdivision, CreateSubdivision, UpdateSubdivision } from '../types/Subdivision';
import { Railroad } from '../types/Railroad';
import { getSubdivisions, createSubdivision, updateSubdivision, deleteSubdivision } from '../api/subdivisions';
import { getRailroads } from '../api/railroads';
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
  });
  const [formErrors, setFormErrors] = useState<string[]>([]);

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
    });
    setFormErrors([]);
    setShowModal(true);
  };

  const handleEdit = (subdivision: Subdivision) => {
    setModalMode('edit');
    setSelectedSubdivision(subdivision);
    setFormData({
      name: subdivision.name,
      railroadID: subdivision.railroadID,
      dpuCapable: subdivision.dpuCapable,
    });
    setFormErrors([]);
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

    if (modalMode === 'create') {
      const result = await createSubdivision(formData);
      if (result.errors.length > 0) {
        setFormErrors(result.errors);
      } else {
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
        setShowModal(false);
        await loadData();
      }
    }
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
