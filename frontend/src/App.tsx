import { useCallback, useEffect, useState } from 'react';
import { API_BASE_URL, api, tokenStore } from './api/client';
import { ConnectionIndicator } from './components/ConnectionIndicator';
import { Login } from './components/Login';
import { OrderList } from './components/OrderList';
import { PlaceOrderPanel } from './components/PlaceOrderPanel';
import { StatsCards } from './components/StatsCards';
import { LowStockToasts, type Toast } from './components/LowStockToast';
import { useSignalR } from './hooks/useSignalR';
import type {
  AuthResponse,
  DashboardStats,
  LowStockAlert,
  OrderStatusUpdate,
  OrderSummary,
} from './types';

const HUB_URL = `${API_BASE_URL}/hubs/orders`;

export default function App() {
  const [token, setToken] = useState<string | null>(() => tokenStore.get());
  const [user, setUser] = useState<{ fullName: string; email: string } | null>(null);

  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [recentOrders, setRecentOrders] = useState<OrderSummary[]>([]);
  const [highlightedId, setHighlightedId] = useState<string | null>(null);
  const [toasts, setToasts] = useState<Toast[]>([]);

  const { connection, isConnected } = useSignalR(HUB_URL, token);

  const dismissToast = useCallback((key: number) => {
    setToasts((prev) => prev.filter((t) => t.key !== key));
  }, []);

  function handleLoggedIn(auth: AuthResponse) {
    setUser({ fullName: auth.fullName, email: auth.email });
    setToken(auth.accessToken);
  }

  function handleLogout() {
    tokenStore.clear();
    setToken(null);
    setUser(null);
    setStats(null);
    setRecentOrders([]);
  }

  // Wire up all SignalR event listeners once a connection exists.
  useEffect(() => {
    if (!connection) return;

    connection.on('InitialStats', (incoming: DashboardStats) => {
      setStats(incoming);
      setRecentOrders(incoming.recentOrders ?? []);
    });

    connection.on('NewOrderPlaced', (order: OrderSummary) => {
      setRecentOrders((prev) =>
        [order, ...prev.filter((o) => o.id !== order.id)].slice(0, 20),
      );
      setHighlightedId(order.id);
      setTimeout(
        () => setHighlightedId((cur) => (cur === order.id ? null : cur)),
        2500,
      );
    });

    connection.on('OrderStatusChanged', (update: OrderStatusUpdate) => {
      setRecentOrders((prev) =>
        prev.map((o) =>
          o.id === update.orderId ? { ...o, status: update.newStatus } : o,
        ),
      );
      setHighlightedId(update.orderId);
      setTimeout(
        () => setHighlightedId((cur) => (cur === update.orderId ? null : cur)),
        2500,
      );
    });

    connection.on('StatsUpdated', (incoming: DashboardStats) => {
      setStats(incoming);
    });

    connection.on('LowStockAlert', (alert: LowStockAlert) => {
      setToasts((prev) => [...prev, { ...alert, key: Date.now() + prev.length }]);
    });

    return () => {
      connection.off('InitialStats');
      connection.off('NewOrderPlaced');
      connection.off('OrderStatusChanged');
      connection.off('StatsUpdated');
      connection.off('LowStockAlert');
    };
  }, [connection]);

  // REST fallback for the initial snapshot — covers the brief window before the hub's
  // InitialStats arrives, or a transient hub failure.
  useEffect(() => {
    if (!token) return;
    api
      .getStats()
      .then((s) => {
        setStats(s);
        setRecentOrders((prev) => (prev.length ? prev : s.recentOrders ?? []));
      })
      .catch(() => {
        /* hub will deliver stats; ignore */
      });
  }, [token]);

  if (!token) {
    return <Login onLoggedIn={handleLoggedIn} />;
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-50">Real-Time Order Tracking</h1>
          <p className="text-sm text-slate-400">
            Live push updates over SignalR — no polling.
          </p>
        </div>
        <div className="flex items-center gap-4">
          <ConnectionIndicator isConnected={isConnected} />
          {user && (
            <span className="text-sm text-slate-400">{user.fullName || user.email}</span>
          )}
          <button
            onClick={handleLogout}
            className="rounded-lg border border-slate-600 px-3 py-1.5 text-sm text-slate-300 transition hover:bg-slate-700/50"
          >
            Sign out
          </button>
        </div>
      </header>

      <main className="mt-8 space-y-6">
        <StatsCards stats={stats} />

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
          <div className="lg:col-span-2">
            <OrderList orders={recentOrders} highlightedId={highlightedId} />
          </div>
          <div>
            <PlaceOrderPanel />
          </div>
        </div>
      </main>

      <LowStockToasts toasts={toasts} onDismiss={dismissToast} />
    </div>
  );
}
