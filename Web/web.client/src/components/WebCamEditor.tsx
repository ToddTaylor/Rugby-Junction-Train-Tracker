import React, { useState } from "react";
import { findLiveVideo } from "../services/youtube";
import { MAX_WEBCAMS, WebCamEntry, defaultEntries, padEntries, parseVideoId } from "../utils/webcamConfig";

interface RowStatus {
    kind: "busy" | "ok" | "warn" | "error";
    text: string;
}

interface WebCamEditorProps {
    entries: WebCamEntry[];
    onSave: (entries: WebCamEntry[]) => void;
}

const WebCamEditor: React.FC<WebCamEditorProps> = ({ entries, onSave }) => {
    const [open, setOpen] = useState(false);
    const [rows, setRows] = useState<WebCamEntry[]>(() => padEntries(entries));
    const [statuses, setStatuses] = useState<Record<number, RowStatus>>({});

    const setStatus = (index: number, status: RowStatus | null) =>
        setStatuses(prev => {
            const next = { ...prev };
            if (status) next[index] = status;
            else delete next[index];
            return next;
        });

    const updateRow = (index: number, patch: Partial<WebCamEntry>) =>
        setRows(prev => prev.map((row, i) => (i === index ? { ...row, ...patch } : row)));

    const toggle = () => {
        if (!open) {
            setRows(padEntries(entries));
            setStatuses({});
        }
        setOpen(!open);
    };

    const findId = async (index: number) => {
        const source = rows[index].source.trim();
        if (!source) return;
        setStatus(index, { kind: "busy", text: "Searching…" });
        try {
            const found = await findLiveVideo(source);
            if (found) {
                updateRow(index, { videoId: found.videoId });
                setStatus(index, { kind: "ok", text: found.title ? `Found: ${found.title}` : "Found" });
            } else {
                setStatus(index, { kind: "warn", text: "No live stream found. Enter a video ID manually." });
            }
        } catch (e) {
            setStatus(index, { kind: "error", text: e instanceof Error ? e.message : "YouTube lookup failed." });
        }
    };

    // Accept a pasted video URL in the ID field and keep only the ID.
    const tidyVideoId = (index: number) => {
        const value = rows[index].videoId.trim();
        const parsed = parseVideoId(value);
        if (parsed && parsed !== value) updateRow(index, { videoId: parsed });
    };

    const save = () => {
        let valid = true;
        const cleaned = rows.map((row, index) => {
            const raw = row.videoId.trim();
            const videoId = raw ? parseVideoId(raw) : "";
            if (videoId === null) {
                valid = false;
                setStatus(index, { kind: "error", text: "This is not a valid YouTube video ID or URL." });
            }
            return { source: row.source, videoId: videoId ?? raw };
        });
        if (!valid) return;
        onSave(cleaned);
        setOpen(false);
    };

    return (
        <div className={`webcam-editor${open ? " open" : ""}`}>
            <button type="button" className="webcam-editor-toggle" aria-expanded={open} onClick={toggle}>
                {open ? "▾" : "▸"} Edit web cams
            </button>

            {open && (
                <div className="webcam-editor-panel">
                    <p className="webcam-editor-help">
                        Enter search text or a YouTube URL (a video, or a channel such as
                        youtube.com/@SteelHighway/live), then press Find ID. Leave the ID blank to search
                        for a live stream each time the page loads. Up to {MAX_WEBCAMS} web cams.
                    </p>

                    <div className="webcam-editor-rows">
                        {rows.map((row, index) => {
                            const status = statuses[index];
                            return (
                                <div className="webcam-editor-row" key={index}>
                                    <span className="webcam-editor-index">{index + 1}</span>
                                    <input
                                        type="text"
                                        className="webcam-editor-source"
                                        aria-label={`Web cam ${index + 1} search text or URL`}
                                        placeholder="Search text or YouTube URL"
                                        value={row.source}
                                        onChange={e => updateRow(index, { source: e.target.value })}
                                    />
                                    <input
                                        type="text"
                                        className="webcam-editor-id"
                                        aria-label={`Web cam ${index + 1} video ID`}
                                        placeholder="Video ID"
                                        value={row.videoId}
                                        onChange={e => updateRow(index, { videoId: e.target.value })}
                                        onBlur={() => tidyVideoId(index)}
                                    />
                                    <button
                                        type="button"
                                        className="webcam-editor-find"
                                        disabled={!row.source.trim() || status?.kind === "busy"}
                                        onClick={() => findId(index)}
                                    >
                                        Find ID
                                    </button>
                                    <button
                                        type="button"
                                        className="webcam-editor-clear"
                                        aria-label={`Clear web cam ${index + 1}`}
                                        title="Clear"
                                        onClick={() => {
                                            updateRow(index, { source: "", videoId: "" });
                                            setStatus(index, null);
                                        }}
                                    >
                                        ×
                                    </button>
                                    {status && (
                                        <div className={`webcam-editor-status ${status.kind}`}>{status.text}</div>
                                    )}
                                </div>
                            );
                        })}
                    </div>

                    <div className="webcam-editor-actions">
                        <button type="button" className="primary" onClick={save}>Save</button>
                        <button type="button" onClick={() => setOpen(false)}>Cancel</button>
                        <button
                            type="button"
                            className="webcam-editor-reset"
                            onClick={() => {
                                setRows(padEntries(defaultEntries()));
                                setStatuses({});
                            }}
                        >
                            Reset to defaults
                        </button>
                    </div>
                </div>
            )}
        </div>
    );
};

export default WebCamEditor;
