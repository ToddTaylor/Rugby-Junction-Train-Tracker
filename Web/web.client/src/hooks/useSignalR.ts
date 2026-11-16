import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { MapPin } from "../types/MapPin";
import { Beacon } from "../types/Beacon";
import { PassengerMapPin } from "../types/PassengerMapPin";
import { ServerCommandUpdate, ServerStatusChange } from "../types/ServerHealth";
import { getAuthToken } from "../services/auth";
import { LiveConnectionStatus, reconnectPolicy, startWithRetry } from "../services/liveConnection";

const beaconUpdateMethodName = "BeaconUpdate";
const mapPinUpdateMethodName = "MapPinUpdate";
const mapPinRemovedMethodName = "MapPinRemoved";
const trackedPinAddedMethodName = "TrackedPinAdded";
const trackedPinUpdatedMethodName = "TrackedPinUpdated";
const trackedPinRemovedMethodName = "TrackedPinRemoved";
const passengerPinUpdateMethodName = "PassengerPinUpdate";
const passengerPinRemovedMethodName = "PassengerPinRemoved";
const serverStatusChangedMethodName = "ServerStatusChanged";
const serverCommandUpdatedMethodName = "ServerCommandUpdated";

/**
 * Opens the SignalR connection for live updates and keeps it open: it retries without limit when the
 * connection drops or cannot be opened. Returns `status` so a view can tell the user when updates have stopped.
 *
 * Messages sent while the connection was down are not replayed. Pass `Reconnected` to re-fetch whatever the
 * view shows once the connection is back.
 */
export function useSignalR(handlers: {
    /** The connection is open again after being down, or opened only after failed attempts. */
    Reconnected?: () => void;
    MapPinUpdate?: (mapPin: MapPin) => void;
    MapPinRemoved?: (mapPinId: number) => void;
    BeaconUpdate?: (beacons: Beacon[]) => void;
    TrackedPinAdded?: (payload: any) => void;
    TrackedPinUpdated?: (payload: any) => void;
    TrackedPinRemoved?: (mapPinId: number) => void;
    PassengerPinUpdate?: (pin: PassengerMapPin) => void;
    PassengerPinRemoved?: (payload: { trainId?: string; trainNum?: string }) => void;
    ServerStatusChanged?: (change: ServerStatusChange) => void;
    ServerCommandUpdated?: (update: ServerCommandUpdate) => void;
}) {
    const handlersRef = useRef(handlers);
    // null until a connection is attempted; it never is when nobody is signed in.
    const [status, setStatus] = useState<LiveConnectionStatus | null>(null);

    // Keep ref updated
    useEffect(() => {
        handlersRef.current = handlers;
    }, [handlers]);

    useEffect(() => {
        let disposed = false;
        let connection: signalR.HubConnection | null = null;

        const startConnection = async () => {
            const token = await getAuthToken();
            // The effect may have been cleaned up while the token was read; do not open a connection nobody will stop.
            if (disposed) {
                return;
            }
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
                .withAutomaticReconnect(reconnectPolicy)
                .build();
            const hub = connection;
            setStatus('connecting');

            // Opens the connection, retrying until it works. `afterOutage` is true when it was open before.
            const open = async (afterOutage: boolean) => {
                const failedAttempts = await startWithRetry(hub, {
                    isDisposed: () => disposed,
                    onAttemptFailed: error => {
                        console.error("SignalR Connection Error: ", error);
                        setStatus('reconnecting');
                    }
                });
                if (failedAttempts === null) {
                    return;
                }

                console.log("SignalR Connected");
                setStatus('connected');
                if (afterOutage || failedAttempts > 0) {
                    handlersRef.current.Reconnected?.();
                }
            };

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

            if (handlersRef.current.ServerStatusChanged) {
                connection.on(serverStatusChangedMethodName, (payload: ServerStatusChange) => {
                    handlersRef.current.ServerStatusChanged?.(payload);
                });
            }

            if (handlersRef.current.ServerCommandUpdated) {
                connection.on(serverCommandUpdatedMethodName, (payload: ServerCommandUpdate) => {
                    handlersRef.current.ServerCommandUpdated?.(payload);
                });
            }

            connection.onclose(error => {
                if (disposed) {
                    return;
                }

                // Automatic reconnect never gives up, so this is a close it does not cover. Open it again.
                console.error("SignalR Connection closed:", error);
                setStatus('reconnecting');
                open(true);
            });

            connection.onreconnecting(error => {
                console.warn("SignalR reconnecting...", error);
                setStatus('reconnecting');
            });

            connection.onreconnected(connectionId => {
                console.log("SignalR reconnected. Connection ID:", connectionId);
                setStatus('connected');
                handlersRef.current.Reconnected?.();
            });

            await open(false);
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
                connection.off(serverStatusChangedMethodName);
                connection.off(serverCommandUpdatedMethodName);
                connection.stop();
            }
        };
    }, []);

    return { status };
}