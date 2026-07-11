import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { MapPin } from "../types/MapPin";
import { Beacon } from "../types/Beacon";
import { PassengerMapPin } from "../types/PassengerMapPin";
import { getAuthToken } from "../services/auth";

const beaconUpdateMethodName = "BeaconUpdate";
const mapPinUpdateMethodName = "MapPinUpdate";
const mapPinRemovedMethodName = "MapPinRemoved";
const trackedPinAddedMethodName = "TrackedPinAdded";
const trackedPinUpdatedMethodName = "TrackedPinUpdated";
const trackedPinRemovedMethodName = "TrackedPinRemoved";
const passengerPinUpdateMethodName = "PassengerPinUpdate";
const passengerPinRemovedMethodName = "PassengerPinRemoved";

// Accept handlers for multiple events
export function useSignalR(handlers: {
    MapPinUpdate?: (mapPin: MapPin) => void;
    MapPinRemoved?: (mapPinId: number) => void;
    BeaconUpdate?: (beacons: Beacon[]) => void;
    TrackedPinAdded?: (payload: any) => void;
    TrackedPinUpdated?: (payload: any) => void;
    TrackedPinRemoved?: (mapPinId: number) => void;
    PassengerPinUpdate?: (pin: PassengerMapPin) => void;
    PassengerPinRemoved?: (payload: { trainId?: string; trainNum?: string }) => void;
}) {
    const handlersRef = useRef(handlers);
    const [connectionState, setConnectionState] = useState<signalR.HubConnectionState | null>(null);
    const connectionRef = useRef<signalR.HubConnection | null>(null);

    // Keep ref updated
    useEffect(() => {
        handlersRef.current = handlers;
    }, [handlers]);

    useEffect(() => {
        let disposed = false;
        let connection: signalR.HubConnection | null = null;

        const startConnection = async () => {
            const token = await getAuthToken();
            if (!token) {
                console.warn("SignalR connection skipped: missing auth token");
                return;
            }

            const signalRUrl = import.meta.env.VITE_API_URL + "/hubs/notificationhub";
            const options: signalR.IHttpConnectionOptions = {
                transport: signalR.HttpTransportType.WebSockets,
                accessTokenFactory: () => token
            };

            connection = new signalR.HubConnectionBuilder()
                .withUrl(signalRUrl, options)
                .withAutomaticReconnect()
                .build();
            connectionRef.current = connection;
            setConnectionState(connection.state);

            // Register handlers if provided
            if (handlersRef.current.MapPinUpdate) {
                connection.on(mapPinUpdateMethodName, (mapPin: MapPin) => {
                    handlersRef.current.MapPinUpdate?.(mapPin);
                });
            }
            if (handlersRef.current.MapPinRemoved) {
                connection.on(mapPinRemovedMethodName, (payload: any) => {
                    const rawId = typeof payload === "number"
                        ? payload
                        : typeof payload === "string"
                            ? payload
                            : (payload?.mapPinId ?? payload?.id);

                    const id = typeof rawId === "number" ? rawId : Number.parseInt(String(rawId), 10);

                    if (Number.isFinite(id)) {
                        handlersRef.current.MapPinRemoved?.(id);
                    }
                });
            }
            if (handlersRef.current.BeaconUpdate) {
                connection.on(beaconUpdateMethodName, (payload: Beacon | Beacon[]) => {
                    const arr = Array.isArray(payload) ? payload : [payload];
                    handlersRef.current.BeaconUpdate?.(arr);
                });
            }
            if (handlersRef.current.TrackedPinAdded) {
                connection.on(trackedPinAddedMethodName, (payload: any) => {
                    handlersRef.current.TrackedPinAdded?.(payload);
                });
            }
            if (handlersRef.current.TrackedPinUpdated) {
                connection.on(trackedPinUpdatedMethodName, (payload: any) => {
                    handlersRef.current.TrackedPinUpdated?.(payload);
                });
            }
            if (handlersRef.current.TrackedPinRemoved) {
                connection.on(trackedPinRemovedMethodName, (payload: any) => {
                    const id = typeof payload === "number"
                        ? payload
                        : typeof payload === "string"
                            ? parseInt(payload, 10)
                            : (payload?.mapPinId ?? payload?.id);
                    if (Number.isFinite(id)) {
                        handlersRef.current.TrackedPinRemoved?.(Number(id));
                    }
                });
            }
            if (handlersRef.current.PassengerPinUpdate) {
                connection.on(passengerPinUpdateMethodName, (payload: PassengerMapPin) => {
                    handlersRef.current.PassengerPinUpdate?.(payload);
                });
            }
            if (handlersRef.current.PassengerPinRemoved) {
                connection.on(passengerPinRemovedMethodName, (payload: any) => {
                    const trainId = typeof payload === "object"
                        ? (payload?.trainId ?? payload?.TrainId)
                        : undefined;
                    const trainNum = typeof payload === "string"
                        ? payload
                        : (payload?.trainNum ?? payload?.TrainNum);
                    if (typeof trainId === "string" && trainId.trim().length > 0) {
                        handlersRef.current.PassengerPinRemoved?.({ trainId: trainId.trim(), trainNum: typeof trainNum === "string" ? trainNum.trim() : undefined });
                    } else if (typeof trainNum === "string" && trainNum.trim().length > 0) {
                        handlersRef.current.PassengerPinRemoved?.({ trainNum: trainNum.trim() });
                    }
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
                setConnectionState(connection?.state ?? null);
            });

            connection.onreconnected(connectionId => {
                console.log("SignalR reconnected. Connection ID:", connectionId);
                setConnectionState(connection?.state ?? null);
            });

            try {
                await connection.start();
                if (!disposed) {
                    console.log("SignalR Connected");
                    setConnectionState(connection.state);
                }
            }
            catch (err) {
                console.error("SignalR Connection Error: ", err);
                setConnectionState(connection?.state ?? null);
            }
        };

        startConnection();

        return () => {
            disposed = true;
            if (connection) {
                connection.off(mapPinUpdateMethodName);
                connection.off(mapPinRemovedMethodName);
                connection.off(beaconUpdateMethodName);
                connection.off(trackedPinAddedMethodName);
                connection.off(trackedPinUpdatedMethodName);
                connection.off(trackedPinRemovedMethodName);
                connection.off(passengerPinUpdateMethodName);
                connection.off(passengerPinRemovedMethodName);
                connection.stop();
            }
        };
    }, []);

    return { connection: connectionRef.current, connectionState };
}