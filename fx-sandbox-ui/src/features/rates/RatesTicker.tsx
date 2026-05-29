import { useQuery } from '@tanstack/react-query';
import { useRef, useEffect, useState } from 'react';
import { api } from '../../api/client';
import type { Rate } from '../../api/types';

interface RateFlash {
  [pair: string]: 'up' | 'down' | null;
}

export function RatesTicker() {
  const { data: rates, isError } = useQuery({
    queryKey: ['rates'],
    queryFn: api.getRates,
    refetchInterval: 1000,
  });

  const prevRates = useRef<Record<string, number>>({});
  const [flashes, setFlashes] = useState<RateFlash>({});

  useEffect(() => {
    if (!rates) return;
    const newFlashes: RateFlash = {};
    for (const r of rates) {
      const prev = prevRates.current[r.pair];
      if (prev !== undefined && prev !== r.value) {
        newFlashes[r.pair] = r.value > prev ? 'up' : 'down';
      }
      prevRates.current[r.pair] = r.value;
    }
    if (Object.keys(newFlashes).length > 0) {
      setFlashes(newFlashes);
      setTimeout(() => setFlashes({}), 450);
    }
  }, [rates]);

  return (
    <div className="bg-gray-900 border border-gray-800 rounded-lg p-4">
      <h2 className="text-cyan-400 text-xs tracking-widest mb-4 font-bold">◈ LIVE RATES</h2>

      {isError && (
        <p className="text-red-400 text-xs">Cannot reach backend — is the API running on :5000?</p>
      )}

      {rates?.map((rate: Rate) => {
        const flash = flashes[rate.pair];
        const flashClass = flash === 'up' ? 'flash-green' : flash === 'down' ? 'flash-red' : '';
        const changeColor = flash === 'up' ? 'text-green-400' : flash === 'down' ? 'text-red-400' : 'text-gray-400';
        const arrow = flash === 'up' ? '▲' : flash === 'down' ? '▼' : '—';

        return (
          <div
            key={rate.pair}
            className={`flex items-center justify-between py-3 border-b border-gray-800 last:border-0 rounded px-1 ${flashClass}`}
          >
            <div>
              <p className="text-gray-300 text-sm font-semibold">{rate.pair}</p>
              <p className={`text-xs mt-0.5 ${changeColor}`}>{arrow}</p>
            </div>
            <p className={`text-2xl font-bold tabular-nums ${changeColor === 'text-gray-400' ? 'text-gray-100' : changeColor}`}>
              {rate.value.toFixed(4)}
            </p>
          </div>
        );
      })}

      {!rates && !isError && (
        <div className="space-y-4">
          {['USD/EUR', 'USD/GBP', 'USD/CHF'].map(p => (
            <div key={p} className="flex justify-between items-center animate-pulse">
              <div className="h-4 w-20 bg-gray-700 rounded" />
              <div className="h-6 w-24 bg-gray-700 rounded" />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
