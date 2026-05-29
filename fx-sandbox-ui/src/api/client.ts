import type { Account, LimitOrder, PlaceOrderPayload, Position, Rate } from './types';

const BASE = (import.meta as Record<string, unknown> & { env: Record<string, string> }).env
  .VITE_API_URL ?? 'http://localhost:5000';

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

export const api = {
  getRates: () => get<Rate[]>('/api/rates'),
  getOrders: () => get<LimitOrder[]>('/api/orders'),
  getPositions: () => get<Position[]>('/api/positions'),
  getAccount: () => get<Account>('/api/account'),
  placeOrder: (payload: PlaceOrderPayload) => post<LimitOrder>('/api/orders', payload),
};
