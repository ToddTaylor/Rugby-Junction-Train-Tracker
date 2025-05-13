import { useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";
import { MapAlert } from "../types/types";

export function useSignalR(onItemCreated: (alert: MapAlert) => void) {
    const onItemCreatedRef = useRef(onItemCreated);

    // Keep ref updated
    useEffect(() => {
        onItemCreatedRef.current = onItemCreated;
    }, [onItemCreated]);

    useEffect(() => {
        const signalRUrl = import.meta.env.VITE_API_URL + "/hubs/notificationHub";

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(signalRUrl, {
                // Azure SignalR Service uses WebSockets by default which is not supported
                // in the free tier, only on the Basic and Standard tiers.
                transport: signalR.HttpTransportType.LongPolling
            })
            .withAutomaticReconnect()
            .build();

        const handleMapAlert = (alert: MapAlert) => {
            console.log("New Map Alert:", alert);
            onItemCreatedRef.current(alert);
        };

        connection.on("MapAlert", handleMapAlert);

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
            connection.off("MapAlert", handleMapAlert);
            connection.stop();
        };
    }, []);
}