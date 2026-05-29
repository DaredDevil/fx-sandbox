export interface Rate {
  pair: string;
  value: number;
  updatedAt: string;
}

export interface LimitOrder {
  id: string;
  pair: string;
  side: 'Buy' | 'Sell';
  limitPrice: number;
  quantity: number;
  status: 'Pending' | 'Filled';
  createdAt: string;
  filledAt: string | null;
}

export interface Position {
  pair: string;
  side: 'Buy' | 'Sell';
  quantity: number;
  averageEntryPrice: number;
  currentRate: number;
  unrealisedPnl: number;
}

export interface Account {
  balance: number;
  currency: string;
}

export interface PlaceOrderPayload {
  pair: string;
  side: 'Buy' | 'Sell';
  limitPrice: number;
  quantity: number;
}
