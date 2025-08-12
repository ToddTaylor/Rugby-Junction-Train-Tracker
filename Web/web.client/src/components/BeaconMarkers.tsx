import React from 'react';
import { Beacon } from '../types/Beacon';
import BeaconMarker from './BeaconMarker';

interface BeaconMarkersProps {
    pins: Beacon[];
    zoom: number;
}

const BeaconMarkers: React.FC<BeaconMarkersProps> = ({ pins: beaconPins, zoom }) => {
    return (
        <>
            {beaconPins.map((beaconPin, idx) => (
                <BeaconMarker key={`beacon-${beaconPin.beaconID ?? idx}-${beaconPin.latitude}-${beaconPin.longitude}`} pin={beaconPin} zoom={zoom} idx={idx} />
            ))}
        </>
    );
};

export default BeaconMarkers;
