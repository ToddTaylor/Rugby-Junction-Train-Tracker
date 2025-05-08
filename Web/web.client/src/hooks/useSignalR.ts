import { useEffect } from "react";
import * as signalR from "@microsoft/signalr";
import { MapAlert } from "../types/types";

export function useSignalR(onItemCreated: (alert: MapAlert) => void) {
    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("https://localhost:44331/hubs/notificationHub") // Adjust to your backend URL
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