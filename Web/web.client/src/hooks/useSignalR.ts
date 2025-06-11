import { useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";
import { MapPin, Beacon } from "../types/types";

const beaconUpdateMethodName = "BeaconUpdate";
const mapPinUpdateMethodName = "MapPinUpdate";

// Accept handlers for multiple events
export function useSignalR(handlers: {
    MapPinUpdate?: (mapPin: MapPin) => void;
    BeaconUpdate?: (beacons: Beacon) => void;
}) {
    const handlersRef = useRef(handlers);

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

        // Register handlers if provided
        if (handlersRef.current.MapPinUpdate) {
            connection.on(mapPinUpdateMethodName, (mapPin: MapPin) => {
                console.log("New Map Pin:", mapPin);
                handlersRef.current.MapPinUpdate?.(mapPin);
            });
        }
        if (handlersRef.current.BeaconUpdate) {
            connection.on(beaconUpdateMethodName, (beacon: Beacon) => {
                handlersRef.current.BeaconUpdate?.(beacon);
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
        });

        connection.onreconnected(connectionId => {
            console.log("SignalR reconnected. Connection ID:", connectionId);
        });

        connection.start()
            .then(() => console.log("SignalR Connected"))
            .catch(err => console.error("SignalR Connection Error: ", err));

        return () => {
            connection.off(mapPinUpdateMethodName);
            connection.off(beaconUpdateMethodName);
            connection.stop();
        };
    }, []);
}