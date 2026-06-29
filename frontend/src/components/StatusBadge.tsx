import type { OrderStatus } from '../types';

const STYLES: Record<OrderStatus, string> = {
  Pending: 'bg-yellow-500/15 text-yellow-300 ring-yellow-500/30',
  Processing: 'bg-blue-500/15 text-blue-300 ring-blue-500/30',
  Shipped: 'bg-purple-500/15 text-purple-300 ring-purple-500/30',
  Delivered: 'bg-green-500/15 text-green-300 ring-green-500/30',
  Cancelled: 'bg-red-500/15 text-red-300 ring-red-500/30',
};

export function StatusBadge({ status }: { status: OrderStatus }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ring-inset ${STYLES[status]}`}
    >
      {status}
    </span>
  );
}
