import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';

export function useSignalR(hubUrl: string, token: string | null) {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    if (!token) {
      setConnection(null);
      setIsConnected(false);
      return;
    }

    const conn = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connectionRef.current = conn;
    setConnection(conn);

    conn.onreconnected(() => setIsConnected(true));
    conn.onreconnecting(() => setIsConnected(false));
    conn.onclose(() => setIsConnected(false));

    conn
      .start()
      .then(() => setIsConnected(true))
      .catch((err) => console.error('SignalR connection failed:', err));

    return () => {
      conn.stop();
      connectionRef.current = null;
    };
  }, [hubUrl, token]);

  return { connection, isConnected };
}
