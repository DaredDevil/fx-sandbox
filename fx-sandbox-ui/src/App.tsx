import { RatesTicker } from './features/rates/RatesTicker';
import { PlaceOrderForm } from './features/orders/PlaceOrderForm';
import { OrderBook } from './features/orders/OrderBook';
import { PositionsPanel } from './features/positions/PositionsPanel';

export default function App() {
  return (
    <div className="min-h-screen bg-gray-950 text-gray-100 font-mono">
      {/* Header */}
      <header className="border-b border-gray-800 px-6 py-3 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <span className="text-cyan-400 font-bold text-sm tracking-widest">FX SANDBOX</span>
          <span className="text-gray-600 text-xs">|</span>
          <span className="text-gray-500 text-xs">PAPER TRADING TERMINAL</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="w-2 h-2 rounded-full bg-green-400 animate-pulse" />
          <span className="text-green-400 text-xs">LIVE</span>
        </div>
      </header>

      {/* Main grid */}
      <main className="p-4 grid grid-cols-1 lg:grid-cols-3 gap-4 max-w-7xl mx-auto">
        {/* Column 1 — Rates */}
        <div className="space-y-4">
          <RatesTicker />
        </div>

        {/* Column 2 — Place Order */}
        <div className="space-y-4">
          <PlaceOrderForm />
        </div>

        {/* Column 3 — Orders + Positions */}
        <div className="space-y-4">
          <PositionsPanel />
          <OrderBook />
        </div>
      </main>
    </div>
  );
}
