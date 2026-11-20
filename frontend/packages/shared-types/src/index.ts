// Contratos de la API. Espejo de backend/FlowDesk.Shared/Contracts (mantener sincronizados).

export type ExpedientStatus =
  | 'Received'
  | 'InValidation'
  | 'PendingApproval'
  | 'CorrectionRequested'
  | 'Approved'
  | 'Rejected';

export type ExpedientPriority = 'Low' | 'Normal' | 'High' | 'Urgent';
export type DocumentStatus = 'Pending' | 'Validated' | 'Invalid';
export type DocumentSource = 'Web' | 'DesktopFile' | 'Scanner';

export const Permissions = {
  ExpedientsRead: 'expedients.read',
  ExpedientsCreate: 'expedients.create',
  ExpedientsAssign: 'expedients.assign',
  DocumentsUpload: 'documents.upload',
  WorkflowValidate: 'workflow.validate',
  WorkflowApprove: 'workflow.approve',
  AuditRead: 'audit.read',
  UsersManage: 'users.manage',
} as const;

export type Permission = (typeof Permissions)[keyof typeof Permissions];

export const StatusLabels: Record<ExpedientStatus, string> = {
  Received: 'Recibido',
  InValidation: 'En validación',
  PendingApproval: 'Pendiente de aprobación',
  CorrectionRequested: 'Corrección solicitada',
  Approved: 'Aprobado',
  Rejected: 'Rechazado',
};

export const PriorityLabels: Record<ExpedientPriority, string> = {
  Low: 'Baja',
  Normal: 'Normal',
  High: 'Alta',
  Urgent: 'Urgente',
};

export const DocumentStatusLabels: Record<DocumentStatus, string> = {
  Pending: 'Procesando',
  Validated: 'Validado',
  Invalid: 'Inválido',
};

export const SourceLabels: Record<DocumentSource, string> = {
  Web: 'Web',
  DesktopFile: 'Escritorio',
  Scanner: 'Escáner',
};

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  roles: string[];
  permissions: string[];
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: UserDto;
}

export interface RoleDto {
  name: string;
  description: string;
  permissions: string[];
}

export interface UserLookupDto {
  id: string;
  fullName: string;
  roles: string[];
}

export interface CreateUserRequest {
  email: string;
  fullName: string;
  password: string;
  roles: string[];
}

export interface UpdateUserRequest {
  roles: string[];
  isActive: boolean;
}

export interface ExpedientSummaryDto {
  id: string;
  code: string;
  title: string;
  applicantName: string;
  status: ExpedientStatus;
  priority: ExpedientPriority;
  assignedToId: string | null;
  assignedToName: string | null;
  documentCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface DocumentVersionDto {
  version: number;
  sizeBytes: number;
  sha256: string | null;
  uploadedByName: string;
  uploadedAt: string;
  source: DocumentSource;
}

export interface DocumentDto {
  id: string;
  expedientId: string;
  fileName: string;
  contentType: string;
  status: DocumentStatus;
  validationMessage: string | null;
  currentVersion: number;
  sizeBytes: number;
  source: DocumentSource;
  uploadedAt: string;
  versions: DocumentVersionDto[];
}

export interface HistoryEntryDto {
  fromStatus: ExpedientStatus | null;
  toStatus: ExpedientStatus;
  changedByName: string;
  comment: string | null;
  occurredAt: string;
}

export interface ApprovalDto {
  decision: 'Approved' | 'Rejected' | 'CorrectionRequested';
  decidedByName: string;
  comment: string | null;
  decidedAt: string;
}

export interface TransitionDto {
  code: string;
  name: string;
  fromStatus: ExpedientStatus;
  toStatus: ExpedientStatus;
  requiresComment: boolean;
}

export interface ExpedientDetailDto {
  id: string;
  code: string;
  title: string;
  applicantName: string;
  description: string | null;
  status: ExpedientStatus;
  priority: ExpedientPriority;
  createdById: string;
  createdByName: string;
  assignedToId: string | null;
  assignedToName: string | null;
  createdAt: string;
  updatedAt: string;
  concurrencyStamp: string;
  documents: DocumentDto[];
  history: HistoryEntryDto[];
  approvals: ApprovalDto[];
  availableTransitions: TransitionDto[];
}

export interface CreateExpedientRequest {
  title: string;
  applicantName: string;
  description: string | null;
  priority: ExpedientPriority;
  assignedToId: string | null;
}

export interface ExpedientQuery {
  status?: string;
  search?: string;
  assignedToMe?: boolean;
  priority?: string;
  page?: number;
  pageSize?: number;
}

export interface AuditEntryDto {
  id: string;
  occurredAt: string;
  userName: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  previousState: string | null;
  newState: string | null;
  details: string | null;
  correlationId: string | null;
}

export interface DashboardDto {
  openExpedients: number;
  assignedToMe: number;
  pendingApproval: number;
  approvedLast30Days: number;
  rejectedLast30Days: number;
  averageResolutionHours: number | null;
  byStatus: Record<ExpedientStatus, number>;
  recentActivity: AuditEntryDto[];
}

export interface NotificationDto {
  id: string;
  title: string;
  message: string;
  expedientId: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface RealtimeEvent {
  type:
    | 'expedient.created'
    | 'expedient.statusChanged'
    | 'expedient.assigned'
    | 'document.uploaded'
    | 'document.processed'
    | 'notification';
  expedientId: string | null;
  documentId: string | null;
  status: string | null;
  message: string;
  occurredAt: string;
  targetUserId: string | null;
}

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}
