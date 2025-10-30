import { BrowserRouter, Route, Routes } from 'react-router-dom'
import RailMap from "./views/RailMap";
import MapPinsLog from './views/MapPinsLog';
import TelemetryLog from './views/TelemetryLog'; // <-- Import the new page
import WebCams from './views/WebCams'; // <-- Add this import

function App() {
    // NOTE: RailMap manages stale refresh hook for live telemetry. App-level refresh removed to avoid duplicate reload logic.
    return (
        <>
            <BrowserRouter>
                <Routes>
                    <Route index element={<RailMap />} />
                    <Route path="railmap" element={<RailMap />} />
                    <Route path="mappinslog" element={<MapPinsLog />} />
                    <Route path="telemetrylog" element={<TelemetryLog />} />
                    <Route path="webcams" element={<WebCams />} /> {/* Add this line */}
                    <Route path="*" element={<RailMap />} />
                </Routes>
            </BrowserRouter>
        </>
    );
}

export default App;