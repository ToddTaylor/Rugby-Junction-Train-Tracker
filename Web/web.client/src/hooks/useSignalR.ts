import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { MapPin } from "../types/MapPin";
import { Beacon } from "../types/Beacon";

const beaconUpdateMethodName = "BeaconUpdate";
const mapPinUpdateMethodName = "MapPinUpdate";

// Accept handlers for multiple events
export function useSignalR(handlers: {
    MapPinUpdate?: (mapPin: MapPin) => void;
    BeaconUpdate?: (beacons: Beacon[]) => void;
}) {
    const handlersRef = useRef(handlers);
    const [connectionState, setConnectionState] = useState<signalR.HubConnectionState | null>(null);
    const connectionRef = useRef<signalR.HubConnection | null>(null);

    // Keep ref updated
    useEffect(() => {
        handlersRef.current = handlers;
    }, [handlers]);

    useEffect(() => {
        const signalRUrl = import.meta.env.VITE_API_URL + "/hubs/notificationHub";

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(signalRUrl, {
                transport: signalR.HttpTransportType.WebSockets
            })
            .withAutomaticReconnect()
            .build();
        connectionRef.current = connection;
        setConnectionState(connection.state);

        // Register handlers if provided
        if (handlersRef.current.MapPinUpdate) {
            connection.on(mapPinUpdateMethodName, (mapPin: MapPin) => {
                //console.log("New Map Pin:", mapPin);
                handlersRef.current.MapPinUpdate?.(mapPin);
            });
        }
        if (handlersRef.current.BeaconUpdate) {
            connection.on(beaconUpdateMethodName, (payload: Beacon | Beacon[]) => {
                const arr = Array.isArray(payload) ? payload : [payload];
                handlersRef.current.BeaconUpdate?.(arr);
            });
        }

        connection.onclose(error => {
            if (error) {
                console.error("SignalR Connection closed with error:", error);
            } else {
                console.log("SignalR Connection closed.");
            }
        });

        connection.onreconnecting(error => {
            console.warn("SignalR reconnecting...", error);
            setConnectionState(connection.state);
        });

        connection.onreconnected(connectionId => {
            console.log("SignalR reconnected. Connection ID:", connectionId);
            setConnectionState(connection.state);
        });

        connection.start()
            .then(() => {
                console.log("SignalR Connected");
                setConnectionState(connection.state);
            })
            .catch(err => {
                console.error("SignalR Connection Error: ", err);
                setConnectionState(connection.state);
            });

        return () => {
            connection.off(mapPinUpdateMethodName);
            connection.off(beaconUpdateMethodName);
            connection.stop();
        };
    }, []);

    return { connection: connectionRef.current, connectionState };
}