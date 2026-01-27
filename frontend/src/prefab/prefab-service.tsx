
export interface PrefabItem {
    id: string;
    name: string;
    folder: string;
    path: string;
}

const getAll = async (): Promise<PrefabItem[]> => {

    const baseUrl = process.env.REACT_APP_BACKEND_URL;

    if (!baseUrl) {
        throw new Error('REACT_APP_BACKEND_URL is not defined in environment variables');
    }

    const response = await fetch(`${baseUrl}/prefab`);

    if (!response.ok) {
        throw new Error(`Failed to fetch prefabs: ${response.status} ${response.statusText}`);
    }

    return response.json();
}

export {
    getAll
}