import type { Account, LimitOrder, PlaceOrderPayload, Position, Rate } from './types';

// Empty string = relative URLs (works when API and UI share the same origin in production).
// Set VITE_API_URL=http://localhost:5000 in .env.local for local dev against a separate API process.
const BASE = import.meta.env.VITE_API_URL ?? '';

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`);
  if (!res.ok) throw new Error(`GET ${path} failed: ${res.status}`);
  return res.json() as Promise<T>;
}

async function post<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({})) as Record<string, unknown>;
    throw new Error(JSON.stringify(err));
  }
  return res.json() as Promise<T>;
}

async function del(path: string): Promise<void> {
  const res = await fetch(`${BASE}${path}`, { method: 'DELETE' });
  if (!res.ok && res.status !== 404) throw new Error(`DELETE ${path} failed: ${res.status}`);
}

export const api = {
  getRates: () => get<Rate[]>('/api/rates'),
  getOrders: () => get<LimitOrder[]>('/api/orders'),
  getPositions: () => get<Position[]>('/api/positions'),
  getAccount: () => get<Account>('/api/account'),
  placeOrder: (payload: PlaceOrderPayload) => post<LimitOrder>('/api/orders', payload),
  cancelOrder: (id: string) => del(`/api/orders/${id}`),
  reset: () => post<Account>('/api/reset', {}),
};
