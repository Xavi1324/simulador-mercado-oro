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

// Evento EstadoInicial (servidor → cliente, al conectar)
export interface EstadoInicialPayload {
  simulacionActiva: boolean;
  nucleos: number;
  nucleosDisponibles: number;
  ultimasMetricas: MetricaCiclo[];
  saldoInicialPortafolio: number;
  saldoPortafolio: number;
  modoEspeculacion: 'Secuencial' | 'Paralelo';
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

// ── Descomposición Especulativa ───────────────────────────────────────────────

// Una estrategia calculada por el backend
export interface ApuestaEspeculativa {
  nombre: string;
  precioEsperado: number;
  precioMin?: number;   // solo Conservadora — extremo bajo del rango
  precioMax?: number;   // solo Conservadora — extremo alto del rango
  direccion: 'Alcista' | 'Bajista' | 'Neutro';
  tiempoExpiracion: string;
}

// Evento "PrediccionesCalculadas" — llega cuando las 3 estrategias terminaron
export interface PrediccionesCalculadasPayload {
  estrategias: ApuestaEspeculativa[];
  tiempoMs: number;
  modo: 'Secuencial' | 'Paralelo';
  tick: number;
}

// Evento "EstrategiaSeleccionada" — la ganadora + las 2 descartadas
export interface EstrategiaSeleccionadaPayload {
  seleccionada: ApuestaEspeculativa;
  descartadas: ApuestaEspeculativa[];
  tick: number;
}

// Evento "PortafolioActualizado" — balance tras registrar resultado
export interface PortafolioActualizadoPayload {
  balance: number;
  ultimoEvento: string | null;
}

// Evento PruebaCargaPortafolio (servidor → cliente)
export interface PruebaCargaPortafolioPayload {
  estado: 'iniciada' | 'progreso' | 'completada' | 'fallida';
  operaciones: number;
  completadas: number;
  concurrencia: number;
  trabajoCriticoMs: number;
  montoOperacion: number;
  saldoInicial: number;
  saldoEsperado: number;
  saldoObtenido: number;
  ganadas: number;
  perdidas: number;
  tiempoTotalMs: number;
  tiempoEsperaLockMs: number;
  porcentajeLock: number;
  adquisicionesLock: number;
  consistente: boolean;
}

// Evento ConsoleLog (servidor → cliente)
export interface ConsoleLogPayload {
  timestamp: string;
  level: 'info' | 'success' | 'warning' | 'error';
  fase: string;
  mensaje: string;
  tick?: number;
  nucleos?: number;
  modo?: 'Secuencial' | 'Paralelo';
  threadId?: number;
  agente?: number;
  speedup?: number;
  eficiencia?: number;
  tiempoSecuencialMs?: number;
  tiempoParaleloMs?: number;
}
