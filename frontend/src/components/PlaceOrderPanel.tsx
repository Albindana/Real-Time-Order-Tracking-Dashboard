import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Product } from '../types';

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
});

/**
 * A small panel to place orders from the UI so the live updates are easy to demo
 * without dropping into Swagger.
 */
export function PlaceOrderPanel() {
  const [products, setProducts] = useState<Product[]>([]);
  const [productId, setProductId] = useState('');
  const [quantity, setQuantity] = useState(1);
  const [status, setStatus] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    api
      .getProducts()
      .then((res) => {
        setProducts(res.items);
        if (res.items.length) setProductId(res.items[0].id);
      })
      .catch((err) => console.error('Failed to load products', err));
  }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!productId) return;
    setSubmitting(true);
    setStatus(null);
    try {
      await api.placeOrder([{ productId, quantity }]);
      setStatus('Order placed ✓');
    } catch (err) {
      setStatus(err instanceof Error ? err.message : 'Failed to place order');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="rounded-xl border border-slate-700/60 bg-slate-800/50 p-5 shadow-lg">
      <h2 className="text-base font-semibold text-slate-100">Place an Order</h2>
      <p className="text-xs text-slate-400">Watch the dashboard update in real time.</p>

      <form onSubmit={submit} className="mt-4 space-y-3">
        <select
          value={productId}
          onChange={(e) => setProductId(e.target.value)}
          className="w-full rounded-lg border border-slate-600 bg-slate-900 px-3 py-2 text-sm text-slate-100 focus:border-blue-500 focus:outline-none"
        >
          {products.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name} — {currency.format(p.price)} ({p.stockQuantity} in stock)
            </option>
          ))}
        </select>

        <div className="flex items-center gap-3">
          <label className="text-sm text-slate-300">Qty</label>
          <input
            type="number"
            min={1}
            value={quantity}
            onChange={(e) => setQuantity(Math.max(1, Number(e.target.value)))}
            className="w-20 rounded-lg border border-slate-600 bg-slate-900 px-3 py-2 text-sm text-slate-100 focus:border-blue-500 focus:outline-none"
          />
          <button
            type="submit"
            disabled={submitting || !productId}
            className="ml-auto rounded-lg bg-green-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-green-500 disabled:opacity-50"
          >
            {submitting ? 'Placing…' : 'Place Order'}
          </button>
        </div>

        {status && <p className="text-sm text-slate-300">{status}</p>}
      </form>
    </div>
  );
}
