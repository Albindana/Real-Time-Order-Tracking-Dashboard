export type OrderStatus =
  | 'Pending'
  | 'Processing'
  | 'Shipped'
  | 'Delivered'
  | 'Cancelled';

export interface OrderSummary {
  id: string;
  orderNumber: string;
  customerName: string;
  customerEmail: string;
  status: OrderStatus;
  totalAmount: number;
  itemCount: number;
  createdAt: string;
}

export interface OrderStatusUpdate {
  orderId: string;
  orderNumber: string;
  oldStatus: OrderStatus;
  newStatus: OrderStatus;
  updatedAt: string;
}

export interface DashboardStats {
  totalOrdersToday: number;
  revenueToday: number;
  pendingOrders: number;
  activeConnections: number;
  recentOrders: OrderSummary[];
}

export interface LowStockAlert {
  productId: string;
  productName: string;
  currentStock: number;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  email: string;
  fullName: string;
  roles: string[];
}

export interface Product {
  id: string;
  name: string;
  price: number;
  stockQuantity: number;
  category: string;
  isActive: boolean;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
