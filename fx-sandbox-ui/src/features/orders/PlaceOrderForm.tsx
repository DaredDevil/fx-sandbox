import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';

const PAIRS = ['USD/EUR', 'USD/GBP', 'USD/CHF'] as const;

const schema = z.object({
  pair: z.enum(PAIRS),
  side: z.enum(['Buy', 'Sell']),
  limitPrice: z.number({ error: 'Required' }).positive('Must be > 0'),
  quantity: z.number({ error: 'Required' }).positive('Must be > 0'),
});
type FormData = z.infer<typeof schema>;

export function PlaceOrderForm() {
  const queryClient = useQueryClient();

  const { register, handleSubmit, watch, setValue, reset, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { pair: 'USD/EUR', side: 'Buy', limitPrice: undefined, quantity: undefined },
  });

  const side = watch('side');

  const mutation = useMutation({
    mutationFn: api.placeOrder,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['orders'] });
      reset({ pair: 'USD/EUR', side: 'Buy' });
    },
  });

  const onSubmit = (data: FormData) => mutation.mutate(data);

  const sideColor = side === 'Buy' ? 'text-green-400 border-green-500' : 'text-red-400 border-red-500';

  return (
    <div className="bg-gray-900 border border-gray-800 rounded-lg p-4">
      <h2 className="text-cyan-400 text-xs tracking-widest mb-4 font-bold">⊕ PLACE ORDER</h2>

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        {/* Pair */}
        <div>
          <label className="text-gray-400 text-xs block mb-1">PAIR</label>
          <select
            {...register('pair')}
            className="w-full bg-gray-800 border border-gray-700 text-gray-100 rounded px-3 py-2 text-sm focus:outline-none focus:border-cyan-500"
          >
            {PAIRS.map(p => <option key={p} value={p}>{p}</option>)}
          </select>
        </div>

        {/* Side toggle */}
        <div>
          <label className="text-gray-400 text-xs block mb-1">SIDE</label>
          <div className="flex rounded overflow-hidden border border-gray-700">
            {(['Buy', 'Sell'] as const).map(s => (
              <button
                key={s}
                type="button"
                onClick={() => setValue('side', s)}
                className={`flex-1 py-2 text-sm font-bold transition-colors ${
                  side === s
                    ? s === 'Buy' ? 'bg-green-600 text-white' : 'bg-red-600 text-white'
                    : 'bg-gray-800 text-gray-400 hover:bg-gray-700'
                }`}
              >
                {s.toUpperCase()}
              </button>
            ))}
          </div>
        </div>

        {/* Limit price */}
        <div>
          <label className="text-gray-400 text-xs block mb-1">LIMIT PRICE</label>
          <input
            type="number"
            step="0.0001"
            placeholder="e.g. 0.9200"
            {...register('limitPrice', { valueAsNumber: true })}
            className={`w-full bg-gray-800 border rounded px-3 py-2 text-sm text-gray-100 focus:outline-none tabular-nums ${
              errors.limitPrice ? 'border-red-500' : 'border-gray-700 focus:border-cyan-500'
            }`}
          />
          {errors.limitPrice && <p className="text-red-400 text-xs mt-1">{errors.limitPrice.message}</p>}
        </div>

        {/* Quantity */}
        <div>
          <label className="text-gray-400 text-xs block mb-1">QUANTITY (USD units)</label>
          <input
            type="number"
            step="0.01"
            placeholder="e.g. 1000"
            {...register('quantity', { valueAsNumber: true })}
            className={`w-full bg-gray-800 border rounded px-3 py-2 text-sm text-gray-100 focus:outline-none tabular-nums ${
              errors.quantity ? 'border-red-500' : 'border-gray-700 focus:border-cyan-500'
            }`}
          />
          {errors.quantity && <p className="text-red-400 text-xs mt-1">{errors.quantity.message}</p>}
        </div>

        {mutation.isError && (
          <p className="text-red-400 text-xs bg-red-900/20 border border-red-800 rounded p-2">
            {String(mutation.error)}
          </p>
        )}

        <button
          type="submit"
          disabled={mutation.isPending}
          className={`w-full py-2.5 rounded font-bold text-sm transition-colors ${sideColor} border ${
            mutation.isPending ? 'opacity-50 cursor-not-allowed' : 'hover:bg-opacity-20 hover:bg-white/5'
          }`}
        >
          {mutation.isPending ? 'PLACING...' : `PLACE ${side === 'Buy' ? 'BUY' : 'SELL'} LIMIT`}
        </button>

        {mutation.isSuccess && (
          <p className="text-green-400 text-xs text-center">✓ Order placed</p>
        )}
      </form>
    </div>
  );
}
