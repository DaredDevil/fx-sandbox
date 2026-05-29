import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RatesTicker } from './RatesTicker';
import { api } from '../../api/client';
import type { Rate } from '../../api/types';

vi.mock('../../api/client');

const mockRates: Rate[] = [
  { pair: 'USD/EUR', value: 0.9185, updatedAt: '2024-01-01T00:00:00Z' },
  { pair: 'USD/GBP', value: 0.7890, updatedAt: '2024-01-01T00:00:00Z' },
  { pair: 'USD/CHF', value: 0.8990, updatedAt: '2024-01-01T00:00:00Z' },
];

function wrapper({ children }: { children: React.ReactNode }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

beforeEach(() => {
  vi.mocked(api.getRates).mockResolvedValue(mockRates);
});

describe('RatesTicker', () => {
  it('renders the section heading', () => {
    render(<RatesTicker />, { wrapper });
    expect(screen.getByText(/LIVE RATES/i)).toBeInTheDocument();
  });

  it('shows skeleton loading state before data arrives', () => {
    vi.mocked(api.getRates).mockReturnValue(new Promise(() => {}));
    render(<RatesTicker />, { wrapper });
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('displays all three rate pairs after load', async () => {
    render(<RatesTicker />, { wrapper });
    expect(await screen.findByText('USD/EUR')).toBeInTheDocument();
    expect(await screen.findByText('USD/GBP')).toBeInTheDocument();
    expect(await screen.findByText('USD/CHF')).toBeInTheDocument();
  });

  it('formats rate values to 4 decimal places', async () => {
    render(<RatesTicker />, { wrapper });
    expect(await screen.findByText('0.9185')).toBeInTheDocument();
    expect(await screen.findByText('0.7890')).toBeInTheDocument();
  });

  it('shows error message when API fails', async () => {
    vi.mocked(api.getRates).mockRejectedValue(new Error('Network error'));
    render(<RatesTicker />, { wrapper });
    expect(await screen.findByText(/Cannot reach backend/i)).toBeInTheDocument();
  });
});
