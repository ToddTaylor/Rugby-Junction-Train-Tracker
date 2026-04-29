import { useEffect, useState } from 'react';

export type MilepostPoint = {
    lat: number;
    lng: number;
    milepost: number;
};

export function useMileposts() {
    const [milepostPoints, setMilepostPoints] = useState<MilepostPoint[]>([]);

    useEffect(() => {
        let cancelled = false;

        async function loadMileposts() {
            try {
                const moduleData = await import('../data/wi-mileposts-integer.min.json');
                const json = moduleData?.default ?? moduleData;
                const items = Array.isArray(json?.mileposts) ? json.mileposts : [];
                const normalized = items
                    .map((item: any) => {
                        if (Array.isArray(item) && item.length >= 3) {
                            return {
                                lat: Number(item[0]),
                                lng: Number(item[1]),
                                milepost: Math.round(Number(item[2]))
                            };
                        }
                        return {
                            lat: Number(item?.lat),
                            lng: Number(item?.lng),
                            milepost: Math.round(Number(item?.milepost))
                        };
                    })
                    .filter((item: MilepostPoint) => Number.isFinite(item.lat) && Number.isFinite(item.lng) && Number.isFinite(item.milepost));

                if (!cancelled) {
                    setMilepostPoints(normalized);
                }
            } catch (error: any) {
                console.error('Error loading milepost labels:', error);
            }
        }

        loadMileposts();
        return () => {
            cancelled = true;
        };
    }, []);

    return { milepostPoints };
}
