import { useEffect } from 'react';
import type { LowStockAlert } from '../types';

export interface Toast extends LowStockAlert {
  key: number;
}

export function LowStockToasts({
  toasts,
  onDismiss,
}: {
  toasts: Toast[];
  onDismiss: (key: number) => void;
}) {
  return (
    <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2">
      {toasts.map((toast) => (
        <ToastCard key={toast.key} toast={toast} onDismiss={onDismiss} />
      ))}
    </div>
  );
}

function ToastCard({
  toast,
  onDismiss,
}: {
  toast: Toast;
  onDismiss: (key: number) => void;
}) {
  useEffect(() => {
    const timer = setTimeout(() => onDismiss(toast.key), 6000);
    return () => clearTimeout(timer);
  }, [toast.key, onDismiss]);

  return (
    <div className="flex w-80 items-start gap-3 rounded-lg border border-amber-500/40 bg-amber-950/90 p-4 shadow-xl">
      <span className="text-xl">⚠️</span>
      <div className="flex-1">
        <p className="text-sm font-semibold text-amber-200">Low Stock Alert</p>
        <p className="text-sm text-amber-100/80">
          <span className="font-medium">{toast.productName}</span> is down to{' '}
          {toast.currentStock} units.
        </p>
      </div>
      <button
        onClick={() => onDismiss(toast.key)}
        className="text-amber-300/60 hover:text-amber-200"
        aria-label="Dismiss"
      >
        ✕
      </button>
    </div>
  );
}
