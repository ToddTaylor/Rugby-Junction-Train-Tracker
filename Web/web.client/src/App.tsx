import { BrowserRouter, Route, Routes } from 'react-router-dom'
import RailMap from "./views/RailMap";
import MapPinsLog from './views/MapPinsLog';
import TelemetryLog from './views/TelemetryLog';
import WebCams from './views/WebCams';
import Login from './views/Login';
import PrivateRoute from './components/PrivateRoute';

function App() {
    // NOTE: RailMap manages stale refresh hook for live telemetry. App-level refresh removed to avoid duplicate reload logic.
    return (
        <>
            <BrowserRouter>
                <Routes>
                    <Route path="login" element={<Login />} />
                    <Route index element={<PrivateRoute><RailMap /></PrivateRoute>} />
                    <Route path="railmap" element={<PrivateRoute><RailMap /></PrivateRoute>} />
                    <Route path="mappinslog" element={<PrivateRoute><MapPinsLog /></PrivateRoute>} />
                    <Route path="telemetrylog" element={<PrivateRoute><TelemetryLog /></PrivateRoute>} />
                    <Route path="webcams" element={<PrivateRoute><WebCams /></PrivateRoute>} />
                    <Route path="*" element={<PrivateRoute><RailMap /></PrivateRoute>} />
                </Routes>
            </BrowserRouter>
        </>
    );
}

export default App;