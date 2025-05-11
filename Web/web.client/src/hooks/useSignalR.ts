import { useEffect } from "react";
import * as signalR from "@microsoft/signalr";
import { MapAlert } from "../types/types";

export function useSignalR(onItemCreated: (alert: MapAlert) => void) {
    useEffect(() => {
        // Read the SignalR URL from environment variables
        const signalRUrl = import.meta.env.VITE_API_URL + "/hubs/notificationHub";

        console.log("SignalR URL:", signalRUrl);

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(signalRUrl)
            .withAutomaticReconnect()
            .build();

        connection.start()
            .then(() => console.log("SignalR Connected"))
            .catch(err => console.error("SignalR Connection Error: ", err));

        connection.on("MapAlert", (alert) => {
            console.log("New Map Alert:", alert);
            onItemCreated(alert);
        });

        return () => {
            connection.stop();
        };
    }, [onItemCreated]);
}