import { fetchApi } from './users';

export async function getRoles(): Promise<{ data: string[]; errors: string[] }> {
  return fetchApi<string[]>('/api/v1/Roles/roles') as unknown as { data: string[]; errors: string[] };
}
