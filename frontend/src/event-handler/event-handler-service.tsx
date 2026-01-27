
export interface EventHandlerItem {
    name: string;
    type: string;
    prefab: string;
}

const getAll = async (): Promise<EventHandlerItem[]> => {

    const baseUrl = process.env.REACT_APP_BACKEND_URL;

    if (!baseUrl) {
        throw new Error('REACT_APP_BACKEND_URL is not defined in environment variables');
    }

    const response = await fetch(`${baseUrl}/event/handler`);

    if (!response.ok) {
        throw new Error(`Failed to fetch event handlers: ${response.status} ${response.statusText}`);
    }

    return response.json();
}

export {
    getAll
}