import React, {useEffect, useState} from "react";
import {getAll, EventHandlerItem} from "./event-handler-service"
import {Button, Paper, Stack} from "@mui/material";
import {DataGrid, GridColDef} from "@mui/x-data-grid";
import DashboardContainer from "../dashboard/dashboard-container";

interface PrefabPageProps {}

const EventHandlerPage: React.FC<PrefabPageProps> = () => {

    const [data, setData] = useState<EventHandlerItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<any>(null);

    const fetchData = async () => {
        try {
            setLoading(true);
            setError(null);
            const response = await getAll();
            setData(response);
        } catch (err: any) {
            setError(err.message || 'Failed to fetch data');
            console.error('Error fetching event handlers:', err);
        } finally {
            setLoading(false);
        }
    };

    const columns: GridColDef[] = [
        {field: 'name', headerName: 'Name', width: 250, },
        {field: 'type', headerName: 'Type'},
        {field: 'prefab', headerName: 'Prefab', width: 250},
    ];

    const paginationModel = { page: 0, pageSize: 10 };

    useEffect(() => {
        fetchData();
    }, []);

    return (
        <DashboardContainer title="Event Handlers" error={error}>
            <Stack spacing={2} direction="row">
                <Button variant="contained">Add</Button>
            </Stack>
            <br />
            <Paper>
                <DataGrid
                    rows={data}
                    columns={columns}
                    getRowId={x => x.name}
                    initialState={{ pagination: { paginationModel } }}
                    pageSizeOptions={[10, 20, 30, 40, 50, 100]}
                    checkboxSelection
                    sx={{ border: 0 }}
                />
            </Paper>
        </DashboardContainer>
    );
}

export default EventHandlerPage;