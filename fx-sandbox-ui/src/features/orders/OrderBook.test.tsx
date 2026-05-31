import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { OrderBook } from './OrderBook';
import { api } from '../../api/client';
import type { LimitOrder } from '../../api/types';

vi.mock('../../api/client');

const pendingOrder: LimitOrder = {
  id: 'ord-1',
  pair: 'USD/EUR',
  side: 'Buy',
  limitPrice: 0.91,
  quantity: 1000,
  status: 'Pending',
  createdAt: '2024-01-01T00:00:00Z',
  filledAt: null,
};

const filledOrder: LimitOrder = {
  id: 'ord-2',
  pair: 'USD/GBP',
  side: 'Sell',
  limitPrice: 0.80,
  quantity: 500,
  status: 'Filled',
  createdAt: '2024-01-01T00:00:00Z',
  filledAt: '2024-01-01T00:01:00Z',
};

const cancelledOrder: LimitOrder = {
  id: 'ord-3',
  pair: 'USD/CHF',
  side: 'Buy',
  limitPrice: 0.88,
  quantity: 200,
  status: 'Cancelled',
  createdAt: '2024-01-01T00:00:00Z',
  filledAt: null,
};

function wrapper({ children }: { children: React.ReactNode }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

beforeEach(() => {
  vi.mocked(api.getOrders).mockResolvedValue([pendingOrder, filledOrder]);
  vi.mocked(api.cancelOrder).mockResolvedValue(undefined);
});

describe('OrderBook', () => {
  it('renders the section heading', () => {
    render(<OrderBook />, { wrapper });
    expect(screen.getByText(/ORDER BOOK/i)).toBeInTheDocument();
  });

  it('shows skeleton loading state before data arrives', () => {
    vi.mocked(api.getOrders).mockReturnValue(new Promise(() => {}));
    render(<OrderBook />, { wrapper });
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when no orders', async () => {
    vi.mocked(api.getOrders).mockResolvedValue([]);
    render(<OrderBook />, { wrapper });
    expect(await screen.findByText(/No orders yet/i)).toBeInTheDocument();
  });

  it('renders pair and side for each order', async () => {
    render(<OrderBook />, { wrapper });
    expect(await screen.findByText('USD/EUR')).toBeInTheDocument();
    expect(await screen.findByText('USD/GBP')).toBeInTheDocument();
  });

  it('shows PENDING badge in yellow', async () => {
    render(<OrderBook />, { wrapper });
    const badge = await screen.findByText('PENDING');
    expect(badge).toHaveClass('text-yellow-400');
  });

  it('shows FILLED badge in green', async () => {
    render(<OrderBook />, { wrapper });
    const badge = await screen.findByText('FILLED');
    expect(badge).toHaveClass('text-green-400');
  });

  it('shows CANCELLED badge in gray', async () => {
    vi.mocked(api.getOrders).mockResolvedValue([cancelledOrder]);
    render(<OrderBook />, { wrapper });
    const badge = await screen.findByText('CANCELLED');
    expect(badge).toHaveClass('text-gray-500');
  });

  it('shows cancel button only for pending orders', async () => {
    render(<OrderBook />, { wrapper });
    await screen.findByText('USD/EUR');
    const cancelButtons = screen.getAllByTitle('Cancel order');
    expect(cancelButtons).toHaveLength(1);
  });

  it('calls cancelOrder when cancel button clicked', async () => {
    const user = userEvent.setup();
    render(<OrderBook />, { wrapper });
    const cancelBtn = await screen.findByTitle('Cancel order');
    await user.click(cancelBtn);
    await waitFor(() => {
      expect(api.cancelOrder).toHaveBeenCalledWith('ord-1', expect.anything());
    });
  });

  it('formats limit price to 4 decimal places', async () => {
    render(<OrderBook />, { wrapper });
    expect(await screen.findByText('0.9100')).toBeInTheDocument();
  });

  it('shows error message when API fails', async () => {
    vi.mocked(api.getOrders).mockRejectedValue(new Error('Network error'));
    render(<OrderBook />, { wrapper });
    expect(await screen.findByText(/Failed to load orders/i)).toBeInTheDocument();
  });
});
