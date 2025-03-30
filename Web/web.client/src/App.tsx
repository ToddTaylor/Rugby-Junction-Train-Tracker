import { BrowserRouter, Route, Routes } from 'react-router-dom'
import RailMap from "./views/RailMap";
import Telemetry from "./views/Telemetry";

function App() {
     return (
         <>
             <BrowserRouter>
                 <Routes>
                     <Route index element={<RailMap />} />
                     <Route path="railmap" element={<RailMap />} />
                     <Route path="telemetry" element={<Telemetry />} />
                     <Route path="*" element={<RailMap />} />
                 </Routes>
             </BrowserRouter>
         </>
    );
}

export default App;