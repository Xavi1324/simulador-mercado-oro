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
const MONTO_APUESTA = 100; // dólares por apuesta

export function useSimuladorHub() {
  const hubRef = useRef<signalR.HubConnection | null>(null);

  // ── Estado: simulación de fondo ───────────────────────────────────────────
  const [conectado, setConectado]       = useState(false);
  const [modoFuente, setModoFuente]     = useState<'Swissquote' | 'API' | 'CSV' | null>(null);
  const [estadoInicial, setEstadoInicial] = useState<EstadoInicialPayload | null>(null);
  const [precioActual, setPrecioActual] = useState<number>(0);

  // ── Estado: demo de descomposición especulativa ───────────────────────────
  const [isCalculando, setIsCalculando]             = useState(false);
  const [modoDemo, setModoDemo]                     = useState<'Secuencial' | 'Paralelo'>('Paralelo');
  const [tiempoMsDemo, setTiempoMsDemo]             = useState<number | null>(null);
  const [predicciones, setPredicciones]             = useState<ApuestaDemo[] | null>(null);
  const [estrategiaSeleccionada, setEstrategiaSeleccionada] = useState<ApuestaDemo | null>(null);
  const [balanceDemo, setBalanceDemo]               = useState(10_000);
  const [logsDemo, setLogsDemo]                     = useState<string[]>([]);

  // Callbacks de alta frecuencia — via refs para evitar re-renders
  const onNuevoPrecioRef    = useRef<((data: NuevoPrecioPayload) => void) | null>(null);
  const onNuevaMetricaRef   = useRef<((data: NuevaMetricaPayload) => void) | null>(null);
  const onResultadoTickRef  = useRef<((data: ResultadoTickPayload) => void) | null>(null);

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
    hub.on('NuevaMetrica',      (data: NuevaMetricaPayload)  => onNuevaMetricaRef.current?.(data));
    hub.on('ResultadoTick',     (data: ResultadoTickPayload) => onResultadoTickRef.current?.(data));
    hub.on('ModoFuenteChanged', (modo: 'Swissquote' | 'API' | 'CSV') => setModoFuente(modo));
    hub.on('EstadoInicial',     (data: EstadoInicialPayload) => setEstadoInicial(data));

    // ── Demo: Descomposición Especulativa ────────────────────────────────────
    hub.on('CalculoIniciado', (data: { modo: string }) => {
      setIsCalculando(true);
      setPredicciones(null);
      setEstrategiaSeleccionada(null);
      setModoDemo(data.modo === 'Secuencial' ? 'Secuencial' : 'Paralelo');
    });

    hub.on('PrediccionesCalculadas', (data: PrediccionesCalculadasPayload) => {
      setIsCalculando(false);
      setTiempoMsDemo(data.tiempoMs);
      setPredicciones(data.estrategias);
    });

    hub.on('EstrategiaSeleccionada', (data: EstrategiaSeleccionadaPayload) => {
      setEstrategiaSeleccionada(data.seleccionada);
      // Fase 6 — verificar apuesta después de 60 segundos
      verificarApuesta(data.seleccionada, hub);
    });

    hub.on('PortafolioActualizado', (data: PortafolioActualizadoPayload) => {
      setBalanceDemo(data.balance);
      if (data.ultimoEvento) {
        setLogsDemo(prev => [...prev.slice(-49), data.ultimoEvento!]);
      }
    });

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
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ── Fase 6: verificación de apuesta a 60 segundos ─────────────────────────
  function verificarApuesta(apuesta: ApuestaDemo, hub: signalR.HubConnection) {
    const precioAlApostar = precioActual; // captura el precio actual

    setTimeout(async () => {
      // En una demo real se obtendría el precio actual del mercado.
      // Aquí usamos el último precio recibido (actualizado en tiempo real).
      const precioFinal = precioActual; // será el precio actualizado tras 60s

      const diferencia = Math.abs(precioFinal - apuesta.precioEsperado);
      const margen = 2; // $2 de tolerancia

      const gano =
        apuesta.direccion === 'Alcista' ? precioFinal > precioAlApostar :
        apuesta.direccion === 'Bajista' ? precioFinal < precioAlApostar :
        diferencia <= margen;

      const emoji    = gano ? '✅' : '❌';
      const etiqueta = gano ? 'GANADA' : 'PERDIDA';
      const mensaje  = `${emoji} Apuesta ${etiqueta} — ${apuesta.nombre} esperaba ${apuesta.direccion} · $${apuesta.precioEsperado.toFixed(2)}`;

      setLogsDemo(prev => [...prev.slice(-49), mensaje]);

      // Notificar al backend para actualizar el portafolio
      try {
        await hub.invoke('RegistrarResultadoApuesta', apuesta.nombre, gano, MONTO_APUESTA);
      } catch {
        // Si el hub ya no está conectado, solo mostramos el log local
      }
    }, 60_000);
  }

  // ── Acciones de la simulación de fondo ────────────────────────────────────
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

  // ── Acción principal de la demo ───────────────────────────────────────────
  const ejecutar = useCallback(async (precio: number, modo: string) => {
    if (!hubRef.current || !conectado) return;
    await hubRef.current.invoke('EjecutarEspeculacion', precio, modo);
  }, [conectado]);

  return {
    // Simulación de fondo
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
    // Demo de descomposición especulativa
    isCalculando,
    modoDemo,
    tiempoMsDemo,
    predicciones,
    estrategiaSeleccionada,
    balanceDemo,
    logsDemo,
    ejecutar,
  };
}
