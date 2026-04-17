import { useEffect, useState } from 'react';
import dynamic from 'next/dynamic';
import type { MetricaCiclo } from '@/types/simulador';
import { useSimuladorHub } from '@/hooks/useSimuladorHub';
import PanelConfiguracion from '@/components/PanelConfiguracion';
import PanelMetricas      from '@/components/PanelMetricas';
import OverlayCalculando  from '@/components/overlays/OverlayCalculando';

const BACKEND_URL = process.env.NEXT_PUBLIC_BACKEND_URL ?? 'http://localhost:5000';

// GraficaVelas usa lightweight-charts (solo cliente)
const GraficaVelas = dynamic(() => import('@/components/GraficaVelas'), { ssr: false });

// LogsEspeculacion solo existe en test2-stash; condicionalmente importamos
let LogsEspeculacion: React.ComponentType<{ logs: string[] }> | null = null;
try {
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  LogsEspeculacion = require('@/components/panels/LogsEspeculacion').default;
} catch {
  // No disponible en esta rama — se omite silenciosamente
}

export default function Home() {
  const {
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
    // Demo especulativa
    isCalculando,
    modoDemo,
    tiempoMsDemo,
    predicciones,
    estrategiaSeleccionada,
    balanceDemo,
    logsDemo,
  } = useSimuladorHub();

  const [nucleosDisponibles, setNucleosDisponibles] = useState(4);
  const [simulacionActiva, setSimulacionActiva]     = useState(false);
  const [metricasIniciales, setMetricasIniciales]   = useState<MetricaCiclo[]>([]);

  // Obtener núcleos del backend
  useEffect(() => {
    fetch(`${BACKEND_URL}/api/sistema/nucleos`)
      .then((r) => r.json())
      .then((data: { nucleosDisponibles: number }) => setNucleosDisponibles(data.nucleosDisponibles))
      .catch(() => setNucleosDisponibles(4));
  }, []);

  // Sincronizar desde EstadoInicial SignalR
  useEffect(() => {
    if (estadoInicial) {
      setSimulacionActiva(estadoInicial.simulacionActiva);
      setNucleosDisponibles(estadoInicial.nucleosDisponibles);
      setMetricasIniciales(estadoInicial.ultimasMetricas ?? []);
    }
  }, [estadoInicial]);

  const handleIniciar = (nucleos: number, intervalo: number, modo: 'Secuencial' | 'Paralelo') => {
    iniciar(nucleos, intervalo, modo);
    setSimulacionActiva(true);
  };

  const handlePausar = () => {
    pausar();
    setSimulacionActiva(false);
  };

  return (
    <div className="bg-slate-900 text-white">

      {/* ── Header ultracompacto ── */}
      <header className="px-4 h-10 flex items-center justify-between flex-shrink-0">
        <div className="flex items-center gap-3">
          <h1 className="text-sm font-bold text-slate-200 tracking-wide uppercase">
            Simulador XAU/USD
          </h1>
          <span className="text-slate-600 text-xs hidden sm:inline">
            Descomposición Especulativa · ITLA · Xavier Casilla
          </span>
        </div>
        <div className="flex items-center gap-2">
          {modoFuente !== null && (
            <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${
              modoFuente === 'Swissquote' ? 'bg-green-800 text-green-300'
              : modoFuente === 'API'      ? 'bg-blue-800 text-blue-300'
              :                             'bg-yellow-800 text-yellow-300'
            }`}>
              {modoFuente === 'Swissquote' ? '● Swissquote'
               : modoFuente === 'API'       ? '● Metals-API'
               :                             '● CSV'}
            </span>
          )}
          {tiempoMsDemo !== null && (
            <span className="text-xs font-mono bg-slate-700 text-slate-300 px-2 py-0.5 rounded-full">
              {modoDemo} {(tiempoMsDemo / 1000).toFixed(1)}s
            </span>
          )}
          <div className="flex items-center gap-1">
            <div className={`w-1.5 h-1.5 rounded-full ${conectado ? 'bg-green-400' : 'bg-red-400'}`} />
            <span className="text-slate-500 text-xs">
              {conectado ? 'Online' : 'Offline'}
            </span>
          </div>
        </div>
      </header>

      {/* ── Gráfica — llena el resto del viewport ── */}
      <div className="px-3" style={{ height: 'calc(100vh - 2.5rem)' }}>
        <GraficaVelas
          onNuevoPrecioRef={onNuevoPrecioRef}
          modoFuente={modoFuente}
          predicciones={predicciones}
          estrategiaSeleccionada={estrategiaSeleccionada}
        />
      </div>

      {/* ── Paneles lado a lado — requieren scroll ── */}
      <div className="px-3 pt-3 pb-3 grid grid-cols-1 md:grid-cols-2 gap-3">
        <PanelConfiguracion
          conectado={conectado}
          nucleosDisponibles={nucleosDisponibles}
          simulacionActiva={simulacionActiva}
          isCalculando={isCalculando}
          balanceDemo={balanceDemo}
          onIniciar={handleIniciar}
          onPausar={handlePausar}
          onConfigurar={configurar}
        />
        <PanelMetricas
          onNuevaMetricaRef={onNuevaMetricaRef}
          metricasIniciales={metricasIniciales}
        />
      </div>

      {/* ── Registro de apuestas — al final ── */}
      {logsDemo.length > 0 && (
        <div className="px-3 pb-6">
          <div className="bg-slate-800 rounded-xl p-4">
            <p className="text-slate-500 text-xs uppercase tracking-wider font-semibold mb-2">
              Registro de apuestas
            </p>
            <div className="space-y-0.5 max-h-36 overflow-y-auto font-mono text-xs">
              {[...logsDemo].reverse().map((log, i) => (
                <p key={i} className={
                  log.includes('✅') ? 'text-green-400'
                  : log.includes('❌') ? 'text-red-400'
                  : 'text-slate-400'
                }>
                  {log}
                </p>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Toast de cálculo en curso */}
      <OverlayCalculando modo={modoDemo} visible={isCalculando} />
    </div>
  );
}
