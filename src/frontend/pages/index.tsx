import { useEffect, useState } from 'react';
import type { MetricaCiclo } from '@/types/simulador';
import { useSimuladorHub } from '@/hooks/useSimuladorHub';
import GraficaVelas from '@/components/GraficaVelas';
import PanelConfiguracion from '@/components/PanelConfiguracion';
import PanelMetricas from '@/components/PanelMetricas';

const BACKEND_URL = process.env.NEXT_PUBLIC_BACKEND_URL ?? 'http://localhost:5000';

export default function Home() {
  const {
    conectado,
    modoFuente,
    estadoInicial,
    iniciar,
    pausar,
    configurar,
    onNuevoPrecioRef,
    onNuevaMetricaRef,
  } = useSimuladorHub();

  const [nucleosDisponibles, setNucleosDisponibles] = useState(4);
  const [simulacionActiva, setSimulacionActiva] = useState(false);
  const [metricasIniciales, setMetricasIniciales] = useState<MetricaCiclo[]>([]);

  // Obtener núcleos disponibles del backend
  useEffect(() => {
    fetch(`${BACKEND_URL}/api/sistema/nucleos`)
      .then((r) => r.json())
      .then((data: { nucleosDisponibles: number }) => setNucleosDisponibles(data.nucleosDisponibles))
      .catch(() => setNucleosDisponibles(4));
  }, []);

  // Sincronizar estado desde EstadoInicial recibido por SignalR
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
            Agentes especulativos paralelos — ITLA · Xavier Casilla 2023-0995
          </p>
        </div>
        <div className="flex items-center gap-3">
          {/* Badge modo fuente — solo se muestra cuando hay datos del backend */}
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
          {/* Estado conexión */}
          <div className="flex items-center gap-1.5">
            <div
              className={`w-2 h-2 rounded-full ${
                conectado ? 'bg-green-400' : 'bg-red-400'
              }`}
            />
            <span className="text-slate-400 text-xs">
              {conectado ? 'Backend conectado' : 'Backend desconectado'}
            </span>
          </div>
        </div>
      </header>

      {/* Layout principal */}
      <main className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Gráfica — ocupa 2/3 en desktop */}
        <div className="lg:col-span-2">
          <GraficaVelas
            onNuevoPrecioRef={onNuevoPrecioRef}
            modoFuente={modoFuente}
          />
        </div>

        {/* Paneles laterales */}
        <div className="flex flex-col gap-4">
          <PanelConfiguracion
            conectado={conectado}
            nucleosDisponibles={nucleosDisponibles}
            simulacionActiva={simulacionActiva}
            onIniciar={handleIniciar}
            onPausar={handlePausar}
            onConfigurar={configurar}
          />
          <PanelMetricas
            onNuevaMetricaRef={onNuevaMetricaRef}
            metricasIniciales={metricasIniciales}
          />
        </div>
      </main>
    </div>
  );
}
