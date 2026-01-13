import React from 'react';
import { Outlet, Link, useLocation } from 'react-router-dom';
import './Admin.css';

const Admin: React.FC = () => {
  const location = useLocation();

  const menuItems = [
    { path: '/admin/beacons', label: 'Beacons', icon: '📡' },
    { path: '/admin/beacon-railroads', label: 'Beacon Railroads', icon: '🗺️' },
    { path: '/admin/railroads', label: 'Railroads', icon: '🚂' },
    { path: '/admin/subdivisions', label: 'Subdivisions', icon: '🛤️' },
    { path: '/admin/telemetry', label: 'Telemetry Log', icon: '📋' },
    { path: '/admin/users', label: 'Users', icon: '👥' },
    // Future menu items can be added here
    // { path: '/admin/settings', label: 'Settings', icon: '⚙️' },
    // { path: '/admin/reports', label: 'Reports', icon: '📊' },
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
