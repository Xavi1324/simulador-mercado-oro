import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import type {
  NuevoPrecioPayload,
  NuevaMetricaPayload,
  ResultadoTickPayload,
  EstadoInicialPayload,
} from '@/types/simulador';

const BACKEND_URL = process.env.NEXT_PUBLIC_BACKEND_URL ?? 'http://localhost:5000';

export function useSimuladorHub() {
  const hubRef = useRef<signalR.HubConnection | null>(null);
  const [conectado, setConectado] = useState(false);
  const [modoFuente, setModoFuente] = useState<'Swissquote' | 'API' | 'CSV' | null>(null);
  const [estadoInicial, setEstadoInicial] = useState<EstadoInicialPayload | null>(null);

  const onNuevoPrecioRef = useRef<((data: NuevoPrecioPayload) => void) | null>(null);
  const onNuevaMetricaRef = useRef<((data: NuevaMetricaPayload) => void) | null>(null);
  const onResultadoTickRef = useRef<((data: ResultadoTickPayload) => void) | null>(null);

  useEffect(() => {
    const hub = new signalR.HubConnectionBuilder()
      .withUrl(`${BACKEND_URL}/hubs/simulador`)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    hub.on('NuevoPrecio', (data: NuevoPrecioPayload) => {
      setModoFuente(data.modo);          // sincronizar badge con la fuente real del tick
      onNuevoPrecioRef.current?.(data);
    });
    hub.on('NuevaMetrica',      (data: NuevaMetricaPayload)   => onNuevaMetricaRef.current?.(data));
    hub.on('ResultadoTick',     (data: ResultadoTickPayload)  => onResultadoTickRef.current?.(data));
    hub.on('ModoFuenteChanged', (modo: 'Swissquote' | 'API' | 'CSV') => setModoFuente(modo));
    hub.on('EstadoInicial',     (data: EstadoInicialPayload)  => setEstadoInicial(data));

    hub.onreconnecting(() => setConectado(false));
    hub.onreconnected(()  => setConectado(true));
    hub.onclose(()        => setConectado(false));

    hub.start()
      .then(() => setConectado(true))
      .catch((err: unknown) => {
        if (process.env.NODE_ENV === 'development') {
          console.warn('SignalR: no se pudo conectar al backend:', err);
        }
      });

    hubRef.current = hub;
    return () => { hub.stop(); };
  }, []);

  const iniciar = useCallback((nucleos: number, intervaloSegundos: number) => {
    if (!hubRef.current || !conectado) return;
    hubRef.current.invoke('IniciarSimulacion', nucleos, intervaloSegundos);
  }, [conectado]);

  const pausar = useCallback(() => {
    if (!hubRef.current || !conectado) return;
    hubRef.current.invoke('PausarSimulacion');
  }, [conectado]);

  const configurar = useCallback((nucleos: number, intervaloSegundos: number) => {
    if (!hubRef.current || !conectado) return;
    hubRef.current.invoke('Configurar', nucleos, intervaloSegundos);
  }, [conectado]);

  return {
    conectado,
    modoFuente,
    estadoInicial,
    iniciar,
    pausar,
    configurar,
    onNuevoPrecioRef,
    onNuevaMetricaRef,
    onResultadoTickRef,
  };
}
