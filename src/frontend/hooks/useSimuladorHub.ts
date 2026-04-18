import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import type {
  NuevoPrecioPayload,
  NuevaMetricaPayload,
  ResultadoTickPayload,
  EstadoInicialPayload,
  ApuestaDemo,
  PrediccionesCalculadasPayload,
  EstrategiaSeleccionadaPayload,
  PortafolioActualizadoPayload,
} from '@/types/simulador';

const BACKEND_URL = process.env.NEXT_PUBLIC_BACKEND_URL ?? 'http://localhost:5000';

export function useSimuladorHub() {
  const hubRef = useRef<signalR.HubConnection | null>(null);

  // ── Estado: simulación de fondo ───────────────────────────────────────────
  const [conectado, setConectado]         = useState(false);
  const [modoFuente, setModoFuente]       = useState<'Swissquote' | 'API' | 'CSV' | null>(null);
  const [estadoInicial, setEstadoInicial] = useState<EstadoInicialPayload | null>(null);
  const [precioActual, setPrecioActual]   = useState<number>(0);

  // ── Estado: demo de descomposición especulativa ───────────────────────────
  const [isCalculando, setIsCalculando]   = useState(false);
  const [modoDemo, setModoDemo]           = useState<'Secuencial' | 'Paralelo'>('Paralelo');
  const [tiempoMsDemo, setTiempoMsDemo]   = useState<number | null>(null);
  const [predicciones, setPredicciones]   = useState<ApuestaDemo[] | null>(null);
  const [estrategiaSeleccionada, setEstrategiaSeleccionada] = useState<ApuestaDemo | null>(null);
  const [balanceDemo, setBalanceDemo]     = useState(0);
  const [logsDemo, setLogsDemo]           = useState<string[]>([]);
  const [segsRestantes, setSegsRestantes] = useState<number | null>(null);
  const countdownRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Callbacks de alta frecuencia — via refs para evitar re-renders
  const onNuevoPrecioRef   = useRef<((data: NuevoPrecioPayload) => void) | null>(null);
  const onNuevaMetricaRef  = useRef<((data: NuevaMetricaPayload) => void) | null>(null);
  const onResultadoTickRef = useRef<((data: ResultadoTickPayload) => void) | null>(null);

  // ── Conexión SignalR ───────────────────────────────────────────────────────
  useEffect(() => {
    const hub = new signalR.HubConnectionBuilder()
      .withUrl(`${BACKEND_URL}/hubs/simulador`)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // Simulación de fondo
    hub.on('NuevoPrecio', (data: NuevoPrecioPayload) => {
      setModoFuente(data.modo);
      setPrecioActual(data.precio);
      onNuevoPrecioRef.current?.(data);
    });

    hub.on('NuevaMetrica', (data: NuevaMetricaPayload) => {
      onNuevaMetricaRef.current?.(data);
    });

    hub.on('ResultadoTick', (data: ResultadoTickPayload) => {
      onResultadoTickRef.current?.(data);
    });

    hub.on('ModoFuenteChanged', (modo: 'Swissquote' | 'API' | 'CSV') => {
      setModoFuente(modo);
    });

    hub.on('EstadoInicial', (data: EstadoInicialPayload) => {
      setEstadoInicial(data);
      setBalanceDemo(data.balanceInicialDemo);
    });

    // ── Demo: Descomposición Especulativa ────────────────────────────────────
    hub.on('CalculoIniciado', (data: { modo: string; timestamp: string }) => {
      const modo = data.modo === 'Secuencial' ? 'Secuencial' : 'Paralelo';
      setIsCalculando(true);
      setPredicciones(null);
      setEstrategiaSeleccionada(null);
      setModoDemo(modo);
    });

    hub.on('PrediccionesCalculadas', (data: PrediccionesCalculadasPayload) => {
      setIsCalculando(false);
      setTiempoMsDemo(data.tiempoMs);
      setPredicciones(data.estrategias);
    });

    hub.on('EstrategiaSeleccionada', (data: EstrategiaSeleccionadaPayload) => {
      setEstrategiaSeleccionada(data.seleccionada);

      if (countdownRef.current) clearInterval(countdownRef.current);
      setSegsRestantes(60);
      countdownRef.current = setInterval(() => {
        setSegsRestantes(prev => {
          if (prev === null || prev <= 1) {
            clearInterval(countdownRef.current!);
            countdownRef.current = null;
            return null;
          }
          return prev - 1;
        });
      }, 1_000);
    });

    hub.on('PortafolioActualizado', (data: PortafolioActualizadoPayload) => {
      setBalanceDemo(data.balance);
      if (data.ultimoEvento) {
        setLogsDemo(prev => [...prev.slice(-49), data.ultimoEvento!]);
      }
      if (countdownRef.current) {
        clearInterval(countdownRef.current);
        countdownRef.current = null;
      }
      setSegsRestantes(null);
    });

    hub.onreconnecting(() => setConectado(false));
    hub.onreconnected(()  => setConectado(true));
    hub.onclose(()        => setConectado(false));

    hub.start()
      .then(() => setConectado(true))
      .catch((err: unknown) => {
        console.error('SignalR error al conectar:', err);
      });

    hubRef.current = hub;
    return () => {
      hub.stop();
      if (countdownRef.current) clearInterval(countdownRef.current);
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ── Acciones ──────────────────────────────────────────────────────────────
  const iniciar = useCallback((nucleos: number, intervaloSegundos: number, modo: 'Secuencial' | 'Paralelo') => {
    if (!hubRef.current || !conectado) return;
    hubRef.current.invoke('IniciarSimulacion', nucleos, intervaloSegundos, modo);
  }, [conectado]);

  const pausar = useCallback(() => {
    if (!hubRef.current || !conectado) return;
    hubRef.current.invoke('PausarSimulacion');
  }, [conectado]);

  const configurar = useCallback((nucleos: number, intervaloSegundos: number) => {
    if (!hubRef.current || !conectado) return;
    hubRef.current.invoke('Configurar', nucleos, intervaloSegundos);
  }, [conectado]);

  const cambiarFuente = useCallback((fuente: 'Swissquote' | 'CSV') => {
    if (!hubRef.current || !conectado) return;
    hubRef.current.invoke('CambiarFuente', fuente);
  }, [conectado]);

  return {
    conectado,
    modoFuente,
    estadoInicial,
    precioActual,
    iniciar,
    pausar,
    configurar,
    onNuevoPrecioRef,
    onNuevaMetricaRef,
    onResultadoTickRef,
    isCalculando,
    modoDemo,
    tiempoMsDemo,
    predicciones,
    estrategiaSeleccionada,
    balanceDemo,
    logsDemo,
    cambiarFuente,
    segsRestantes,
  };
}
