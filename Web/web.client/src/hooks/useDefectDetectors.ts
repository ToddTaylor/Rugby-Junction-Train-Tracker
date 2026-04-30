import { useEffect, useState } from 'react';

export type DefectDetector = {
    id: number;
    lat: number;
    lng: number;
    railroad: string;
    division: string;
    subdivision: string;
    location: string;
    shortName: string;
    milepost: string;
    frequency: string;
    talkOnDefect: boolean;
};

export function useDefectDetectors() {
    const [defectDetectors, setDefectDetectors] = useState<DefectDetector[]>([]);

    useEffect(() => {
        let cancelled = false;

        async function loadDefectDetectors() {
            try {
                const moduleData = await import('../data/wi-defect-detectors.json');
                const json = moduleData?.default ?? moduleData;
                const features = Array.isArray(json?.features) ? json.features : [];
                const normalized = features
                    .map((feature: any) => {
                        const properties = feature?.properties ?? {};
                        const coordinates = feature?.geometry?.coordinates;
                        const isActive = properties.active === undefined || Number(properties.active) === 1;

                        return {
                            id: Number(properties.detector_id),
                            lat: Array.isArray(coordinates) ? Number(coordinates[1]) : Number.NaN,
                            lng: Array.isArray(coordinates) ? Number(coordinates[0]) : Number.NaN,
                            railroad: String(properties.name ?? '').trim(),
                            division: String(properties.division ?? '').trim(),
                            subdivision: String(properties.subdivision ?? '').trim(),
                            location: String(properties.location ?? '').trim(),
                            shortName: String(properties.short_name ?? '').trim(),
                            milepost: String(properties.milepost ?? '').trim(),
                            frequency: String(properties.frequency ?? '').trim(),
                            talkOnDefect: Number(properties.talk_on_defect) === 1,
                            isActive
                        };
                    })
                    .filter((item: DefectDetector & { isActive: boolean }) => (
                        item.isActive
                        && Number.isFinite(item.id)
                        && Number.isFinite(item.lat)
                        && Number.isFinite(item.lng)
                    ))
                    .map(({ isActive: _isActive, ...item }: DefectDetector & { isActive: boolean }) => item);

                if (!cancelled) {
                    setDefectDetectors(normalized);
                }
            } catch (error: any) {
                console.error('Error loading defect detectors:', error);
            }
        }

        loadDefectDetectors();
        return () => {
            cancelled = true;
        };
    }, []);

    return { defectDetectors };
}