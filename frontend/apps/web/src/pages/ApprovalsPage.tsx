import { useState } from 'react';
import { Card } from '@mui/material';
import TaskAltOutlined from '@mui/icons-material/TaskAltOutlined';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { EmptyState, PageHeader } from '@flowdesk/ui-components';
import { api } from '../api';
import { ExpedientTable } from '../components/ExpedientTable';

export function ApprovalsPage() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const query = { status: 'PendingApproval', page, pageSize };
  const { data, isFetching } = useQuery({
    queryKey: ['expedients', query],
    queryFn: () => api.expedients.search(query),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <PageHeader
        overline="Workflow"
        title="Aprobaciones"
        subtitle="Expedientes validados que esperan una decisión: aprobar, rechazar o devolver para corrección."
      />
      <Card>
        {data && data.totalCount === 0 ? (
          <EmptyState icon={<TaskAltOutlined />} title="Todo al día" description="No hay expedientes pendientes de aprobación." />
        ) : (
          <ExpedientTable data={data} loading={isFetching} page={page} pageSize={pageSize} onPageChange={setPage} onPageSizeChange={setPageSize} />
        )}
      </Card>
    </>
  );
}
