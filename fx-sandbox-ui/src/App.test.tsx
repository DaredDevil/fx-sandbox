import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import App from './App';
import { api } from './api/client';
import type { Account, Rate, LimitOrder, Position } from './api/types';

vi.mock('./api/client');

const mockAccount: Account = { balance: 10000, currency: 'USD' };
const mockRates: Rate[] = [
  { pair: 'USD/EUR', value: 0.9185, updatedAt: new Date().toISOString() },
  { pair: 'USD/GBP', value: 0.7890, updatedAt: new Date().toISOString() },
  { pair: 'USD/CHF', value: 0.8990, updatedAt: new Date().toISOString() },
];

function wrapper({ children }: { children: React.ReactNode }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(api.getAccount).mockResolvedValue(mockAccount);
  vi.mocked(api.getRates).mockResolvedValue(mockRates);
  vi.mocked(api.getOrders).mockResolvedValue([] as LimitOrder[]);
  vi.mocked(api.getPositions).mockResolvedValue([] as Position[]);
  vi.mocked(api.reset).mockResolvedValue(mockAccount);
  vi.spyOn(window, 'confirm').mockReturnValue(true);
});

describe('App — Reset button', () => {
  it('renders the RESET button', () => {
    render(<App />, { wrapper });
    expect(screen.getByRole('button', { name: /reset/i })).toBeInTheDocument();
  });

  it('calls api.reset when user confirms the dialog', async () => {
    const user = userEvent.setup();
    render(<App />, { wrapper });
    await user.click(screen.getByRole('button', { name: /reset/i }));
    await waitFor(() => expect(api.reset).toHaveBeenCalledOnce());
  });

  it('does not call api.reset when user cancels the dialog', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    const user = userEvent.setup();
    render(<App />, { wrapper });
    await user.click(screen.getByRole('button', { name: /reset/i }));
    expect(api.reset).not.toHaveBeenCalled();
  });

  it('shows RESETTING... while the mutation is in flight', async () => {
    vi.mocked(api.reset).mockReturnValue(new Promise(() => {})); // never resolves
    const user = userEvent.setup();
    render(<App />, { wrapper });
    await user.click(screen.getByRole('button', { name: /reset trading state/i }));
    await screen.findByText('RESETTING...');
  });

  it('button is disabled while resetting', async () => {
    vi.mocked(api.reset).mockReturnValue(new Promise(() => {}));
    const user = userEvent.setup();
    render(<App />, { wrapper });
    const button = screen.getByRole('button', { name: /reset trading state/i });
    await user.click(button);
    await waitFor(() => expect(button).toBeDisabled());
  });
});
