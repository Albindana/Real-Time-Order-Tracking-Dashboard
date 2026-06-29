import { useState } from 'react';
import { api, tokenStore } from '../api/client';
import type { AuthResponse } from '../types';

export function Login({ onLoggedIn }: { onLoggedIn: (auth: AuthResponse) => void }) {
  const [email, setEmail] = useState('admin@dashboard.com');
  const [password, setPassword] = useState('Admin123!');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const auth = await api.login(email, password);
      tokenStore.set(auth.accessToken);
      onLoggedIn(auth);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex min-h-full items-center justify-center p-4">
      <div className="w-full max-w-sm rounded-2xl border border-slate-700/60 bg-slate-800/60 p-8 shadow-2xl">
        <h1 className="text-xl font-semibold text-slate-50">Order Dashboard</h1>
        <p className="mt-1 text-sm text-slate-400">Sign in to view live order activity.</p>

        <form onSubmit={handleSubmit} className="mt-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-300">Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-600 bg-slate-900 px-3 py-2 text-slate-100 focus:border-blue-500 focus:outline-none"
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Password</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-600 bg-slate-900 px-3 py-2 text-slate-100 focus:border-blue-500 focus:outline-none"
              required
            />
          </div>

          {error && <p className="text-sm text-red-400">{error}</p>}

          <button
            type="submit"
            disabled={loading}
            className="w-full rounded-lg bg-blue-600 px-4 py-2 font-medium text-white transition hover:bg-blue-500 disabled:opacity-50"
          >
            {loading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>

        <div className="mt-6 rounded-lg bg-slate-900/60 p-3 text-xs text-slate-400">
          <p className="font-medium text-slate-300">Seeded accounts</p>
          <p>admin@dashboard.com / Admin123!</p>
          <p>customer@dashboard.com / Customer123!</p>
        </div>
      </div>
    </div>
  );
}
