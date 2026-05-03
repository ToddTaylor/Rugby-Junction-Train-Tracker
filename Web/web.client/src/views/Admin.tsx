import React, { useEffect, useState } from 'react';
import { Outlet, Link, useLocation } from 'react-router-dom';
import './Admin.css';
import { useAuth } from '../hooks/useAuth';
import { parseSessionRoles } from '../utils/roles';

const SIDEBAR_EXPANDED_KEY = 'admin.sidebar.expanded';

const Admin: React.FC = () => {
  const location = useLocation();
  const { session } = useAuth();
  const { isAdmin } = parseSessionRoles(session?.roles);
  const [isExpanded, setIsExpanded] = useState<boolean>(() => {
    const saved = localStorage.getItem(SIDEBAR_EXPANDED_KEY);
    return saved === null ? true : saved === 'true';
  });

  useEffect(() => {
    localStorage.setItem(SIDEBAR_EXPANDED_KEY, String(isExpanded));
  }, [isExpanded]);

  const handlePinToggle = () => {
    setIsExpanded((prev) => !prev);
  };

  // Only show menu items allowed for the current role
  const menuItems = [
    ...(isAdmin ? [
      { path: '/admin/beacons', label: 'Beacons', icon: '📡' },
      { path: '/admin/beacon-railroads', label: 'Beacon Railroads', icon: '🗺️' },
      { path: '/admin/railroads', label: 'Railroads', icon: '🚂' },
    ] : []),
    // Both Admin and Custodian can see these:
    { path: '/admin/users', label: 'Users', icon: '👥' },
    { path: '/admin/subdivisions', label: 'Subdivisions', icon: '🛤️' },
    { path: '/admin/telemetry', label: 'Telemetry Log', icon: '📋' },
  ];

  return (
    <div className="admin-layout">
      <aside className={`admin-sidebar ${isExpanded ? 'expanded' : 'collapsed'}`}>
        <div className="admin-sidebar-controls">
          <button
            type="button"
            className={`sidebar-pin-btn ${isExpanded ? 'expanded' : 'collapsed'}`}
            onClick={handlePinToggle}
            aria-label={isExpanded ? 'Collapse menu' : 'Expand menu'}
            title={isExpanded ? 'Collapse menu' : 'Expand menu'}
          >
            <span className="pin-icon" aria-hidden="true">📌</span>
          </button>
        </div>
        <nav className="admin-nav">
          <ul>
            {menuItems.map((item) => (
              <li key={item.path}>
                <Link
                  to={item.path}
                  className={location.pathname === item.path ? 'active' : ''}
                  aria-label={!isExpanded ? item.label : undefined}
                  data-menu-label={item.label}
                >
                  <span
                    className="menu-icon"
                    aria-label={!isExpanded ? item.label : undefined}
                  >
                    {item.icon}
                  </span>
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
