import React, { useEffect, useState } from 'react'

const AppHeader: React.FC = () => {
  const [isAdminPage, setIsAdminPage] = useState(false);

  useEffect(() => {
    // Check if we're on an admin page
    setIsAdminPage(window.location.pathname.startsWith('/admin'));
    
    // Listen for route changes
    const handleLocationChange = () => {
      setIsAdminPage(window.location.pathname.startsWith('/admin'));
    };
    
    window.addEventListener('popstate', handleLocationChange);
    return () => window.removeEventListener('popstate', handleLocationChange);
  }, []);

  return (
    <header className="app-header">
      <img src="/rugbyjunction.svg" alt="Rugby Junction Train Tracker" className="app-logo" />
      <span className="app-title app-title--responsive">Train Tracker</span>
      {isAdminPage && (
        <a href="/railmap" className="btn-back-header">
          Back to RailMap
        </a>
      )}
    </header>
  );
};

export default AppHeader
