import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PlaceOrderForm } from './PlaceOrderForm';
import { api } from '../../api/client';
import type { LimitOrder } from '../../api/types';

vi.mock('../../api/client');

const mockOrder: LimitOrder = {
  id: 'abc-123',
  pair: 'USD/EUR',
  side: 'Buy',
  limitPrice: 0.92,
  quantity: 1000,
  status: 'Pending',
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
  vi.mocked(api.placeOrder).mockResolvedValue(mockOrder);
});

describe('PlaceOrderForm', () => {
  it('renders the form heading', () => {
    render(<PlaceOrderForm />, { wrapper });
    expect(screen.getByText(/PLACE ORDER/i)).toBeInTheDocument();
  });

  it('defaults to Buy side', () => {
    render(<PlaceOrderForm />, { wrapper });
    const buyBtn = screen.getByRole('button', { name: 'BUY' });
    expect(buyBtn).toHaveClass('bg-green-600');
  });

  it('switches to Sell side when Sell button clicked', async () => {
    const user = userEvent.setup();
    render(<PlaceOrderForm />, { wrapper });
    await user.click(screen.getByRole('button', { name: 'SELL' }));
    const sellBtn = screen.getByRole('button', { name: 'SELL' });
    expect(sellBtn).toHaveClass('bg-red-600');
  });

  it('shows validation errors for empty price and quantity', async () => {
    const user = userEvent.setup();
    render(<PlaceOrderForm />, { wrapper });
    await user.click(screen.getByRole('button', { name: /PLACE BUY LIMIT/i }));
    await waitFor(() => {
      expect(screen.getAllByText(/Required|Must be/i).length).toBeGreaterThanOrEqual(2);
    });
  });

  it('submits the form with valid data and calls placeOrder', async () => {
    const user = userEvent.setup();
    render(<PlaceOrderForm />, { wrapper });

    await user.type(screen.getByPlaceholderText(/0\.9200/), '0.92');
    await user.type(screen.getByPlaceholderText(/1000/), '500');
    await user.click(screen.getByRole('button', { name: /PLACE BUY LIMIT/i }));

    await waitFor(() => {
      expect(api.placeOrder).toHaveBeenCalled();
    });
  });

  it('shows success message after order placed', async () => {
    const user = userEvent.setup();
    render(<PlaceOrderForm />, { wrapper });

    await user.type(screen.getByPlaceholderText(/0\.9200/), '0.92');
    await user.type(screen.getByPlaceholderText(/1000/), '500');
    await user.click(screen.getByRole('button', { name: /PLACE BUY LIMIT/i }));

    expect(await screen.findByText(/Order placed/i)).toBeInTheDocument();
  });

  it('clears limitPrice and quantity fields after successful order', async () => {
    const user = userEvent.setup();
    render(<PlaceOrderForm />, { wrapper });

    const priceInput = screen.getByPlaceholderText(/0\.9200/);
    const qtyInput = screen.getByPlaceholderText(/1000/);

    await user.type(priceInput, '0.92');
    await user.type(qtyInput, '500');
    await user.click(screen.getByRole('button', { name: /PLACE BUY LIMIT/i }));

    await screen.findByText(/Order placed/i);

    expect((priceInput as HTMLInputElement).value).toBe('');
    expect((qtyInput as HTMLInputElement).value).toBe('');
  });

  it('shows error message when API call fails', async () => {
    vi.mocked(api.placeOrder).mockRejectedValue(new Error('Server error'));
    const user = userEvent.setup();
    render(<PlaceOrderForm />, { wrapper });

    await user.type(screen.getByPlaceholderText(/0\.9200/), '0.92');
    await user.type(screen.getByPlaceholderText(/1000/), '500');
    await user.click(screen.getByRole('button', { name: /PLACE BUY LIMIT/i }));

    expect(await screen.findByText(/Server error/i)).toBeInTheDocument();
  });
});
