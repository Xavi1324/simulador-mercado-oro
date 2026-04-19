import { useEffect, useState } from 'react';
import dynamic from 'next/dynamic';
import type { ConsoleLogPayload, MetricaCiclo } from '@/types/simulador';
import { useSimuladorHub } from '@/hooks/useSimuladorHub';
import PanelConfiguracion from '@/components/PanelConfiguracion';
import PanelMetricas      from '@/components/PanelMetricas';
import PanelPruebaDatosCompartidos from '@/components/PanelPruebaDatosCompartidos';
import OverlayCalculando  from '@/components/overlays/OverlayCalculando';

const BACKEND_URL = process.env.NEXT_PUBLIC_BACKEND_URL ?? 'http://localhost:5000';

const GraficaVelas = dynamic(() => import('@/components/GraficaVelas'), { ssr: false });

const LOG_PLACEHOLDER_TIMESTAMP = 'placeholder';

function ConsolaSistema({ logs }: { logs: ConsoleLogPayload[] }) {
  const levelClass: Record<ConsoleLogPayload['level'], string> = {
    info: 'text-slate-300',
    success: 'text-green-400',
    warning: 'text-yellow-400',
    error: 'text-red-400',
  };

  const formatearHora = (timestamp: string) =>
    new Intl.DateTimeFormat('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
      timeZone: 'America/New_York',
    }).format(new Date(timestamp));

  const lineas = logs.length > 0
    ? logs
    : [{
        timestamp: LOG_PLACEHOLDER_TIMESTAMP,
        level: 'info' as const,
        fase: 'sistema',
        mensaje: 'Consola lista. Inicie la simulación para ver la ejecución.',
      }];

  return (
    <div className="bg-slate-950 border border-slate-700 rounded-xl overflow-hidden">
      <div className="flex items-center justify-between px-4 py-2 border-b border-slate-800 bg-slate-900">
        <p className="text-slate-300 text-xs uppercase tracking-wider font-semibold">Consola de ejecución</p>
        <span className="text-slate-600 text-xs font-mono">{logs.length} eventos</span>
      </div>
      <div className="h-56 overflow-y-auto px-4 py-3 font-mono text-xs leading-relaxed flex flex-col-reverse">
        <div className="space-y-1">
          {lineas.map((log, i) => (
            <p key={`${log.timestamp}-${i}`} className={levelClass[log.level]}>
              <span className="text-slate-600">
                [{log.timestamp === LOG_PLACEHOLDER_TIMESTAMP ? '--:--:--' : formatearHora(log.timestamp)}]
              </span>{' '}
              <span className="text-blue-300">{log.fase}</span>{' '}
              {log.threadId != null && <span className="text-slate-500">thread:{log.threadId} </span>}
              {log.nucleos != null && <span className="text-slate-500">cpu:{log.nucleos} </span>}
              {log.tick != null && <span className="text-slate-500">tick:{log.tick} </span>}
              <span>{log.mensaje}</span>
            </p>
          ))}
        </div>
      </div>
    </div>
  );
}

