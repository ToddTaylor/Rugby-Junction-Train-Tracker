import React, { useEffect, useState } from 'react';
import { User, CreateUser, UpdateUser } from '../types/User';
import { getUsers, createUser, updateUser, deleteUser } from '../api/users';
import './AdminUsers.css';

const formatDate = (dateString: string): string => {
  const date = new Date(dateString);
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  const year = date.getFullYear();
  return `${month}/${day}/${year}`;
};

const AdminUsers: React.FC = () => {
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
    roles: ['Viewer'],
  });

  // Pagination, search, and sort state
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const [sortField, setSortField] = useState<'firstName' | 'lastName' | 'email'>('lastName');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');

  const availableRoles = ['Admin', 'User', 'Viewer'];

  useEffect(() => {
    loadUsers();
  }, []);

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
      roles: ['Viewer'],
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

  const handleSort = (field: 'firstName' | 'lastName' | 'email') => {
    if (sortField === field) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortDirection('asc');
    }
    setCurrentPage(1);
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

  const getSortIcon = (field: 'firstName' | 'lastName' | 'email') => {
    const icon = sortField !== field ? '⇅' : sortDirection === 'asc' ? '⬆' : '⬇';
    return <span style={{ fontSize: '1.2em', marginLeft: '0.3em' }}>{icon}</span>;
  };

  // Paginate sorted users
  const totalPages = Math.ceil(sortedUsers.length / itemsPerPage);
  const startIndex = (currentPage - 1) * itemsPerPage;
  const endIndex = startIndex + itemsPerPage;
  const paginatedUsers = sortedUsers.slice(startIndex, endIndex);

  // Reset to page 1 when search changes
  const handleSearchChange = (query: string) => {
    setSearchQuery(query);
    setCurrentPage(1);
  };

  if (loading) {
    return <div className="admin-users-loading">Loading users...</div>;
  }

  return (
    <div className="admin-users">
      <div className="admin-users-header">
        <h1>User Management</h1>
        <button className="btn-primary" onClick={handleCreate}>
          Add User
        </button>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="users-controls">
        <div className="search-box">
          <input
            type="text"
            placeholder="Search by name or email..."
            value={searchQuery}
            onChange={(e) => handleSearchChange(e.target.value)}
            className="search-input"
          />
          {searchQuery && (
            <button
              className="search-clear-btn"
              onClick={() => handleSearchChange('')}
              title="Clear search"
            >
              ×
            </button>
          )}
        </div>
        <div className="results-info">
          Showing {startIndex + 1}-{Math.min(endIndex, sortedUsers.length)} of {sortedUsers.length} users
        </div>
      </div>

      <div className="users-table-container">
        <table className="users-table">
          <thead>
            <tr>
              <th className="sortable" onClick={() => handleSort('lastName')}>
                Last Name {getSortIcon('lastName')}
              </th>
              <th className="sortable" onClick={() => handleSort('firstName')}>
                First Name {getSortIcon('firstName')}
              </th>
              <th className="sortable" onClick={() => handleSort('email')}>
                Email {getSortIcon('email')}
              </th>
              <th>Last Login</th>
              <th>Roles</th>
              <th>Status</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {paginatedUsers.map(user => (
              <tr key={user.id}>
                <td>{user.lastName}</td>
                <td>{user.firstName}</td>
                <td>{user.email}</td>
                <td>{user.lastLogin ? formatDate(user.lastLogin) : 'Never'}</td>
                <td>
                  <div className="roles-badges">
                    {user.roles.map(role => (
                      <span key={role} className="role-badge">{role}</span>
                    ))}
                  </div>
                </td>
                <td>
                  <span className={`status-badge ${user.isActive ? 'active' : 'inactive'}`}>
                    {user.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td>{formatDate(user.createdAt)}</td>
                <td className="actions-cell">
                  <button className="btn-edit" onClick={() => handleEdit(user)}>Edit</button>
                  <button className="btn-delete" onClick={() => handleDelete(user.id)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="pagination">
          <button
            className="pagination-btn"
            onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
            disabled={currentPage === 1}
          >
            Previous
          </button>
          
          <div className="pagination-pages">
            {Array.from({ length: totalPages }, (_, i) => i + 1).map(page => (
              <button
                key={page}
                className={`pagination-page ${currentPage === page ? 'active' : ''}`}
                onClick={() => setCurrentPage(page)}
              >
                {page}
              </button>
            ))}
          </div>

          <button
            className="pagination-btn"
            onClick={() => setCurrentPage(prev => Math.min(totalPages, prev + 1))}
            disabled={currentPage === totalPages}
          >
            Next
          </button>
        </div>
      )}

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
                <div className="roles-checkboxes">
                  {availableRoles.map(role => (
                    <label key={role} className="checkbox-label">
                      <input
                        type="radio"
                        name="role"
                        checked={formData.roles.includes(role)}
                        onChange={() => handleRoleChange(role)}
                      />
                      {role}
                    </label>
                  ))}
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
