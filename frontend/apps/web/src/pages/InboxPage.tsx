import { useState } from 'react';
import { Button, Card, FormControlLabel, InputAdornment, MenuItem, Stack, Switch, TextField } from '@mui/material';
import AddOutlined from '@mui/icons-material/AddOutlined';
import SearchOutlined from '@mui/icons-material/SearchOutlined';
import InboxOutlined from '@mui/icons-material/InboxOutlined';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Can } from '@flowdesk/auth';
import { Permissions, PriorityLabels, StatusLabels, type ExpedientPriority, type ExpedientStatus } from '@flowdesk/shared-types';
import { EmptyState, PageHeader } from '@flowdesk/ui-components';
import { api } from '../api';
import { CreateExpedientDialog } from '../components/CreateExpedientDialog';
import { ExpedientTable } from '../components/ExpedientTable';
import { useDebounced } from '../hooks/useDebounced';

export function InboxPage() {
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [priority, setPriority] = useState('');
  const [assignedToMe, setAssignedToMe] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [creating, setCreating] = useState(false);
  const debouncedSearch = useDebounced(search, 300);

  const query = { search: debouncedSearch, status, priority, assignedToMe, page, pageSize };
  const { data, isFetching } = useQuery({
    queryKey: ['expedients', query],
    queryFn: () => api.expedients.search(query),
    placeholderData: keepPreviousData,
  });

  const resetPage = <T,>(setter: (v: T) => void) => (v: T) => {
    setter(v);
    setPage(1);
  };

  return (
    <>
      <PageHeader
        overline="Expedientes"
        title="Bandeja de trabajo"
        subtitle="Expedientes, filtros y prioridades. Los cambios de otros usuarios aparecen en tiempo real."
        actions={
          <Can permission={Permissions.ExpedientsCreate}>
            <Button variant="contained" startIcon={<AddOutlined />} onClick={() => setCreating(true)}>
              Nuevo expediente
            </Button>
          </Can>
        }
      />

      <Card>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} p={2} alignItems={{ md: 'center' }}>
          <TextField
            placeholder="Buscar por código, título o solicitante"
            value={search}
            onChange={(e) => resetPage(setSearch)(e.target.value)}
            size="small"
            sx={{ flex: 1, minWidth: 260 }}
            slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchOutlined /></InputAdornment> } }}
          />
          <TextField select size="small" label="Estado" value={status} onChange={(e) => resetPage(setStatus)(e.target.value)} sx={{ minWidth: 200 }}>
            <MenuItem value="">Todos</MenuItem>
            {(Object.keys(StatusLabels) as ExpedientStatus[]).map((s) => (
              <MenuItem key={s} value={s}>
                {StatusLabels[s]}
              </MenuItem>
            ))}
          </TextField>
          <TextField select size="small" label="Prioridad" value={priority} onChange={(e) => resetPage(setPriority)(e.target.value)} sx={{ minWidth: 150 }}>
            <MenuItem value="">Todas</MenuItem>
            {(Object.keys(PriorityLabels) as ExpedientPriority[]).map((p) => (
              <MenuItem key={p} value={p}>
                {PriorityLabels[p]}
              </MenuItem>
            ))}
          </TextField>
          <FormControlLabel
            control={<Switch checked={assignedToMe} onChange={(e) => resetPage(setAssignedToMe)(e.target.checked)} />}
            label="Asignados a mí"
          />
        </Stack>

        {data && data.totalCount === 0 && !isFetching ? (
          <EmptyState icon={<InboxOutlined />} title="No hay expedientes" description="Ajuste los filtros o cree un nuevo expediente." />
        ) : (
          <ExpedientTable
            data={data}
            loading={isFetching}
            page={page}
            pageSize={pageSize}
            onPageChange={setPage}
            onPageSizeChange={resetPage(setPageSize)}
          />
        )}
      </Card>

      {creating && <CreateExpedientDialog open onClose={() => setCreating(false)} />}
    </>
  );
}
