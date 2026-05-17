import { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { parseSessionRoles } from '../utils/roles';

interface AdminRouteProps {
  children: ReactNode;
}

const AdminRoute: React.FC<AdminRouteProps> = ({ children }) => {
  const { session } = useAuth();

  if (!session) {
    return <Navigate to="/login" replace />;
  }

  // Allow Admin or Custodian (case-insensitive)
  const { isAdmin, isCustodian } = parseSessionRoles(session.roles);

  if (!isAdmin && !isCustodian) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
};

export default AdminRoute;
