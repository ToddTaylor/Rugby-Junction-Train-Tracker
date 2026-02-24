import React, { useEffect, useState } from 'react';
import { useAuth } from '../hooks/useAuth';
import { DataGrid, GridRenderCellParams } from '@mui/x-data-grid';
import { User, CreateUser, UpdateUser } from '../types/User';
import { getUsers, createUser, updateUser, deleteUser } from '../api/users';
import './AdminUsers.css';
import TextField from '@mui/material/TextField';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import ClearIcon from '@mui/icons-material/Clear';

const formatDate = (dateString: string): string => {
  const date = new Date(dateString);
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  const year = date.getFullYear();
  return `${month}/${day}/${year}`;
};

const AdminUsers: React.FC = () => {
    // Role-based access control
    const { session } = useAuth();
    const isAdmin = session?.roles?.includes('Admin');
    const isCustodian = session?.roles?.includes('Custodian');
    if (!isAdmin && !isCustodian) {
      return <div style={{ color: '#e0e0e0', background: '#1a1a1a', padding: '2em' }}>You do not have permission to view this page.</div>;
    }
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showModal, setShowModal] = useState(false);
  const [editingUser, setEditingUser] = useState<User | null>(null);
  const [formData, setFormData] = useState<CreateUser>({
    firstName: '',
    lastName: '',
    email: '',
    isActive: true,
    roles: [],
  });
  const [availableRoles, setAvailableRoles] = useState<string[]>([]);

  // Pagination, search, and sort state
  const [searchQuery, setSearchQuery] = useState('');
  const [sortField] = useState<'firstName' | 'lastName' | 'email'>('lastName');
  const [sortDirection] = useState<'asc' | 'desc'>('asc');


  useEffect(() => {
    loadUsers();
    loadRoles();
  }, []);

  const loadRoles = async () => {
    try {
      const { data, errors } = await (await import('../api/roles')).getRoles();
      if (errors.length > 0) {
        setError(errors.join(', '));
      } else {
        setAvailableRoles(data || []);
      }
    } catch (err) {
      setError('Failed to load roles');
    }
  };

  const loadUsers = async () => {
    setLoading(true);
    setError(null);
    const response = await getUsers();
    if (response.errors.length > 0) {
      setError(response.errors.join(', '));
    } else {
      setUsers(response.data || []);
    }
    setLoading(false);
  };

  const handleCreate = () => {
    setEditingUser(null);
    setFormData({
      firstName: '',
      lastName: '',
      email: '',
      isActive: true,
      roles: availableRoles.length > 0 ? [availableRoles[0]] : [],
    });
    setShowModal(true);
  };

  const handleEdit = (user: User) => {
    setEditingUser(user);
    setFormData({
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email,
      isActive: user.isActive,
      roles: user.roles,
    });
    setShowModal(true);
  };

  const handleDelete = async (id: number) => {
    if (!confirm('Are you sure you want to delete this user?')) return;

    const response = await deleteUser(id);
    if (response.success) {
      setUsers(users.filter(u => u.id !== id));
    } else {
      alert('Error deleting user: ' + response.errors.join(', '));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    // Validate required fields
    if (!formData.firstName.trim()) {
      setError('First name is required');
      return;
    }
    if (!formData.lastName.trim()) {
      setError('Last name is required');
      return;
    }
    if (!formData.email.trim()) {
      setError('Email is required');
      return;
    }

    // Validate name lengths
    if (formData.firstName.length > 25) {
      setError('First name must be 25 characters or less');
      return;
    }
    if (formData.lastName.length > 25) {
      setError('Last name must be 25 characters or less');
      return;
    }

    // Validate email format
    const emailRegex = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
    if (!emailRegex.test(formData.email)) {
      setError('Please enter a valid email address (e.g., user@example.com)');
      return;
    }

    if (editingUser) {
      // Update existing user
      const updateData: UpdateUser = {
        id: editingUser.id,
        ...formData,
      };
      const response = await updateUser(editingUser.id, updateData);
      if (response.errors.length > 0) {
        setError(response.errors.join(', '));
      } else if (response.data) {
        setUsers(users.map(u => u.id === editingUser.id ? response.data! : u));
        setShowModal(false);
      }
    } else {
      // Create new user
      const response = await createUser(formData);
      if (response.errors.length > 0) {
        setError(response.errors.join(', '));
      } else if (response.data) {
        setUsers([...users, response.data]);
        setShowModal(false);
      }
    }
  };

  const handleRoleChange = (role: string) => {
    setFormData(prev => ({
      ...prev,
      roles: [role]
    }));
  };


  // Filter users based on search query
  const filteredUsers = users.filter(user => {
    if (!searchQuery) return true;
    const query = searchQuery.toLowerCase();
    const fullName = `${user.firstName} ${user.lastName}`.toLowerCase();
    return fullName.includes(query) || user.email.toLowerCase().includes(query);
  });

  // Sort filtered users
  const sortedUsers = [...filteredUsers].sort((a, b) => {
    let aVal: string, bVal: string;

    if (sortField === 'firstName') {
      aVal = a.firstName.toLowerCase();
      bVal = b.firstName.toLowerCase();
    } else if (sortField === 'lastName') {
      aVal = a.lastName.toLowerCase();
      bVal = b.lastName.toLowerCase();
    } else {
      aVal = a.email.toLowerCase();
      bVal = b.email.toLowerCase();
    }

    if (sortDirection === 'asc') {
      return aVal.localeCompare(bVal);
    } else {
      return bVal.localeCompare(aVal);
    }
  });

  // DataGrid paging (future-ready)
  const paginatedUsers = sortedUsers; // DataGrid will handle paging

  // Reset to page 1 when search changes
  const handleSearchChange = (query: string) => {
    setSearchQuery(query);
  };

  if (loading) {
    return <div className="admin-users-loading">Loading users...</div>;
  }

  return (
    <div className="admin-users">

      {error && <div className="error-message">{error}</div>}

      <div className="users-controls" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
        <div className="search-box" style={{ display: 'flex', alignItems: 'center', gap: 8, maxWidth: 400 }}>
          <TextField
            label="Filter by Name or Email"
            variant="outlined"
            size="small"
            value={searchQuery}
            onChange={(e) => handleSearchChange(e.target.value)}
            className="admin-input"
            fullWidth={false}
            style={{ width: '100%', minWidth: 200, maxWidth: 400 }}
          />
          <Tooltip title="Clear filters">
            <IconButton
              sx={{ color: '#fff', backgroundColor: '#222', '&:hover': { backgroundColor: '#444' }, height: '40px', width: '40px' }}
              aria-label="clear filters"
              onClick={() => {
                setSearchQuery('');
              }}
            >
              <ClearIcon />
            </IconButton>
          </Tooltip>
        </div>
        {isAdmin && (
          <button className="btn-primary" onClick={handleCreate} style={{ marginLeft: 16 }}>
            Add User
          </button>
        )}
      </div>

      <div className="admin-table-container">
        <div style={{ height: 600, width: '100%' }}>
          <DataGrid
            rows={paginatedUsers.map(u => ({ ...u, id: u.id }))}
            columns={(() => {
              const maskEmail = (email: string) => {
                if (!email) return '';
                // Show first 2 chars, then mask, then show domain
                const [name, domain] = email.split('@');
                if (!name || !domain) return email;
                if (name.length <= 2) return '*'.repeat(name.length) + '@' + domain;
                return name.slice(0, 2) + '***@' + domain;
              };
              const baseColumns = [
                { field: 'lastName', headerName: 'Last Name', width: 160 },
                { field: 'firstName', headerName: 'First Name', width: 160 },
                {
                  field: 'email',
                  headerName: 'Email',
                  width: 220,
                  renderCell: (params: GridRenderCellParams<User, string>) => (
                    isCustodian
                      ? <span>{maskEmail(params.value ?? '')}</span>
                      : <span>{params.value}</span>
                  ),
                },
                {
                  field: 'lastActive',
                  headerName: 'Last Active',
                  width: 140,
                  renderCell: (params: GridRenderCellParams<User, string>) => params.value ? formatDate(params.value) : 'Never',
                },
                {
                  field: 'roles',
                  headerName: 'Roles',
                  width: 180,
                  renderCell: (params: GridRenderCellParams<User, string[]>) => (
                    <div className="roles-badges">
                      {params.value && params.value.map((role: string) => (
                        <span key={role} className="role-badge">{role}</span>
                      ))}
                    </div>
                  ),
                },
                {
                  field: 'isActive',
                  headerName: 'Status',
                  width: 120,
                  renderCell: (params: GridRenderCellParams<User, boolean>) => (
                    <span
                      style={{
                        color: params.value ? '#4caf50' : '#f44336',
                        fontWeight: 600,
                        fontSize: '1rem',
                      }}
                    >
                      {params.value ? 'Active' : 'Inactive'}
                    </span>
                  ),
                },
              ];
              if (isAdmin) {
                baseColumns.push({
                  field: 'actions',
                  headerName: 'Actions',
                  width: 180,
                  renderCell: (params: GridRenderCellParams<User>) => (
                    <div className="actions-cell" style={{ display: 'flex', alignItems: 'center', height: '100%', gap: 8 }}>
                      <button className="btn-edit" style={{ minWidth: 70, padding: '8px 16px', borderRadius: 4, display: 'flex', alignItems: 'center', justifyContent: 'center', height: '36px' }} onClick={() => handleEdit(params.row)}>Edit</button>
                      <button className="btn-delete" style={{ minWidth: 70, padding: '8px 16px', borderRadius: 4, display: 'flex', alignItems: 'center', justifyContent: 'center', height: '36px' }} onClick={() => handleDelete(params.row.id)}>Delete</button>
                    </div>
                  ),
                });
              }
              return baseColumns;
            })()}
            pageSizeOptions={[10, 25, 50]}
            initialState={{
              pagination: { paginationModel: { pageSize: 10, page: 0 } },
            }}
            sx={{
              backgroundColor: '#2a2a2a',
              color: '#e0e0e0',
              border: '1px solid #444',
              borderRadius: 1,
              '& .MuiDataGrid-columnHeaders': {
                backgroundColor: '#333',
                color: '#e0e0e0',
                borderColor: '#444 !important',
              },
              '& .MuiDataGrid-columnHeadersWrapper': {
                backgroundColor: '#333 !important',
                borderColor: '#444 !important',
              },
              '& .MuiDataGrid-columnHeadersInner': {
                backgroundColor: '#333 !important',
              },
              '& .MuiDataGrid-filler': {
                backgroundColor: '#333 !important',
                borderColor: '#444 !important',
              },
              '& .MuiDataGrid-scrollbarFiller': {
                backgroundColor: '#333 !important',
              },
              '& .MuiDataGrid-columnHeader': {
                backgroundColor: '#333 !important',
                color: '#e0e0e0',
                borderColor: '#444 !important',
              },
              '& .MuiDataGrid-columnHeaderTitle': {
                color: '#e0e0e0',
                fontWeight: 600,
              },
              '& .MuiDataGrid-cell': {
                color: '#e0e0e0',
                borderColor: '#444',
              },
              '& .MuiDataGrid-row:hover': {
                backgroundColor: '#3a3a3a',
              },
              '& .MuiDataGrid-row.Mui-selected': {
                backgroundColor: '#1e3a5f !important',
                '&:hover': {
                  backgroundColor: '#0d47a1 !important',
                },
              },
              '& .MuiIconButton-root': {
                color: '#e0e0e0',
              },
              '& .MuiIconButton-root.Mui-disabled': {
                color: '#555 !important',
                opacity: 0.5,
              },
              '& .MuiDataGrid-sortButton, & .MuiIconButton-root.MuiDataGrid-sortButton': {
                background: 'none !important',
                boxShadow: 'none !important',
                borderRadius: '0 !important',
                padding: '0 !important',
                minWidth: '0 !important',
                width: 'auto !important',
                height: 'auto !important',
              },
              '& .MuiTablePagination-root': {
                color: '#e0e0e0',
              },
              '& .MuiTablePagination-toolbar': {
                backgroundColor: '#333',
              },
            }}
          />
        </div>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>{editingUser ? 'Edit User' : 'Create User'}</h2>
              <button className="modal-close" onClick={() => setShowModal(false)}>×</button>
            </div>

            {error && <div className="error-message">{error}</div>}

            <form onSubmit={handleSubmit}>
              <div className="form-group">
                <label htmlFor="firstName">First Name</label>
                <input
                  type="text"
                  id="firstName"
                  value={formData.firstName}
                  onChange={(e) => setFormData({ ...formData, firstName: e.target.value })}
                  maxLength={25}
                />
              </div>

              <div className="form-group">
                <label htmlFor="lastName">Last Name</label>
                <input
                  type="text"
                  id="lastName"
                  value={formData.lastName}
                  onChange={(e) => setFormData({ ...formData, lastName: e.target.value })}
                  maxLength={25}
                />
              </div>

              <div className="form-group">
                <label htmlFor="email">Email</label>
                <input
                  type="text"
                  id="email"
                  value={formData.email}
                  onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                  placeholder="user@example.com"
                />
              </div>

              <div className="form-group">
                <label>Role</label>
                <div className="roles-radio-grid">
                  {availableRoles.length === 0 ? (
                    <span>Loading roles...</span>
                  ) : (
                    availableRoles.map(role => (
                      <div key={role} className="role-radio-row">
                        <input
                          type="radio"
                          name="role"
                          checked={formData.roles.includes(role)}
                          onChange={() => handleRoleChange(role)}
                          className="role-radio-btn"
                        />
                        <span className="role-radio-label-text">{role}</span>
                      </div>
                    ))
                  )}
                </div>
              </div>

              <div className="form-group">
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={formData.isActive}
                    onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                  />
                  Active
                </label>
              </div>

              <div className="modal-actions">
                <button type="button" className="btn-secondary" onClick={() => setShowModal(false)}>
                  Cancel
                </button>
                <button type="submit" className="btn-primary">
                  {editingUser ? 'Update' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminUsers;
