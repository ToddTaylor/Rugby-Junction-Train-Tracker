import React from 'react';
import { Outlet, Link, useLocation } from 'react-router-dom';
import './Admin.css';
import { useAuth } from '../hooks/useAuth';

const Admin: React.FC = () => {
  const location = useLocation();
  const { session } = useAuth();
  const isAdmin = session?.roles?.includes('Admin');

  // Only show menu items allowed for the current role
  const menuItems = [
    ...(isAdmin ? [
      { path: '/admin/beacons', label: 'Beacons', icon: '📡' },
      { path: '/admin/beacon-railroads', label: 'Beacon Railroads', icon: '🗺️' },
      { path: '/admin/railroads', label: 'Railroads', icon: '🚂' },
      { path: '/admin/users', label: 'Users', icon: '👥' },
    ] : []),
    // Both Admin and Custodian can see these:
    { path: '/admin/subdivisions', label: 'Subdivisions', icon: '🛤️' },
    { path: '/admin/telemetry', label: 'Telemetry Log', icon: '📋' },
  ];

  return (
    <div className="admin-layout">
      <aside className="admin-sidebar">
        <nav className="admin-nav">
          <ul>
            {menuItems.map((item) => (
              <li key={item.path}>
                <Link
                  to={item.path}
                  className={location.pathname === item.path ? 'active' : ''}
                >
                  <span className="menu-icon">{item.icon}</span>
                  <span className="menu-label">{item.label}</span>
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      </aside>
      <main className="admin-content">
        <Outlet />
      </main>
    </div>
  );
};

export default Admin;
