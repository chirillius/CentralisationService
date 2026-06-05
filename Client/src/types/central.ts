export type StoreDto = {
  siteKey: string;
  siteName: string;
  serverBaseUrl: string;
  connectorId: string;
  cleaningDay: number;
  lastSyncUtc: string;
  isAvailable: boolean;
  cameraCount: number;
};

export type CameraDto = {
  key: string;
  name: string;
  siteKey: string;
  siteName: string;
  cameraId: number | null;
  sourceCameraKey: string;
  serverBaseUrl: string;
  lastSyncUtc: string;
  isAvailable: boolean;
};

export type MotionFrameDto = {
  cameraKey: string;
  cameraName: string;
  siteKey: string;
  siteName: string;
  relativePath: string;
  fileName: string;
  publicUrl: string;
  capturedAtUtc: string;
};

export type AuthResponseDto = {
  sessionToken: string;
  account: {
    id: string;
    login: string;
    displayName: string;
    roleKey: string;
    permissions: string[];
    accessExpiresAtUtc: string | null;
  };
  company: {
    id: string;
    key: string;
    name: string;
  };
};

export type PlatformAuthResponseDto = {
  platformSessionToken: string;
  admin: {
    login: string;
    displayName: string;
    roleKey: string;
  };
  expiresAtUtc: string;
};

export type CompanyAccessDto = {
  id: string;
  key: string;
  name: string;
  status: 'active' | 'suspended' | 'disabled' | 'archived';
  accessExpiresAtUtc: string | null;
  disabledAtUtc: string | null;
  disabledReason: string | null;
  updatedAtUtc: string;
};

export type CompanySiteDto = {
  companyKey: string;
  siteKey: string;
  siteName: string;
  serverBaseUrl: string;
  connectorId: string;
  cleaningDay: number;
  lastSyncUtc: string;
  isAvailable: boolean;
  cameras: Array<{
    cameraId: number | null;
    cameraKey: string;
    sourceCameraKey: string;
    cameraName: string;
    isAvailable: boolean;
  }>;
};

export type CompanyAccountDto = {
  accountId: string;
  grantId: string;
  login: string;
  displayName: string;
  roleKey: string;
  permissions: string[];
  accessExpiresAtUtc: string | null;
  isEnabled: boolean;
  createdAtUtc: string;
};

export type CompanyInvitationDto = {
  id: string;
  name: string;
  roleKey: string;
  permissions: string[];
  expiresAtUtc: string | null;
  usedAtUtc: string | null;
  usedByAccountId: string | null;
  revokedAtUtc: string | null;
  createdAtUtc: string;
  isActive: boolean;
};

export type CreateInvitationResponseDto = {
  invitation: {
    id: string;
    companyId: string;
    name: string;
    roleKey: string;
    permissions: string[];
    expiresAtUtc: string | null;
    createdAtUtc: string;
  };
  token: string;
  note: string;
};
