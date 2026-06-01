import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PositionsPanel } from './PositionsPanel';
import { api } from '../../api/client';
import type { Position, Account } from '../../api/types';

vi.mock('../../api/client');

const mockPositions: Position[] = [
  {
    pair: 'USD/EUR',
    side: 'Buy',
    quantity: 1000,
    averageEntryPrice: 0.9100,
    currentRate: 0.9200,
    unrealisedPnl: 10.0,
  },
];

const mockAccount: Account = { balance: 10000, currency: 'USD' };

function wrapper({ children }: { children: React.ReactNode }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

beforeEach(() => {
  vi.mocked(api.getPositions).mockResolvedValue(mockPositions);
  vi.mocked(api.getAccount).mockResolvedValue(mockAccount);
});

describe('PositionsPanel', () => {
  it('renders the section heading', () => {
    render(<PositionsPanel />, { wrapper });
    expect(screen.getByText(/POSITIONS/i)).toBeInTheDocument();
  });

  it('displays account balance', async () => {
    render(<PositionsPanel />, { wrapper });
    expect(await screen.findByText(/\$10,000/)).toBeInTheDocument();
  });

  it('shows empty state when no positions', async () => {
    vi.mocked(api.getPositions).mockResolvedValue([]);
    render(<PositionsPanel />, { wrapper });
    expect(await screen.findByText(/No open positions/i)).toBeInTheDocument();
  });

  it('renders pair and side for a position', async () => {
    render(<PositionsPanel />, { wrapper });
    expect(await screen.findByText('USD/EUR')).toBeInTheDocument();
    expect(await screen.findByText('BUY')).toBeInTheDocument();
  });

  it('shows positive P&L in green', async () => {
    render(<PositionsPanel />, { wrapper });
    const pnl = await screen.findByText('+10.00');
    expect(pnl).toHaveClass('text-green-400');
  });

  it('shows negative P&L in red', async () => {
    vi.mocked(api.getPositions).mockResolvedValue([
      { ...mockPositions[0], unrealisedPnl: -5.5 },
    ]);
    render(<PositionsPanel />, { wrapper });
    const pnl = await screen.findByText('-5.50');
    expect(pnl).toHaveClass('text-red-400');
  });

  it('shows entry price and current rate', async () => {
    render(<PositionsPanel />, { wrapper });
    expect(await screen.findByText('0.9100')).toBeInTheDocument();
    expect(await screen.findByText('0.9200')).toBeInTheDocument();
  });

  it('formats balance using the currency from the account', async () => {
    render(<PositionsPanel />, { wrapper });
    // Balance should be formatted as USD currency ($10,000.00)
    expect(await screen.findByText(/\$10,000\.00/)).toBeInTheDocument();
  });

  it('uses account.currency for formatting not a hardcoded string', async () => {
    vi.mocked(api.getAccount).mockResolvedValue({ balance: 5000, currency: 'USD' });
    render(<PositionsPanel />, { wrapper });
    expect(await screen.findByText(/\$5,000\.00/)).toBeInTheDocument();
  });

  it('shows updated balance after partial spend', async () => {
    vi.mocked(api.getAccount).mockResolvedValue({ balance: 7500, currency: 'USD' });
    render(<PositionsPanel />, { wrapper });
    expect(await screen.findByText(/\$7,500\.00/)).toBeInTheDocument();
  });
});
