import { BrowserRouter, Route, Routes } from 'react-router-dom'
import RailMap from "./views/RailMap";
import MapPinsLog from './views/MapPinsLog';

function App() {
     return (
         <>
             <BrowserRouter>
                 <Routes>
                     <Route index element={<RailMap />} />
                     <Route path="railmap" element={<RailMap />} />
                     <Route path="mappinslog" element={<MapPinsLog />} />
                     <Route path="*" element={<RailMap />} />
                 </Routes>
             </BrowserRouter>
         </>
    );
}

export default App;