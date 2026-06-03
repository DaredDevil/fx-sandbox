import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { LimitOrder } from '../../api/types';

export function OrderBook() {
  const queryClient = useQueryClient();

  const { data: orders, isError } = useQuery({
    queryKey: ['orders'],
    queryFn: api.getOrders,
    refetchInterval: 1000,
  });

  const cancelMutation = useMutation({
    mutationFn: api.cancelOrder,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['orders'] });
      void queryClient.invalidateQueries({ queryKey: ['account'] });
      void queryClient.invalidateQueries({ queryKey: ['positions'] });
    },
  });

  return (
    <div className="bg-gray-900 border border-gray-800 rounded-lg p-4">
      <h2 className="text-cyan-400 text-xs tracking-widest mb-4 font-bold">≡ ORDER BOOK</h2>

      {isError && (
        <p className="text-red-400 text-xs">Failed to load orders.</p>
      )}

      {!orders && !isError && (
        <div className="space-y-2">
          {[1, 2, 3].map(i => (
            <div key={i} className="h-8 bg-gray-800 rounded animate-pulse" />
          ))}
        </div>
      )}

      {orders && orders.length === 0 && (
        <p className="text-gray-500 text-xs text-center py-4">No orders yet</p>
      )}

      {orders && orders.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="text-gray-500 border-b border-gray-800">
                <th className="text-left pb-2 font-medium">PAIR</th>
                <th className="text-left pb-2 font-medium">SIDE</th>
                <th className="text-right pb-2 font-medium">PRICE</th>
                <th className="text-right pb-2 font-medium">QTY</th>
                <th className="text-right pb-2 font-medium">STATUS</th>
                <th className="pb-2" />
              </tr>
            </thead>
            <tbody>
              {orders.map((order: LimitOrder) => (
                <tr key={order.id} className="border-b border-gray-800/50 last:border-0">
                  <td className="py-2 text-gray-300">{order.pair}</td>
                  <td className={`py-2 font-bold ${order.side === 'Buy' ? 'text-green-400' : 'text-red-400'}`}>
                    {order.side.toUpperCase()}
                  </td>
                  <td className="py-2 text-right tabular-nums text-gray-100">
                    {order.limitPrice.toFixed(4)}
                  </td>
                  <td className="py-2 text-right tabular-nums text-gray-300">
                    {order.quantity.toLocaleString()}
                  </td>
                  <td className="py-2 text-right">
                    <span className={`px-1.5 py-0.5 rounded text-xs font-medium ${
                      order.status === 'Filled'
                        ? 'bg-green-900/40 text-green-400'
                        : order.status === 'Cancelled'
                        ? 'bg-gray-700/60 text-gray-500'
                        : 'bg-yellow-900/40 text-yellow-400'
                    }`}>
                      {order.status.toUpperCase()}
                    </span>
                  </td>
                  <td className="py-2 pl-2 text-right">
                    {order.status === 'Pending' && (
                      <button
                        onClick={() => cancelMutation.mutate(order.id)}
                        disabled={cancelMutation.isPending}
                        className="text-gray-600 hover:text-red-400 transition-colors text-xs px-1"
                        title="Cancel order"
                        aria-label="Cancel order"
                      >
                        ✕
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
