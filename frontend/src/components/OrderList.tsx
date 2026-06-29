import type { OrderSummary } from '../types';
import { StatusBadge } from './StatusBadge';

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
});

function timeAgo(iso: string): string {
  const seconds = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

export function OrderList({
  orders,
  highlightedId,
}: {
  orders: OrderSummary[];
  highlightedId?: string | null;
}) {
  return (
    <div className="overflow-hidden rounded-xl border border-slate-700/60 bg-slate-800/50 shadow-lg">
      <div className="border-b border-slate-700/60 px-5 py-4">
        <h2 className="text-base font-semibold text-slate-100">Recent Orders</h2>
        <p className="text-xs text-slate-400">Updates live as orders come in</p>
      </div>
      <div className="max-h-[28rem] overflow-y-auto">
        <table className="min-w-full divide-y divide-slate-700/60 text-sm">
          <thead className="sticky top-0 bg-slate-800/90 backdrop-blur">
            <tr className="text-left text-xs uppercase tracking-wide text-slate-400">
              <th className="px-5 py-3 font-medium">Order</th>
              <th className="px-5 py-3 font-medium">Customer</th>
              <th className="px-5 py-3 font-medium">Status</th>
              <th className="px-5 py-3 text-right font-medium">Total</th>
              <th className="px-5 py-3 text-right font-medium">Items</th>
              <th className="px-5 py-3 text-right font-medium">When</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-700/40">
            {orders.length === 0 && (
              <tr>
                <td colSpan={6} className="px-5 py-10 text-center text-slate-500">
                  No orders yet.
                </td>
              </tr>
            )}
            {orders.map((order) => (
              <tr
                key={order.id}
                className={`transition-colors ${
                  highlightedId === order.id
                    ? 'bg-blue-500/10'
                    : 'hover:bg-slate-700/30'
                }`}
              >
                <td className="px-5 py-3 font-mono text-xs text-slate-300">
                  {order.orderNumber}
                </td>
                <td className="px-5 py-3 text-slate-200">{order.customerName}</td>
                <td className="px-5 py-3">
                  <StatusBadge status={order.status} />
                </td>
                <td className="px-5 py-3 text-right tabular-nums text-slate-200">
                  {currency.format(order.totalAmount)}
                </td>
                <td className="px-5 py-3 text-right tabular-nums text-slate-400">
                  {order.itemCount}
                </td>
                <td className="px-5 py-3 text-right text-slate-500">
                  {timeAgo(order.createdAt)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
