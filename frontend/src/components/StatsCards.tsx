import type { DashboardStats } from '../types';

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
});

interface CardProps {
  label: string;
  value: string;
  accent: string;
  icon: string;
}

function Card({ label, value, accent, icon }: CardProps) {
  return (
    <div className="rounded-xl border border-slate-700/60 bg-slate-800/50 p-5 shadow-lg">
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium text-slate-400">{label}</p>
        <span className={`text-lg ${accent}`}>{icon}</span>
      </div>
      <p className="mt-2 text-3xl font-semibold tabular-nums text-slate-50">{value}</p>
    </div>
  );
}

export function StatsCards({ stats }: { stats: DashboardStats | null }) {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <Card
        label="Total Orders Today"
        value={stats ? stats.totalOrdersToday.toString() : '—'}
        accent="text-blue-400"
        icon="📦"
      />
      <Card
        label="Revenue Today"
        value={stats ? currency.format(stats.revenueToday) : '—'}
        accent="text-green-400"
        icon="💰"
      />
      <Card
        label="Pending Orders"
        value={stats ? stats.pendingOrders.toString() : '—'}
        accent="text-yellow-400"
        icon="⏳"
      />
      <Card
        label="Active Connections"
        value={stats ? stats.activeConnections.toString() : '—'}
        accent="text-purple-400"
        icon="🔌"
      />
    </div>
  );
}
