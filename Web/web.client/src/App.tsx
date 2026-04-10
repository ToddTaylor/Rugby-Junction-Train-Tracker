import { BrowserRouter, Route, Routes, Navigate } from 'react-router-dom'
import RailMap from "./views/RailMap";
import MapPinsLog from './views/MapPinsLog';
import WebCams from './views/WebCams';
import Login from './views/Login';
import Admin from './views/Admin';
import AdminUsers from './views/AdminUsers';
import AdminRailroads from './views/AdminRailroads';
import AdminBeacons from './views/AdminBeacons';
import AdminBeaconRailroads from './views/AdminBeaconRailroads';
import { AdminSubdivisions } from './views/AdminSubdivisions';
import PrivateRoute from './components/PrivateRoute';
import AdminRoute from './components/AdminRoute';
import AdminTelemetryLog from './views/AdminTelemetryLog';
import { useEffect } from 'react';
import { getUsers } from './api/users';
import { useAuth } from './hooks/useAuth';

function App() {
    const { session } = useAuth();
    // Only call protected API if user is authenticated to ensure inactive users are logged out
    useEffect(() => {
        if (session) {
            getUsers();
        }
    }, [session]);

    return (
        <>
            <BrowserRouter>
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
                        <Route path="subdivisions" element={<AdminSubdivisions />} />
                        <Route path="telemetry" element={<AdminTelemetryLog />} />
                        <Route path="users" element={<AdminUsers />} />
                    </Route>
                    <Route path="*" element={<PrivateRoute><RailMap /></PrivateRoute>} />
                </Routes>
            </BrowserRouter>
        </>
    );
}

export default App;