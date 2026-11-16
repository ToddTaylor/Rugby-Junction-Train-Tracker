import { BrowserRouter, Route, Routes, Navigate } from 'react-router-dom'
import RailMap from "./views/RailMap";
import Login from './views/Login';
import PrivateRoute from './components/PrivateRoute';
import AdminRoute from './components/AdminRoute';
import ServerAlertListener from './components/ServerAlertListener';
import { lazy, Suspense, useEffect } from 'react';
import { checkSession } from './api/auth';
import { useAuth } from './hooks/useAuth';

// The map and the login page load with the app. Every other page is downloaded the first time it is opened,
// so the map does not wait for the admin pages and their data grids.
const MapPinsLog = lazy(() => import('./views/MapPinsLog'));
const WebCams = lazy(() => import('./views/WebCams'));
const Admin = lazy(() => import('./views/Admin'));
const AdminUsers = lazy(() => import('./views/AdminUsers'));
const AdminRailroads = lazy(() => import('./views/AdminRailroads'));
const AdminBeacons = lazy(() => import('./views/AdminBeacons'));
const AdminBeaconRailroads = lazy(() => import('./views/AdminBeaconRailroads'));
const AdminSubdivisions = lazy(() => import('./views/AdminSubdivisions').then(m => ({ default: m.AdminSubdivisions })));
const AdminAmtrak = lazy(() => import('./views/AdminAmtrak'));
const AdminTelemetryLog = lazy(() => import('./views/AdminTelemetryLog'));
const AdminServerHealth = lazy(() => import('./views/AdminServerHealth'));

function App() {
    const { session } = useAuth();
    // Signs out a user whose token is no longer good, for example after being deactivated.
    useEffect(() => {
        if (session) {
            checkSession();
        }
    }, [session]);

    return (
        <>
            <BrowserRouter>
                <Suspense fallback={null}>
                    <Routes>
                        <Route path="login" element={<Login />} />
                        <Route index element={<PrivateRoute><RailMap /></PrivateRoute>} />
                        <Route path="railmap" element={<PrivateRoute><RailMap /></PrivateRoute>} />
                        <Route path="mappinslog" element={<PrivateRoute><MapPinsLog /></PrivateRoute>} />
                        <Route path="webcams" element={<PrivateRoute><WebCams /></PrivateRoute>} />
                        <Route path="admin" element={<AdminRoute><Admin /></AdminRoute>}>
                            <Route index element={<Navigate to="/admin/telemetry" replace />} />
                            <Route path="beacons" element={<AdminBeacons />} />
                            <Route path="beacon-railroads" element={<AdminBeaconRailroads />} />
                            <Route path="railroads" element={<AdminRailroads />} />
                            <Route path="servers" element={<AdminServerHealth />} />
                            <Route path="amtrak" element={<AdminAmtrak />} />
                            <Route path="subdivisions" element={<AdminSubdivisions />} />
                            <Route path="telemetry" element={<AdminTelemetryLog />} />
                            <Route path="users" element={<AdminUsers />} />
                        </Route>
                        <Route path="*" element={<PrivateRoute><RailMap /></PrivateRoute>} />
                    </Routes>
                </Suspense>
                <ServerAlertListener />
            </BrowserRouter>
        </>
    );
}

export default App;
