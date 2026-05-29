import { useQuery } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { Position } from '../../api/types';

export function PositionsPanel() {
  const { data: positions, isError } = useQuery({
    queryKey: ['positions'],
    queryFn: api.getPositions,
    refetchInterval: 1000,
  });

  const { data: account } = useQuery({
    queryKey: ['account'],
    queryFn: api.getAccount,
    refetchInterval: 5000,
  });

  return (
    <div className="bg-gray-900 border border-gray-800 rounded-lg p-4">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-cyan-400 text-xs tracking-widest font-bold">◎ POSITIONS</h2>
        {account && (
          <span className="text-gray-400 text-xs">
            BAL: <span className="text-gray-100 font-bold tabular-nums">
              {account.balance.toLocaleString('en-US', { style: 'currency', currency: account.currency })}
            </span>
          </span>
        )}
      </div>

      {isError && (
        <p className="text-red-400 text-xs">Failed to load positions.</p>
      )}

      {!positions && !isError && (
        <div className="space-y-3">
          {[1, 2].map(i => (
            <div key={i} className="h-16 bg-gray-800 rounded animate-pulse" />
          ))}
        </div>
      )}

      {positions && positions.length === 0 && (
        <p className="text-gray-500 text-xs text-center py-4">No open positions</p>
      )}

      {positions && positions.length > 0 && (
        <div className="space-y-2">
          {positions.map((pos: Position) => {
            const pnlPositive = pos.unrealisedPnl >= 0;
            const pnlColor = pnlPositive ? 'text-green-400' : 'text-red-400';
            const pnlSign = pnlPositive ? '+' : '';

            return (
              <div
                key={`${pos.pair}-${pos.side}`}
                className="border border-gray-800 rounded p-3 bg-gray-800/40"
              >
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <span className="text-gray-200 text-sm font-semibold">{pos.pair}</span>
                    <span className={`text-xs font-bold px-1.5 py-0.5 rounded ${
                      pos.side === 'Buy'
                        ? 'bg-green-900/50 text-green-400'
                        : 'bg-red-900/50 text-red-400'
                    }`}>
                      {pos.side.toUpperCase()}
                    </span>
                  </div>
                  <span className={`text-sm font-bold tabular-nums ${pnlColor}`}>
                    {pnlSign}{pos.unrealisedPnl.toFixed(2)}
                  </span>
                </div>
                <div className="grid grid-cols-3 gap-2 text-xs">
                  <div>
                    <p className="text-gray-500">QTY</p>
                    <p className="text-gray-300 tabular-nums">{pos.quantity.toLocaleString()}</p>
                  </div>
                  <div>
                    <p className="text-gray-500">ENTRY</p>
                    <p className="text-gray-300 tabular-nums">{pos.averageEntryPrice.toFixed(4)}</p>
                  </div>
                  <div>
                    <p className="text-gray-500">CURRENT</p>
                    <p className={`tabular-nums ${pnlColor}`}>{pos.currentRate.toFixed(4)}</p>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
