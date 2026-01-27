import {Vector} from "three/examples/jsm/physics/RapierPhysics";

export interface SectorInstanceItem {
    id: string;
    sector: Vector;
    startedAt: string;
    expiresAt: string;
    forceExpiresAt: string;
    createdAt: string;
    onLoadScript: string;
    onSectorEnterScript: string;
}

const baseUrl = process.env.REACT_APP_BACKEND_URL;

const getAll = async (): Promise<SectorInstanceItem[]> => {

    if (!baseUrl) {
        throw new Error('REACT_APP_BACKEND_URL is not defined in environment variables');
    }

    const response = await fetch(`${baseUrl}/sector/instance`);

    if (!response.ok) {
        throw new Error(`Failed to fetch sector instances: ${response.status} ${response.statusText}`);
    }

    return response.json();
}

const forceExpireAll = async (): Promise<void> => {
    if (!baseUrl) {
        throw new Error('REACT_APP_BACKEND_URL is not defined in environment variables');
    }

    const response = await fetch(`${baseUrl}/sector/instance/expire/force/all`, {
        method: 'POST',
    });

    if (!response.ok) {
        throw new Error(`Failed to force expire sectors: ${response.status} ${response.statusText}`);
    }

    return response.json();
}

export {
    getAll,
    forceExpireAll
}