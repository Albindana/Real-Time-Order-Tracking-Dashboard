import type {
  AuthResponse,
  DashboardStats,
  OrderSummary,
  PagedResult,
  Product,
} from '../types';

export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5011';

const TOKEN_KEY = 'rtod.accessToken';

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (token: string) => localStorage.setItem(TOKEN_KEY, token),
  clear: () => localStorage.removeItem(TOKEN_KEY),
};

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = tokenStore.get();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  };
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${API_BASE_URL}${path}`, { ...options, headers });

  if (!res.ok) {
    let message = `Request failed (${res.status})`;
    try {
      const body = await res.json();
      message = body?.title ?? message;
    } catch {
      /* non-JSON error body */
    }
    throw new Error(message);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const api = {
  login: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  register: (
    email: string,
    password: string,
    firstName: string,
    lastName: string,
  ) =>
    request<AuthResponse>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password, firstName, lastName }),
    }),

  getStats: () => request<DashboardStats>('/api/dashboard/stats'),

  getRecentOrders: () => request<OrderSummary[]>('/api/dashboard/recent'),

  getProducts: () =>
    request<PagedResult<Product>>('/api/products?pageSize=100'),

  placeOrder: (items: { productId: string; quantity: number }[]) =>
    request<unknown>('/api/orders', {
      method: 'POST',
      body: JSON.stringify({ items }),
    }),

  updateOrderStatus: (orderId: string, status: string) =>
    request<unknown>(`/api/orders/${orderId}/status`, {
      method: 'PUT',
      body: JSON.stringify({ status }),
    }),
};
