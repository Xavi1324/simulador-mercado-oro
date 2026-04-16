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
    ejecutar,
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

  const handleIniciar = (nucleos: number, intervalo: number) => {
    iniciar(nucleos, intervalo);
    setSimulacionActiva(true);
  };

  const handlePausar = () => {
    pausar();
    setSimulacionActiva(false);
  };

  return (
    <div className="bg-slate-900 min-h-screen text-white p-4">
      {/* Header */}
      <header className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-100">
            Simulador de Mercado de Oro
          </h1>
          <p className="text-slate-500 text-sm">
            Descomposición Especulativa — ITLA · Xavier Casilla 2023-0995
          </p>
        </div>
        <div className="flex items-center gap-3">
          {modoFuente !== null && (
            <span
              className={`text-xs font-semibold px-3 py-1 rounded-full ${
                modoFuente === 'Swissquote'
                  ? 'bg-green-700 text-green-100'
                  : modoFuente === 'API'
                  ? 'bg-blue-700 text-blue-100'
                  : 'bg-yellow-700 text-yellow-100'
              }`}
            >
              {modoFuente === 'Swissquote'
                ? 'Swissquote en vivo'
                : modoFuente === 'API'
                ? 'Metals-API en vivo'
                : 'Histórico CSV'}
            </span>
          )}
          {/* Tiempo demo — solo cuando hay resultado */}
          {tiempoMsDemo !== null && (
            <span className="text-xs font-mono bg-slate-700 text-slate-200 px-3 py-1 rounded-full">
              {modoDemo} · {(tiempoMsDemo / 1000).toFixed(1)} s
            </span>
          )}
          <div className="flex items-center gap-1.5">
            <div className={`w-2 h-2 rounded-full ${conectado ? 'bg-green-400' : 'bg-red-400'}`} />
            <span className="text-slate-400 text-xs">
              {conectado ? 'Backend conectado' : 'Backend desconectado'}
            </span>
          </div>
        </div>
      </header>

      {/* Layout principal */}
      <main className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Gráfica — 2/3 del ancho, con overlay de cálculo */}
        <div className="lg:col-span-2 relative">
          <GraficaVelas
            onNuevoPrecioRef={onNuevoPrecioRef}
            modoFuente={modoFuente}
            predicciones={predicciones}
            estrategiaSeleccionada={estrategiaSeleccionada}
          />
          {isCalculando && (
            <OverlayCalculando modo={modoDemo} />
          )}
        </div>

        {/* Paneles laterales */}
        <div className="flex flex-col gap-4">
          <PanelConfiguracion
            conectado={conectado}
            nucleosDisponibles={nucleosDisponibles}
            simulacionActiva={simulacionActiva}
            isCalculando={isCalculando}
            precioActual={precioActual}
            balanceDemo={balanceDemo}
            onIniciar={handleIniciar}
            onPausar={handlePausar}
            onConfigurar={configurar}
            onEjecutar={ejecutar}
          />
          <PanelMetricas
            onNuevaMetricaRef={onNuevaMetricaRef}
            metricasIniciales={metricasIniciales}
          />
        </div>
      </main>

      {/* Logs de especulación — panel inferior */}
      {logsDemo.length > 0 && (
        <section className="mt-4">
          {LogsEspeculacion ? (
            <LogsEspeculacion logs={logsDemo} />
          ) : (
            <div className="bg-slate-800 rounded-xl p-4">
              <p className="text-slate-400 text-xs uppercase tracking-wider font-semibold mb-2">
                Logs de Especulación
              </p>
              <div className="space-y-1 max-h-40 overflow-y-auto font-mono text-xs">
                {[...logsDemo].reverse().map((log, i) => (
                  <p key={i} className={log.includes('✅') ? 'text-green-400' : log.includes('❌') ? 'text-red-400' : 'text-slate-400'}>
                    {log}
                  </p>
                ))}
              </div>
            </div>
          )}
        </section>
      )}
    </div>
  );
}