export default function Home() {
  const {
    conectado,
    modoFuente,
    estadoInicial,
    iniciar,
    pausar,
    configurar,
    cambiarFuente,
    onNuevoPrecioRef,
    onNuevaMetricaRef,
    isCalculando,
    modoEspeculacion,
    tiempoMsEspeculacion,
    predicciones,
    estrategiaSeleccionada,
    saldoPortafolio,
    pruebaCargaPortafolio,
    consoleLogs,
    segsRestantes,
    ejecutarPruebaCargaPortafolio,
  } = useSimuladorHub();

  const [nucleosDisponibles, setNucleosDisponibles] = useState(4);
  const [nucleosSeleccionados, setNucleosSeleccionados] = useState(1);
  const [intervaloSeleccionado, setIntervaloSeleccionado] = useState(2);
  const [simulacionActiva, setSimulacionActiva]     = useState(false);
  const [metricasIniciales, setMetricasIniciales]   = useState<MetricaCiclo[]>([]);

  useEffect(() => {
    fetch(`${BACKEND_URL}/api/sistema/nucleos`)
      .then((r) => r.json())
      .then((data: { nucleosDisponibles: number }) => setNucleosDisponibles(data.nucleosDisponibles))
      .catch(() => setNucleosDisponibles(4));
  }, []);

  useEffect(() => {
    if (estadoInicial) {
      setSimulacionActiva(estadoInicial.simulacionActiva);
      setNucleosDisponibles(estadoInicial.nucleosDisponibles);
      setNucleosSeleccionados(Math.max(1, Math.min(estadoInicial.nucleos, estadoInicial.nucleosDisponibles)));
      setIntervaloSeleccionado(estadoInicial.intervaloSegundos);
      setMetricasIniciales(estadoInicial.ultimasMetricas ?? []);
    }
  }, [estadoInicial]);

  const handleIniciar = () => {
    iniciar(nucleosSeleccionados, intervaloSeleccionado);
    setSimulacionActiva(true);
  };

  const handlePausar = () => {
    pausar();
    setSimulacionActiva(false);
  };

  return (
    <div className="min-h-screen bg-slate-900 text-white p-4 md:p-10">

      {/* ── Header ── */}
      <header className="mb-6">
        <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-3">

          {/* Logo + título */}
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-green-600 rounded-lg flex items-center justify-center shadow-lg shadow-green-900/40 flex-shrink-0">
              <svg viewBox="0 0 24 24" className="w-6 h-6 text-white" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="22 7 13.5 15.5 8.5 10.5 2 17" />
                <polyline points="16 7 22 7 22 13" />
              </svg>
            </div>
            <div>
              <h1 className="text-xl md:text-2xl font-black text-white tracking-tight">
                XAU/USD <span className="text-green-400">Simulator</span>
              </h1>
              <p className="text-slate-500 text-xs">
                Descomposición Especulativa · ITLA · Xavier Casilla 2023-0995
              </p>
            </div>
          </div>

          {/* Badge de estado compacto */}
          <div className="bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 flex items-center gap-3 flex-wrap">
            {modoFuente !== null && (
              <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${
                modoFuente === 'Swissquote' ? 'bg-green-900 text-green-300'
                : modoFuente === 'API'      ? 'bg-blue-900 text-blue-300'
                :                             'bg-yellow-900 text-yellow-300'
              }`}>
                {modoFuente === 'Swissquote' ? 'Swissquote'
                 : modoFuente === 'API'       ? 'Metals-API'
                 :                             'CSV'}
              </span>
            )}
            {tiempoMsEspeculacion !== null && (
              <span className="text-xs font-mono bg-slate-700 text-slate-300 px-2 py-0.5 rounded-full">
                {modoEspeculacion} · {(tiempoMsEspeculacion / 1000).toFixed(1)} s
              </span>
            )}
            <div className="flex items-center gap-1.5">
              <span className={`w-2 h-2 rounded-full ${conectado ? 'bg-green-400 animate-pulse' : 'bg-red-400'}`}
                    style={conectado ? { boxShadow: '0 0 6px rgba(74,222,128,0.6)' } : {}} />
              <span className="text-slate-400 text-xs font-medium">
                {conectado ? 'Online' : 'Offline'}
              </span>
            </div>
          </div>
        </div>
      </header>

      {/* ── Contenido ── */}
      <div className="space-y-6">

        {/* Gráfica — ancho completo */}
        <GraficaVelas
          onNuevoPrecioRef={onNuevoPrecioRef}
          predicciones={predicciones}
          estrategiaSeleccionada={estrategiaSeleccionada}
          segsRestantes={segsRestantes}
        />

        {/* Paneles — 2 columnas en pantallas grandes */}
        <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
          <PanelConfiguracion
            conectado={conectado}
            nucleosDisponibles={nucleosDisponibles}
            simulacionActiva={simulacionActiva}
            isCalculando={isCalculando}
            saldoPortafolio={saldoPortafolio}
            saldoInicialPortafolio={estadoInicial?.saldoInicialPortafolio ?? 1_000}
            modoFuente={modoFuente}
            nucleos={nucleosSeleccionados}
            intervalo={intervaloSeleccionado}
            onNucleosChange={setNucleosSeleccionados}
            onIntervaloChange={setIntervaloSeleccionado}
            onIniciar={handleIniciar}
            onPausar={handlePausar}
            onConfigurar={configurar}
            onCambiarFuente={cambiarFuente}
          />
          <PanelMetricas
            onNuevaMetricaRef={onNuevaMetricaRef}
            metricasIniciales={metricasIniciales}
          />
        </div>

        <PanelPruebaDatosCompartidos
          conectado={conectado}
          nucleosSeleccionados={nucleosSeleccionados}
          pruebaCargaPortafolio={pruebaCargaPortafolio}
          onEjecutarPruebaCarga={ejecutarPruebaCargaPortafolio}
        />

        <ConsolaSistema logs={consoleLogs} />
      </div>

      {/* ── Footer ── */}
      <footer className="mt-8 pt-6 border-t border-slate-800 flex flex-col md:flex-row justify-between items-center gap-2 text-xs text-slate-600">
        <p>© 2026 ITLA · Programación Paralela · Xavier Casilla 2023-0995</p>
        <div className="flex items-center gap-2">
          <span className="font-mono">Descomposición Especulativa</span>
          <span>·</span>
          <span className="flex items-center gap-1">
            <span className={`w-1.5 h-1.5 rounded-full ${conectado ? 'bg-green-400 animate-pulse' : 'bg-red-400'}`} />
            {conectado ? 'Sistema Operativo' : 'Sin conexión'}
          </span>
        </div>
      </footer>

      <OverlayCalculando modo={modoEspeculacion} visible={isCalculando} />
    </div>
  );
}
