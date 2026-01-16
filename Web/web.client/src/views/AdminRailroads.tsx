import { useState, useEffect, useMemo } from 'react';
import { Railroad, CreateRailroad, UpdateRailroad } from '../types/Railroad';
import { getRailroads, createRailroad, updateRailroad, deleteRailroad } from '../api/railroads';
import TextField from '@mui/material/TextField';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import ClearIcon from '@mui/icons-material/Clear';
import './AdminRailroads.css';

type SortField = 'name' | 'createdAt';
type SortDirection = 'asc' | 'desc' | null;

const AdminRailroads = () => {
  const [railroads, setRailroads] = useState<Railroad[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [showModal, setShowModal] = useState(false);
  const [editingRailroad, setEditingRailroad] = useState<Railroad | null>(null);
  const [formData, setFormData] = useState<CreateRailroad>({ name: '' });
  const [searchTerm, setSearchTerm] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [sortField, setSortField] = useState<SortField>('name');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');
  const itemsPerPage = 10;

  useEffect(() => {
    loadRailroads();
  }, []);

  const loadRailroads = async () => {
    setLoading(true);
    const response = await getRailroads();
    if (response.errors.length > 0) {
      setError(response.errors.join(', '));
    } else if (response.data) {
      setRailroads(response.data);
    }
    setLoading(false);
  };

  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : sortDirection === 'desc' ? null : 'asc');
    } else {
      setSortField(field);
      setSortDirection('asc');
    }
  };

  const sortedRailroads = useMemo(() => {
    let sorted = [...railroads];

    if (searchTerm) {
      sorted = sorted.filter(railroad =>
        railroad.name.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (sortDirection) {
      sorted.sort((a, b) => {
        let aVal: string | number = a[sortField];
        let bVal: string | number = b[sortField];

        if (sortField === 'createdAt') {
          aVal = new Date(aVal).getTime();
          bVal = new Date(bVal).getTime();
        } else {
          aVal = (aVal as string).toLowerCase();
          bVal = (bVal as string).toLowerCase();
        }

        if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
        if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
        return 0;
      });
    }

    return sorted;
  }, [railroads, searchTerm, sortField, sortDirection]);

  const getSortIcon = (field: SortField) => {
    const icon = sortField !== field ? '⇅' : sortDirection === 'asc' ? '⬆' : '⬇';
    return <span style={{ fontSize: '1.2em', marginLeft: '0.3em' }}>{icon}</span>;
  };

  const paginatedRailroads = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage;
    return sortedRailroads.slice(startIndex, startIndex + itemsPerPage);
  }, [sortedRailroads, currentPage]);

  const totalPages = Math.ceil(sortedRailroads.length / itemsPerPage);

  const handleAdd = () => {
    setEditingRailroad(null);
    setFormData({ name: '' });
    setError(undefined);
    setShowModal(true);
  };

  const handleEdit = (railroad: Railroad) => {
    setEditingRailroad(railroad);
    setFormData({ name: railroad.name });
    setError(undefined);
    setShowModal(true);
  };

  const handleDelete = async (id: number) => {
    if (!confirm('Are you sure you want to delete this railroad?')) return;

    const response = await deleteRailroad(id);
    if (response.errors.length > 0) {
      setError(response.errors.join(', '));
    } else {
      setRailroads(railroads.filter(r => r.id !== id));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(undefined);

    if (!formData.name.trim()) {
      setError('Name is required');
      return;
    }

    if (formData.name.length > 100) {
      setError('Name must be 100 characters or less');
      return;
    }

    if (editingRailroad) {
      const updateData: UpdateRailroad = {
        id: editingRailroad.id,
        name: formData.name,
      };
      const response = await updateRailroad(editingRailroad.id, updateData);
      if (response.errors.length > 0) {
        setError(response.errors.join(', '));
      } else if (response.data) {
        setRailroads(railroads.map(r => r.id === editingRailroad.id ? response.data! : r));
        setShowModal(false);
      }
    } else {
      const response = await createRailroad(formData);
      if (response.errors.length > 0) {
        setError(response.errors.join(', '));
      } else if (response.data) {
        setRailroads([...railroads, response.data]);
        setShowModal(false);
      }
    }
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const year = date.getFullYear();
    return `${month}/${day}/${year}`;
  };

  if (loading) return <div className="admin-loading">Loading...</div>;

  return (
    <div className="admin-railroads">
      <div className="admin-header">
        <h1>Railroads</h1>
        <button className="btn-primary" onClick={handleAdd}>Add Railroad</button>
      </div>

      <div className="admin-controls">
        <div className="search-container">
          <TextField
            label="Filter by Name"
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
              onClick={() => {
                setSearchTerm('');
              }}
            >
              <ClearIcon />
            </IconButton>
          </Tooltip>
        </div>
        <div className="right-controls">
          <div className="results-info">
            Showing {((currentPage - 1) * itemsPerPage) + 1}-{Math.min(currentPage * itemsPerPage, sortedRailroads.length)} of {sortedRailroads.length} railroads
          </div>
          {totalPages > 1 && (
            <div className="pagination">
              <button
                className="btn-page"
                onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                disabled={currentPage === 1}
              >
                Previous
              </button>
              {Array.from({ length: totalPages }, (_, i) => i + 1).map(page => (
                <button
                  key={page}
                  className={`btn-page ${currentPage === page ? 'active' : ''}`}
                  onClick={() => setCurrentPage(page)}
                >
                  {page}
                </button>
              ))}
              <button
                className="btn-page"
                onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                disabled={currentPage === totalPages}
              >
                Next
              </button>
            </div>
          )}
        </div>
      </div>

      <div className="admin-table-container">
        <table className="admin-table">
          <thead>
            <tr>
              <th className="sortable" onClick={() => handleSort('name')}>
                Name {getSortIcon('name')}
              </th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {paginatedRailroads.map(railroad => (
              <tr key={railroad.id}>
                <td>{railroad.name}</td>
                <td>{formatDate(railroad.createdAt)}</td>
                <td className="actions-cell">
                  <button className="btn-edit" onClick={() => handleEdit(railroad)}>Edit</button>
                  <button className="btn-delete" onClick={() => handleDelete(railroad.id)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <h2>{editingRailroad ? 'Edit Railroad' : 'Add Railroad'}</h2>
            <form onSubmit={handleSubmit}>
              {error && <div className="error-message">{error}</div>}
              
              <div className="form-group">
                <label htmlFor="name">Name *</label>
                <input
                  id="name"
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  maxLength={100}
                  placeholder="Enter railroad name"
                />
              </div>

              <div className="form-actions">
                <button type="button" className="btn-secondary" onClick={() => setShowModal(false)}>
                  Cancel
                </button>
                <button type="submit" className="btn-primary">
                  {editingRailroad ? 'Update' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminRailroads;
