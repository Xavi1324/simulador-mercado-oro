// Evento NuevoPrecio (servidor → cliente)
export interface NuevoPrecioPayload {
  precio: number;
  timestamp: string;
  modo: 'Swissquote' | 'API' | 'CSV';
  tick: number;
}

// Evento NuevaMetrica (servidor → cliente)
export interface NuevaMetricaPayload {
  speedup: number;
  eficiencia: number;
  throughput: number;
  cuellobotella: number;
  tiempoParaleloMs: number;
  tiempoSecuencialMs: number;
  nucleos: number;
}

// Evento ResultadoTick (servidor → cliente)
export interface ResultadoTickPayload {
  agente: number;
  estrategia: string;
  precioEsperado: number;
  precioReal: number;
  ganancia: number;
  esGanadora: boolean;
  portafolioTotal: number;
}

// Evento EstadoInicial (servidor → cliente, al conectar)
export interface EstadoInicialPayload {
  simulacionActiva: boolean;
  nucleos: number;
  nucleosDisponibles: number;
  ultimasMetricas: MetricaCiclo[];
}

// Fila de métricas históricas
export interface MetricaCiclo {
  nucleos: number;
  tickNumero: number;
  tiempoSecuencialMs: number;
  tiempoParaleloMs: number;
  speedup: number;
  eficiencia: number;
  decisionesPorSegundo: number;
  porcentajeLock: number;
  precioOro: number;
  saldoPortafolio: number;
  timestamp: string;
}

// Vela para Lightweight Charts
export interface VelaDatos {
  time: number; // Unix timestamp en segundos
  open: number;
  high: number;
  low: number;
  close: number;
}
