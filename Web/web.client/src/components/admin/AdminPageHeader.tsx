import React from 'react';

type AdminPageHeaderProps = {
  title: string;
  description: string;
  actions?: React.ReactNode;
};

const AdminPageHeader: React.FC<AdminPageHeaderProps> = ({ title, description, actions }) => {
  return (
    <div className="admin-page-header">
      <div className="admin-page-title-block">
        <h1 className="admin-page-title">{title}</h1>
        <p className="admin-page-description">{description}</p>
      </div>
      {actions && <div className="admin-page-actions">{actions}</div>}
    </div>
  );
};

export default AdminPageHeader;
