import { BrowserRouter, Route, Routes } from 'react-router-dom'
import RailMap from "./views/RailMap";
import MapPinsLog from './views/MapPinsLog';
import TelemetryLog from './views/TelemetryLog'; // <-- Import the new page

function App() {
     return (
         <>
             <BrowserRouter>
                 <Routes>
                     <Route index element={<RailMap />} />
                     <Route path="railmap" element={<RailMap />} />
                     <Route path="mappinslog" element={<MapPinsLog />} />
                     <Route path="telemetrylog" element={<TelemetryLog />} /> {/* Add this line */}
                     <Route path="*" element={<RailMap />} />
                 </Routes>
             </BrowserRouter>
         </>
    );
}

export default App;