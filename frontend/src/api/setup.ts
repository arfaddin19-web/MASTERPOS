import { apiClient } from './client';
import type { SetupCompanyRequest, SetupStatusResponse } from './types';

export async function getSetupStatus(): Promise<SetupStatusResponse> {
  const { data } = await apiClient.get<SetupStatusResponse>('/setup/status');
  return data;
}

export async function completeSetup(request: SetupCompanyRequest) {
  const { data } = await apiClient.post('/setup', request);
  return data;
}
