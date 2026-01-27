
export interface ScriptItem {
    name: string;
    type: string;
    prefab: string;
}

const getAll = async (): Promise<ScriptItem[]> => {

    const baseUrl = process.env.REACT_APP_BACKEND_URL;

    if (!baseUrl) {
        throw new Error('REACT_APP_BACKEND_URL is not defined in environment variables');
    }

    const response = await fetch(`${baseUrl}/script`);

    if (!response.ok) {
        throw new Error(`Failed to fetch scripts: ${response.status} ${response.statusText}`);
    }

    return response.json();
}

export {
    getAll
}