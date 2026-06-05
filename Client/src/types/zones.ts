export type ZonePointDto = {
  x: number;
  y: number;
};

export type ZoneBoundsDto = {
  x: number;
  y: number;
  width: number;
  height: number;
};

export type ZoneRecordDto = {
  id: string;
  siteKey: string;
  cameraKey: string;
  zoneTypeKey: string;
  zoneName: string;
  customName?: string | null;
  displayName: string;
  points: ZonePointDto[];
  bounds: ZoneBoundsDto;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type ZoneNameCatalogDto = {
  names: string[];
  allowCustom: boolean;
  customOptionLabel: string;
};

export type UpsertZoneRequestDto = {
  siteKey: string;
  cameraKey: string;
  zoneTypeKey: string;
  zoneName: string;
  customName?: string | null;
  points: ZonePointDto[];
};
