
export interface SectorEncounterItem {
    id: string;
    name: string;
    onLoadScript: string;
    onSectorEnterScript: string;
    active: string;
    properties: SectorEncounterProperties;
}

export interface SectorEncounterProperties {
    tags: string[];
}

const getAll = async (): Promise<SectorEncounterItem[]> => {

    const baseUrl = process.env.REACT_APP_BACKEND_URL;

    if (!baseUrl) {
        throw new Error('REACT_APP_BACKEND_URL is not defined in environment variables');
    }

    const response = await fetch(`${baseUrl}/sector/encounter`);

    if (!response.ok) {
        throw new Error(`Failed to fetch sector encounters: ${response.status} ${response.statusText}`);
    }

    return response.json();
}

export {
    getAll
}